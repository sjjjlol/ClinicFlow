using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Integration;

public class SyncAttempt
{
    public long Id { get; set; }
    public string MessageId { get; set; } = "";
    public string LeaseToken { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public string Outcome { get; set; } = "Started";
}

public record Claim(
    string Id,
    string Token,
    string Payload,
    string AppointmentId,
    int Version,
    string CorrelationId
);

public class Dispatcher(ClinicDb db, HttpClient client, TimeProvider clock, IConfiguration config)
{
    public const int MaxAttempts = 5;

    public static TimeSpan Backoff(int attempt, double jitter) =>
        TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempt)) + Math.Clamp(jitter, 0, 1));

    public async Task<Claim?> ClaimNext(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        var message = (
            await db
                .Outbox.FromSqlInterpolated(
                    $"SELECT * FROM Outbox WHERE (Status='Pending' AND NextAttemptUtc<={now}) OR (Status='Processing' AND LeaseUntilUtc<={now}) ORDER BY NextAttemptUtc,Id LIMIT 1 FOR UPDATE SKIP LOCKED"
                )
                .ToListAsync(ct)
        ).SingleOrDefault();
        if (message is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }
        if (message.Attempts >= MaxAttempts)
        {
            message.Status = "Failed";
            message.LeaseToken = null;
            message.LeaseUntilUtc = null;
            message.LastError = "Lease expired after attempt limit";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return null;
        }
        message.Status = "Processing";
        message.Attempts++;
        message.LeaseToken = Guid.NewGuid().ToString();
        message.LeaseUntilUtc = now.AddSeconds(30);
        db.Set<SyncAttempt>()
            .Add(
                new SyncAttempt
                {
                    MessageId = message.Id,
                    LeaseToken = message.LeaseToken,
                    AtUtc = now,
                }
            );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var claim = new Claim(
            message.Id,
            message.LeaseToken,
            message.Payload,
            message.AppointmentId,
            message.Version,
            message.CorrelationId
        );
        db.ChangeTracker.Clear();
        return claim;
    }

    public async Task Deliver(Claim claim, CancellationToken ct)
    {
        bool delivered = false,
            permanent = false;
        string outcome;
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(new Uri(config["Integration:Url"] ?? "http://127.0.0.1:5090"), "/messages")
            );
            request.Headers.Add(
                "X-Integration-Key",
                config["INTEGRATION_TOKEN"]
                    ?? throw new InvalidOperationException("INTEGRATION_TOKEN required")
            );
            request.Headers.Add("X-Correlation-ID", claim.CorrelationId);
            request.Content = JsonContent.Create(
                new
                {
                    messageId = claim.Id,
                    appointmentId = claim.AppointmentId,
                    version = claim.Version,
                    snapshot = JsonSerializer.Deserialize<JsonElement>(claim.Payload),
                }
            );
            using var response = await client.SendAsync(request, ct);
            delivered = response.IsSuccessStatusCode;
            permanent =
                (int)response.StatusCode >= 400
                && (int)response.StatusCode < 500
                && response.StatusCode != HttpStatusCode.TooManyRequests
                && response.StatusCode != HttpStatusCode.RequestTimeout;
            outcome = delivered ? "Delivered" : $"HTTP {(int)response.StatusCode}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            outcome = "HTTP timeout";
        }
        catch (HttpRequestException)
        {
            outcome = "External service unavailable";
        }
        // Cancellation of the worker leaves the lease to expire; no guessed failure/success is saved.
        await Complete(claim, delivered, permanent, outcome, ct);
    }

    public async Task<bool> Complete(
        Claim claim,
        bool delivered,
        bool permanent,
        string outcome,
        CancellationToken ct
    )
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        var message = (
            await db
                .Outbox.FromSqlInterpolated($"SELECT * FROM Outbox WHERE Id={claim.Id} FOR UPDATE")
                .ToListAsync(ct)
        ).Single();
        if (
            message.LeaseToken != claim.Token
            || message.Status != "Processing"
            || message.LeaseUntilUtc <= now
        )
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return false;
        }
        message.Status =
            delivered ? "Delivered"
            : permanent || message.Attempts >= MaxAttempts ? "Failed"
            : "Pending";
        message.LastError = delivered ? null : outcome;
        message.LeaseToken = null;
        message.LeaseUntilUtc = null;
        message.NextAttemptUtc = now + Backoff(message.Attempts, Random.Shared.NextDouble());
        var attempt = await db.Set<SyncAttempt>().SingleAsync(x => x.LeaseToken == claim.Token, ct);
        attempt.Outcome = outcome;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }
}

public class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
                var claim = await dispatcher.ClaimNext(stoppingToken);
                if (claim is not null)
                    await dispatcher.Deliver(claim, stoppingToken);
                else
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox worker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}

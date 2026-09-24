using System.Net;
using ClinicFlow;
using ClinicFlow.Integration;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClinicFlow.Tests;

public class IntegrationTests : IAsyncLifetime
{
    readonly SchedulingTests fixture = new();

    ClinicDb Db() => fixture.Db();

    Task<Appointment> Create() => fixture.Create();

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => fixture.DisposeAsync();

    static IConfiguration Config =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["INTEGRATION_TOKEN"] = "test-token" }
            )
            .Build();

    public class Clock : TimeProvider
    {
        DateTimeOffset now = DateTimeOffset.UtcNow.AddMinutes(1);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }

    public class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) => Task.FromResult(response());
    }

    static Dispatcher Worker(
        ClinicDb db,
        Clock clock,
        Func<HttpResponseMessage>? response = null
    ) =>
        new(
            db,
            new HttpClient(new Handler(response ?? (() => new(HttpStatusCode.OK)))),
            clock,
            Config
        );

    [Fact]
    public async Task A13_OutageDoesNotRollbackLocalBookingAndRecovers()
    {
        var a = await Create();
        var clock = new Clock();
        await using var db = Db();
        var down = Worker(db, clock, () => throw new HttpRequestException());
        var claim = (await down.ClaimNext(default))!;
        await down.Deliver(claim, default);
        Assert.Equal("Pending", (await db.Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.NotNull(await db.Appointments.FindAsync(a.Id));
        clock.Advance(20);
        var up = Worker(db, clock);
        await up.Deliver((await up.ClaimNext(default))!, default);
        Assert.Equal("Delivered", (await db.Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await db.Set<SyncAttempt>().CountAsync());
    }

    [Fact]
    public async Task A16_ParallelClaimsAndLeaseRecoveryRejectOldOwner()
    {
        await Create();
        var clock = new Clock();
        await using var db1 = Db();
        await using var db2 = Db();
        var w1 = Worker(db1, clock);
        var w2 = Worker(db2, clock);
        var claims = await Task.WhenAll(w1.ClaimNext(default), w2.ClaimNext(default));
        Assert.Single(claims, x => x is not null);
        var old = claims.Single(x => x is not null)!;
        clock.Advance(31);
        await using var db3 = Db();
        var w3 = Worker(db3, clock);
        var fresh = (await w3.ClaimNext(default))!;
        Assert.Equal(old.Id, fresh.Id);
        Assert.NotEqual(old.Token, fresh.Token);
        Assert.False(await w1.Complete(old, true, false, "stale", default));
        Assert.True(await w3.Complete(fresh, true, false, "Delivered", default));
        await using var verify = Db();
        Assert.Equal("Delivered", (await verify.Outbox.SingleAsync()).Status);
        Assert.Equal(2, await verify.Set<SyncAttempt>().CountAsync());
    }

    [Fact]
    public async Task A17_RetryLimitAndIdempotentManualRetryPreserveIdentityHistory()
    {
        await Create();
        var clock = new Clock();
        await using var db = Db();
        var worker = Worker(db, clock, () => new(HttpStatusCode.ServiceUnavailable));
        string? id = null;
        for (var i = 0; i < 5; i++)
        {
            var claim = (await worker.ClaimNext(default))!;
            id = claim.Id;
            await worker.Deliver(claim, default);
            clock.Advance(400);
        }
        Assert.Equal("Failed", (await db.Outbox.AsNoTracking().SingleAsync()).Status);
        Assert.Null(await worker.ClaimNext(default));
        await using (var retryDb = Db())
            await new SchedulingService(retryDb, new NoTransactionProbe()).RetrySync(
                id!,
                "admin",
                "retry-key",
                "retry",
                default
            );
        await using (var replayDb = Db())
            await new SchedulingService(replayDb, new NoTransactionProbe()).RetrySync(
                id!,
                "admin",
                "retry-key",
                "retry",
                default
            );
        db.ChangeTracker.Clear();
        var saved = await db.Outbox.SingleAsync();
        Assert.Equal(id, saved.Id);
        Assert.Equal(0, saved.Attempts);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal(5, await db.Set<SyncAttempt>().CountAsync());
        Assert.Equal(1, await db.Audits.CountAsync(x => x.Action == "SyncRetry"));
    }

    [Fact]
    public async Task PermanentFailureDoesNotRetryAutomatically()
    {
        await Create();
        var clock = new Clock();
        await using var db = Db();
        var worker = Worker(db, clock, () => new(HttpStatusCode.UnprocessableEntity));
        await worker.Deliver((await worker.ClaimNext(default))!, default);
        Assert.Equal("Failed", (await db.Outbox.SingleAsync()).Status);
    }

    [Fact]
    public void BackoffIsBoundedAndJittered()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), Dispatcher.Backoff(1, 0));
        Assert.Equal(TimeSpan.FromSeconds(301), Dispatcher.Backoff(20, 1));
    }
}

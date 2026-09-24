using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Integration;

// SyncAttempt：每次投递尝试的流水记录（含租约令牌，便于审计"谁完成/失败了这次投递"）。
public class SyncAttempt
{
    public long Id { get; set; }
    public string MessageId { get; set; } = "";
    public string LeaseToken { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public string Outcome { get; set; } = "Started";
}

// record 型 DTO：一次被认领的消息（跨方法传递的不可变快照）。
public record Claim(
    string Id,
    string Token,
    string Payload,
    string AppointmentId,
    int Version,
    string CorrelationId
);

// ===== Dispatcher：发件箱投递器（租约 + 指数退避 + 结果未知处理）=====
// 主构造函数注入：ClinicDb（Scoped）、HttpClient（工厂托管）、TimeProvider（时钟抽象）、IConfiguration。
public class Dispatcher(ClinicDb db, HttpClient client, TimeProvider clock, IConfiguration config)
{
    // const ≈ Java static final（编译期常量，必须字面量）。
    public const int MaxAttempts = 5;

    // 指数退避 + 抖动：2^attempt 秒封顶 300 秒，外加 [0,1] 秒随机抖动防惊群。
    public static TimeSpan Backoff(int attempt, double jitter) =>
        TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempt)) + Math.Clamp(jitter, 0, 1));

    // ClaimNext：认领一条待投递消息。返回 Claim?（可空）——没有可认领消息时为 null。
    public async Task<Claim?> ClaimNext(CancellationToken ct)
    {
        // TimeProvider.GetUtcNow() ≈ Instant.now()，但测试可替换时钟（≈ 注入 Clock）。
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        var message = (
            await db
                // FOR UPDATE SKIP LOCKED：多实例并发消费时各自认领不同行（≈ 数据库版工作队列）。
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
        // 租约过期且已达上限 → 标记 Failed（死信），释放租约。
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
        // 改被跟踪实体的属性 → SaveChanges 生成 UPDATE；租约 30 秒，令牌唯一标识本次认领。
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
        // ChangeTracker.Clear()：清空 EF 变更跟踪——Dispatcher 跨多次调用复用同一 DbContext，
        // 不清理会累积跟踪的实体（内存膨胀 + 后续 SaveChanges 误提交陈旧状态）。
        db.ChangeTracker.Clear();
        return claim;
    }

    public async Task Deliver(Claim claim, CancellationToken ct)
    {
        // 一行声明多个同类型变量（bool delivered = false, permanent = false; 的换行写法）。
        bool delivered = false,
            permanent = false;
        string outcome;
        try
        {
            // using var ≈ try-with-resources：HttpRequestMessage/response 用完即释放。
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                // new Uri(base, relative)：组合地址（≈ URI.resolve）。
                new Uri(new Uri(config["Integration:Url"] ?? "http://127.0.0.1:5090"), "/messages")
            );
            request.Headers.Add(
                "X-Integration-Key",
                config["INTEGRATION_TOKEN"]
                    ?? throw new InvalidOperationException("INTEGRATION_TOKEN required")
            );
            request.Headers.Add("X-Correlation-ID", claim.CorrelationId);
            // JsonContent.Create：匿名对象 → JSON body（≈ RestTemplate 的 body 序列化）。
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
            // 4xx（除 429/408）视为永久失败，不再重试；(int)response.StatusCode 是枚举转数值的强制转换。
            permanent =
                (int)response.StatusCode >= 400
                && (int)response.StatusCode < 500
                && response.StatusCode != HttpStatusCode.TooManyRequests
                && response.StatusCode != HttpStatusCode.RequestTimeout;
            // $"" 内插字符串里可直接写表达式。
            outcome = delivered ? "Delivered" : $"HTTP {(int)response.StatusCode}";
        }
        // when (!ct.IsCancellationRequested)：HttpClient 自身超时抛的取消 ≠ 应用停机取消，区别对待。
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            outcome = "HTTP timeout";
        }
        catch (HttpRequestException)
        {
            outcome = "External service unavailable";
        }
        // Cancellation of the worker leaves the lease to expire; no guessed failure/success is saved.
        // 关键设计：应用停机/请求取消时不猜测 HTTP 是否已生效——留给租约过期后重投（"结果未知"语义）。
        await Complete(claim, delivered, permanent, outcome, ct);
    }

    // Complete：凭租约令牌确认完成；返回 bool 表示本次完成是否被接受（令牌失配/过期则拒绝）。
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
        // 租约校验：令牌不匹配 / 状态不是 Processing / 租约已过期 → 拒绝本次完成。
        // 防的就是：慢请求超时后消息被重新认领，旧 worker 晚到的响应覆盖新状态。
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
        // 嵌套三元：delivered→Delivered；(permanent 或达上限)→Failed；否则回 Pending 等待退避重试。
        message.Status =
            delivered ? "Delivered"
            : permanent || message.Attempts >= MaxAttempts ? "Failed"
            : "Pending";
        message.LastError = delivered ? null : outcome;
        message.LeaseToken = null;
        message.LeaseUntilUtc = null;
        // Random.Shared：线程安全的共享随机数实例（.NET 6+；≈ ThreadLocalRandom.current()）。
        message.NextAttemptUtc = now + Backoff(message.Attempts, Random.Shared.NextDouble());
        var attempt = await db.Set<SyncAttempt>().SingleAsync(x => x.LeaseToken == claim.Token, ct);
        attempt.Outcome = outcome;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }
}

// ===== OutboxWorker：后台轮询循环（对照 @Scheduled(fixedDelay) 或独立消费线程）=====
// BackgroundService：由宿主管理生命周期的后台服务基类；应用停机时取消 stoppingToken。
// IServiceScopeFactory 是 Singleton 安全的——worker 本身是 Singleton，不能直持 Scoped 的 DbContext。
public class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> logger)
    : BackgroundService
{
    // ILogger<T>：泛型日志（≈ LoggerFactory.getLogger(OutboxWorker.class)），T 决定日志分类名。
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 每轮新建 AsyncScope：Scoped 服务（ClinicDb 等）随轮次创建并释放——
                // 这是后台服务消费 Scoped 依赖的标准姿势（铁律：Singleton 不能长期持有 Scoped）。
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
                var claim = await dispatcher.ClaimNext(stoppingToken);
                if (claim is not null)
                    await dispatcher.Deliver(claim, stoppingToken);
                else
                    // Task.Delay ≈ Thread.sleep 的异步版：不占线程，可被停止令牌打断。
                    await Task.Delay(1000, stoppingToken);
            }
            // 停机取消 → 跳出循环，优雅退出。
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // LogError(ex, "...")：结构化日志，异常对象单独传（保留堆栈）。
                logger.LogError(ex, "Outbox worker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}

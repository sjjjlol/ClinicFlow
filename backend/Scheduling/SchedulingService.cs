using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ClinicFlow.Scheduling;

// ===== 事务探针接口：测试专用接缝（fault injection point），生产注册 NoTransactionProbe 空实现 =====
// interface 与 Java 接口相同；Task = 无返回值的异步操作（≈ CompletableFuture<Void>）。
// Test probes are supplied through DI only; normal runtime never enables fault injection.
public interface ITransactionProbe
{
    Task Reach(string point, CancellationToken ct);
}

public class NoTransactionProbe : ITransactionProbe
{
    // => 表达式体方法：一行实现。Task.CompletedTask = 已完成的空任务（≈ CompletableFuture.completedFuture(null)）。
    public Task Reach(string point, CancellationToken ct) => Task.CompletedTask;
}

// 主构造函数：SchedulingService(ClinicDb db, ITransactionProbe probe)
// = 声明两个构造参数并可在类体内直接当字段用；DI 容器按参数类型自动注入（无需 @Autowired）。
public class SchedulingService(ClinicDb db, ITransactionProbe probe, TimeProvider? clock = null)
{
    // static readonly ≈ Java static final；JsonSerializerDefaults.Web = camelCase 命名策略。
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new UtcDateTimeConverter() }, // 集合初始化器语法（≈ list.add 的声明式）
    };

    // 静态方法 + 表达式体；SHA256.HashData 一次性哈希（≈ MessageDigest.getInstance("SHA-256").digest(...)）。
    static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    // Task<Appointment> = 异步返回 Appointment（≈ CompletableFuture<Appointment>）。
    // => Execute(...) 表达式体：整个方法体就是调用 Execute 模板方法。
    public Task<Appointment> Create(
        Booking input,
        string actor,
        string key,
        string correlation,
        CancellationToken ct,
        bool selfService = false
    ) =>
        Execute(
            actor,
            "create",
            key,
            // with 表达式：record 的"复制并修改"——生成一个把时间转为 UTC 的新 Booking（Java record 无此语法）。
            input with
            {
                StartUtc = input.StartUtc.ToUniversalTime(),
                EndUtc = input.EndUtc.ToUniversalTime(),
            },
            // async lambda：传给 Execute 的业务动作，稍后在其事务内执行。
            async () =>
            {
                if (selfService)
                    RequireFuture(input.StartUtc.UtcDateTime);
                var slots = Rules.Slots(input.StartUtc.UtcDateTime, input.EndUtc.UtcDateTime);
                // [input.ResourceId]：C# 12 集合表达式，创建只有一个元素的数组。
                await LockResources([input.ResourceId], ct);
                // AnyAsync ≈ SELECT EXISTS(...)（≈ JPA count > 0 判断）。
                if (!await db.Patients.AnyAsync(x => x.Id == input.PatientId, ct))
                    throw new BusinessException("patient_missing", "患者不存在", 404);
                await CheckFree(input.ResourceId, slots, null, ct);
                // new Appointment { ... }：对象初始化器（≈ builder 链式 set 后 build）。
                var a = new Appointment
                {
                    PatientId = input.PatientId,
                    ResourceId = input.ResourceId,
                    StartUtc = input.StartUtc.UtcDateTime,
                    EndUtc = input.EndUtc.UtcDateTime,
                };
                // Add ≈ EntityManager.persist：标记为新实体，SaveChanges 时生成 INSERT。
                db.Appointments.Add(a);
                Claim(a, slots);
                // AddRange：批量 Add（多个实体一次标记）。
                db.Tasks.AddRange(
                    new PrerequisiteTask { AppointmentId = a.Id, Name = "资料核对" },
                    new PrerequisiteTask { AppointmentId = a.Id, Name = "预约信息复核" }
                );
                Record(a, "Created", actor, correlation);
                await probe.Reach("before-save", ct); // 测试注入点：可在 flush 前制造并发交错
                return a;
            },
            ct
        );

    public Task<Appointment> Mutate(
        string id,
        string operation,
        Mutation input,
        string actor,
        string key,
        string correlation,
        CancellationToken ct,
        int? patientScope = null
    ) =>
        Execute(
            actor,
            operation + ":" + id,
            key,
            input with
            {
                StartUtc = input.StartUtc?.ToUniversalTime(),
                EndUtc = input.EndUtc?.ToUniversalTime(),
            },
            async () =>
            {
                // ?? throw：null 合并的抛出形式——查不到就抛 404（≈ Optional.orElseThrow）。
                // 预读用 AsNoTracking：只是业务校验，不做变更跟踪；预读不是锁！
                var before =
                    await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new BusinessException("appointment_missing", "预约不存在", 404);
                if (patientScope is not null)
                {
                    if (before.PatientId != patientScope)
                        throw new BusinessException("appointment_missing", "预约不存在", 404);
                    if (operation is not ("cancel" or "reschedule"))
                        throw new BusinessException("forbidden", "当前角色没有此操作权限", 403);
                    RequireFuture(before.StartUtc);
                }
                await probe.Reach("after-preread", ct);
                // 资源按固定顺序（升序）加锁，避免两个请求交叉锁两个资源造成死锁。
                await LockResources([before.ResourceId, input.ResourceId ?? before.ResourceId], ct);
                // FromSqlInterpolated：原生 SQL 混 LINQ——$"" 内插值自动参数化（防注入），
                // 结果仍映射为被跟踪的实体。FOR UPDATE = MySQL 行锁（≈ JPA PESSIMISTIC_WRITE）。
                var a = (
                    await db
                        .Appointments.FromSqlInterpolated(
                            $"SELECT * FROM Appointments WHERE Id={id} FOR UPDATE"
                        )
                        .ToListAsync(ct)
                ).Single(); // Single()：内存中断言恰好 1 条（此时已加行锁，必存在）
                // 行锁内再校验：资源未变 + 版本匹配（乐观锁 Version ≈ JPA @Version 的手动版）。
                if (a.ResourceId != before.ResourceId || a.Version != input.Version)
                    throw new BusinessException(
                        "version_conflict",
                        "预约已被其他人更新，请刷新详情后重新操作"
                    );
                if (a.Status is "Cancelled" or "Completed")
                    throw new BusinessException(
                        "invalid_state",
                        "已取消或已完成的预约不可继续修改"
                    );
                if (patientScope is not null)
                    RequireFuture(a.StartUtc);
                object? change = null; // object ≈ Java Object；这里装随操作变化的审计附加信息
                if (operation == "reschedule")
                {
                    if (input.ResourceId is null || input.StartUtc is null || input.EndUtc is null)
                        throw new BusinessException(
                            "invalid_time",
                            "改期须提供资源及起止时间",
                            400
                        );
                    if (patientScope is not null)
                        RequireFuture(input.StartUtc.Value.UtcDateTime);
                    // .Value：取 Nullable<T> 的值（判空后使用；≈ Optional.get()）。
                    var slots = Rules.Slots(
                        input.StartUtc.Value.UtcDateTime,
                        input.EndUtc.Value.UtcDateTime
                    );
                    await CheckFree(input.ResourceId.Value, slots, a.Id, ct);
                    // RemoveRange ≈ EntityManager.remove 批量版（标记删除，SaveChanges 时生成 DELETE）。
                    db.SlotClaims.RemoveRange(
                        await db.SlotClaims.Where(x => x.AppointmentId == id).ToListAsync(ct)
                    );
                    // SaveChangesAsync ≈ em.flush()：生成并执行 SQL，但事务未提交！
                    // 先 flush 删除旧槽位，让后续新槽位插入不与唯一键冲突；异常则整体回滚，中间状态不外泄。
                    await db.SaveChangesAsync(ct); // deletion remains uncommitted; overlapping claims can now be reinserted
                    await probe.Reach("after-release", ct);
                    // 直接改被跟踪实体的属性：SaveChanges 时 EF 脏检查自动生成 UPDATE（≈ JPA dirty checking）。
                    a.ResourceId = input.ResourceId.Value;
                    a.StartUtc = input.StartUtc.Value.UtcDateTime;
                    a.EndUtc = input.EndUtc.Value.UtcDateTime;
                    a.Status = "Pending";
                    Claim(a, slots);
                    foreach (
                        var task in await db.Tasks.Where(x => x.AppointmentId == id).ToListAsync(ct)
                    )
                    {
                        task.Completed = false;
                        task.CompletedBy = null;
                        task.CompletedUtc = null;
                    }
                }
                else if (operation == "cancel")
                {
                    db.SlotClaims.RemoveRange(
                        await db.SlotClaims.Where(x => x.AppointmentId == id).ToListAsync(ct)
                    );
                    a.Status = "Cancelled";
                }
                else if (operation == "complete")
                {
                    if (a.Status != "Confirmed")
                        throw new BusinessException("invalid_state", "仅已确认预约可登记完成");
                    if (a.EndUtc > (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime)
                        throw new BusinessException(
                            "appointment_not_ended",
                            "预约尚未结束，不能登记完成"
                        );
                    db.SlotClaims.RemoveRange(
                        await db.SlotClaims.Where(x => x.AppointmentId == id).ToListAsync(ct)
                    );
                    a.Status = "Completed";
                }
                else if (operation == "complete-task")
                {
                    if (a.Status != "Pending")
                        throw new BusinessException("invalid_state", "仅待确认预约可完成前置任务");
                    var task =
                        await db.Tasks.SingleOrDefaultAsync(
                            // 表达式里同时用路由 id 和 input.TaskId（int?），== 对可空类型做值比较。
                            x => x.AppointmentId == id && x.Id == input.TaskId,
                            ct
                        ) ?? throw new BusinessException("task_missing", "任务不存在", 404);
                    if (task.Completed)
                        throw new BusinessException("task_completed", "该任务已完成，请刷新详情");
                    task.Completed = true;
                    task.CompletedBy = actor;
                    task.CompletedUtc = DateTime.UtcNow;
                    change = new
                    {
                        task.Id,
                        task.Name,
                        task.CompletedBy,
                        task.CompletedUtc,
                    };
                }
                else if (operation == "confirm")
                {
                    if (a.Status != "Pending")
                        throw new BusinessException("invalid_state", "仅待确认预约可确认");
                    // !x.Completed：lambda 内的逻辑非，翻译成 SQL NOT。
                    if (await db.Tasks.AnyAsync(x => x.AppointmentId == id && !x.Completed, ct))
                        throw new BusinessException(
                            "prerequisites_incomplete",
                            "请先完成本次预约的全部前置任务"
                        );
                    a.Status = "Confirmed";
                }
                else
                    throw new BusinessException("invalid_operation", "未知操作", 400);
                a.Version++;
                a.UpdatedUtc = DateTime.UtcNow;
                Record(
                    a,
                    // switch 表达式（≈ Java 21 switch 表达式）：按值匹配返回字符串，_ 是默认分支。
                    operation switch
                    {
                        "cancel" => "Cancelled",
                        "reschedule" => "Rescheduled",
                        "confirm" => "Confirmed",
                        "complete" => "Completed",
                        _ => "TaskCompleted",
                    },
                    actor,
                    correlation,
                    change
                );
                await probe.Reach("before-save", ct);
                return a;
            },
            ct
        );

    void RequireFuture(DateTime start)
    {
        if (start <= (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime)
            throw new BusinessException(
                "appointment_started",
                "请选择未来时间；已开始的预约请联系工作人员处理",
                409
            );
    }

    public Task<Appointment> RetrySync(
        string id,
        string actor,
        string key,
        string correlation,
        CancellationToken ct
    ) =>
        Execute(
            actor,
            "retry-sync:" + id,
            key,
            new { id },
            async () =>
            {
                var message =
                    (
                        await db
                            .Outbox.FromSqlInterpolated(
                                $"SELECT * FROM Outbox WHERE Id={id} FOR UPDATE"
                            )
                            .ToListAsync(ct)
                    ).SingleOrDefault()
                    ?? throw new BusinessException("message_missing", "同步消息不存在", 404);
                if (message.Status != "Failed")
                    throw new BusinessException(
                        "sync_not_failed",
                        "仅失败消息可手动重试，请刷新队列"
                    );
                message.Status = "Pending";
                message.Attempts = 0;
                message.NextAttemptUtc = DateTime.UtcNow;
                message.LeaseToken = null;
                message.LeaseUntilUtc = null;
                var a = await db
                    .Appointments.AsNoTracking()
                    .SingleAsync(x => x.Id == message.AppointmentId, ct);
                db.Audits.Add(
                    new AuditEntry
                    {
                        AppointmentId = a.Id,
                        Action = "SyncRetry",
                        Actor = actor,
                        Version = a.Version,
                        Summary = JsonSerializer.Serialize(new { messageId = id }, Json),
                        CorrelationId = correlation,
                    }
                );
                return a;
            },
            ct
        );

    // ===== Execute：写操作的统一模板方法（幂等 + 事务 + 并发异常翻译）=====
    // 泛型方法 <T>：payload 的类型参数（≈ Java 泛型方法）；Func<Task<Appointment>> = 返回 Task 的委托
    // （≈ Java 的 Supplier<CompletionStage<Appointment>>，委托是 C# 的"函数类型"）。
    async Task<Appointment> Execute<T>(
        string actor,
        string operation,
        string key,
        T payload,
        Func<Task<Appointment>> action,
        CancellationToken ct
    )
    {
        // string.IsNullOrWhiteSpace ≈ StringUtils.isBlank。
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new BusinessException("invalid_key", "请提供1–128字符的 Idempotency-Key", 400);
        // 幂等键 = hash(actor + operation + 客户端 key)；指纹 = hash(规范化后的请求体)。
        var id = Hash(JsonSerializer.Serialize(new[] { actor, operation, key }));
        var fingerprint = Hash(JsonSerializer.Serialize(payload, Json));
        // await using ≈ try-with-resources 的异步版：离开作用域自动 DisposeAsync（未提交则回滚）。
        // IsolationLevel.ReadCommitted ≈ Connection.TRANSACTION_READ_COMMITTED。
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            ct
        );
        try
        {
            // INSERT obtains the unique-key lock even for a concurrent duplicate. The row and result commit together.
            // ExecuteSqlInterpolatedAsync：执行非查询 SQL，内插参数同样自动参数化。
            // ON DUPLICATE KEY UPDATE Id=Id：MySQL 幂等插入——并发的相同 key 在此拿唯一键锁排队。
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Idempotency (Id,Fingerprint,CreatedUtc) VALUES ({id},{fingerprint},{DateTime.UtcNow}) ON DUPLICATE KEY UPDATE Id=Id",
                ct
            );
            // FOR UPDATE 锁住幂等记录行：同一 key 的并发重试串行化。
            var record = (
                await db
                    .Idempotency.FromSqlInterpolated(
                        $"SELECT * FROM Idempotency WHERE Id={id} FOR UPDATE"
                    )
                    .ToListAsync(ct)
            ).Single();
            if (record.Fingerprint != fingerprint)
                throw new BusinessException(
                    "idempotency_conflict",
                    "同一请求标识已用于不同内容，请重新发起操作"
                );
            // 已有成功响应 → 提交事务后直接回放（重试返回首次的结果，不重复执行业务）。
            if (record.Response is not null)
            {
                await tx.CommitAsync(ct);
                // JsonSerializer.Deserialize<T>(json) ≈ objectMapper.readValue(json, T.class)；末尾 ! 空断言。
                return JsonSerializer.Deserialize<Appointment>(record.Response, Json)!;
            }
            var result = await action(); // 执行业务动作（Create/Mutate 传入的 lambda）
            record.Response = JsonSerializer.Serialize(result, Json);
            // SaveChangesAsync = flush；CommitAsync 才真正提交。两次显式分离是显式事务编排的核心。
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        // catch ... when：异常过滤器——只捕获满足条件的异常，且模式匹配直接检查异常属性。
        // { Number: 1213 or 1205 or 1062 } = MySQL 死锁/锁等待超时/唯一键冲突；
        // or 是模式内的逻辑或；InnerException ≈ Java getCause()。
        catch (Exception ex)
            when (ex is DbUpdateConcurrencyException
                || ex is MySqlException { Number: 1213 or 1205 or 1062 }
                || ex.InnerException is MySqlException { Number: 1213 or 1205 or 1062 }
            )
        {
            // 数据库层并发信号统一翻译成业务 409（≈ Spring DataAccessException 翻译，这里显式做）。
            await tx.RollbackAsync(ct);
            throw new BusinessException(
                "concurrent_conflict",
                "资源正在被其他请求修改，请刷新后重试"
            );
        }
    }

    // 按升序逐个 FOR UPDATE 锁资源行；固定顺序是多资源加锁防死锁的经典手法。
    async Task LockResources(IEnumerable<int> ids, CancellationToken ct)
    {
        // Distinct().Order()：去重 + 升序（Order 是 .NET 7+ 的简写，≈ OrderBy(x => x)）。
        foreach (var id in ids.Distinct().Order())
            if (
                (
                    await db
                        .Resources.FromSqlInterpolated(
                            $"SELECT * FROM Resources WHERE Id={id} FOR UPDATE"
                        )
                        .ToListAsync(ct)
                ).Count == 0
            )
                throw new BusinessException("resource_missing", "资源不存在", 404);
    }

    async Task CheckFree(int resource, DateTime[] slots, string? ownId, CancellationToken ct)
    {
        if (
            await db.SlotClaims.AnyAsync(
                x =>
                    x.ResourceId == resource
                    // slots.Contains(...)：内存集合的 Contains 被翻译成 SQL IN (...)——LINQ 表达式树的威力。
                    && slots.Contains(x.SlotStartUtc)
                    && x.AppointmentId != ownId,
                ct
            )
        )
            throw new BusinessException("slot_conflict", "该资源时段已被占用，请选择其他时间");
    }

    // void + 表达式体：把每个槽位映射成 SlotClaim 实体并批量标记新增。
    void Claim(Appointment a, DateTime[] slots) =>
        db.SlotClaims.AddRange(
            slots.Select(s => new SlotClaim
            {
                ResourceId = a.ResourceId,
                SlotStartUtc = s,
                AppointmentId = a.Id,
            })
        );

    // 默认参数 object? detail = null：调用方可省略（≈ Java 重载方法的简写）。
    void Record(
        Appointment a,
        string action,
        string actor,
        string correlation,
        object? detail = null
    )
    {
        var snapshot = JsonSerializer.Serialize(a, Json);
        // 审计与发件箱消息与业务写入同一个 DbContext → 同一个事务落库（事务性发件箱的关键）。
        db.Audits.Add(
            new AuditEntry
            {
                AppointmentId = a.Id,
                Action = action,
                Actor = actor,
                Version = a.Version,
                Summary = JsonSerializer.Serialize(new { appointment = a, detail }, Json),
                CorrelationId = correlation,
            }
        );
        db.Outbox.Add(
            new OutboxMessage
            {
                AppointmentId = a.Id,
                Version = a.Version,
                Payload = snapshot,
                CorrelationId = correlation,
            }
        );
    }
}

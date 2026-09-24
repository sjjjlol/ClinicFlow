namespace ClinicFlow.Scheduling;

// ===== 数据库实体（可变 class）：EF Core 变更跟踪需要 setter；对照下面 record 型的入站 DTO =====
// 约定映射：类名→表名，Id 属性→主键（无需 @Entity/@Id 注解，OnModelCreating 里再显式覆盖细节）。
public class Appointment
{
    // 属性初始化器：new 时即赋默认值。Guid.NewGuid() ≈ UUID.randomUUID()。
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int PatientId { get; set; }
    public int ResourceId { get; set; }
    // DateTime ≈ LocalDateTime/Instant 的混合体；本项目约定一律存 UTC（配 UtcDateTimeConverter 输出 Z）。
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public int Version { get; set; } = 1; // 乐观锁版本（ClinicDb 中配为 IsConcurrencyToken）
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

// SlotClaim：资源占用的最小粒度（15 分钟槽），复合主键 (ResourceId, SlotStartUtc) 见 ClinicDb。
public class SlotClaim
{
    public int ResourceId { get; set; }
    public DateTime SlotStartUtc { get; set; }
    public string AppointmentId { get; set; } = "";
}

public class PrerequisiteTask
{
    public int Id { get; set; }
    public string AppointmentId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Completed { get; set; }
    // string? / DateTime? ：可空类型。? 修饰引用类型 = 编译器空分析允许 null；
    // ? 修饰值类型 = Nullable<T>（≈ Java Integer，但无装箱）。未完成时这两个字段为 null。
    public string? CompletedBy { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

// IdempotencyRecord：幂等键 → 已执行结果的映射，HTTP 层"至少一次"语义的 Exactly-once 效果来源。
public class IdempotencyRecord
{
    public string Id { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string? Response { get; set; } // 首次成功后的响应快照；重试时直接回放
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class AuditEntry
{
    public long Id { get; set; } // long ≈ Java long；自增主键由数据库生成
    public string AppointmentId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Actor { get; set; } = "";
    public int Version { get; set; }
    public string Summary { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

// OutboxMessage：事务性发件箱模式——与业务写入同事务落库，后台 worker 异步投递（保证不丢消息）。
public class OutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AppointmentId { get; set; } = "";
    public int Version { get; set; }
    public string Payload { get; set; } = "";
    public string Status { get; set; } = "Pending"; // Pending → Processing → Delivered/Failed
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public string? LeaseToken { get; set; } // 租约令牌：防止多实例/超时后重复完成
    public DateTime? LeaseUntilUtc { get; set; }
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = "";
}

// ===== 入站 DTO（record）：不可变值对象，≈ Java 16+ record =====
// 这一行位置参数语法 = 自动生成 4 个 init-only 属性 + 构造器 + 值相等 + ToString。
// DateTimeOffset ≈ OffsetDateTime：入站允许带偏移（前端传上海时间），服务内统一转 UTC。
public record Booking(
    int PatientId,
    int ResourceId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc
);

// 带默认值的 record 参数 = 可选字段；int?/DateTimeOffset? 可空值类型表达"未提供"。
public record Mutation(
    int Version,
    int? ResourceId = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    int? TaskId = null
);

// 自定义业务异常：主构造函数参数 (string code, ...) 直接可用；: Exception(message) 传基类。
// { get; } = 只读属性（≈ final 字段 + getter）；= code 初始化自构造参数。
public class BusinessException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

// static class：全是静态成员、不能实例化（≈ Java 工具类 + 私有构造器）。
public static class Rules
{
    // 表达式以外的常规方法体；返回 DateTime[] 数组。
    public static DateTime[] Slots(DateTime start, DateTime end)
    {
        // Ticks：100 纳秒为单位的时间计数；TimeSpan.FromMinutes(15) ≈ Duration.ofMinutes(15)。
        if (
            start.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
            || end.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
            || end <= start
            || end - start > TimeSpan.FromHours(4)
        )
            throw new BusinessException(
                "invalid_time",
                "起止时间必须对齐15分钟，时长为15分钟至4小时",
                400
            );
        // Enumerable.Range/Select/ToArray：内存集合上的 LINQ（≈ IntStream.range().mapToObj().toArray()）。
        return Enumerable
            .Range(0, (int)(end - start).TotalMinutes / 15)
            .Select(i => start.AddMinutes(i * 15))
            .ToArray();
    }
}

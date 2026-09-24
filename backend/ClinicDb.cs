using ClinicFlow.Integration;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

// 文件作用域 namespace：顶格声明、全文件生效，免一层缩进（C# 10+；≈ Java package）。
namespace ClinicFlow;

// DbContext ≈ JPA 的 EntityManager + Unit of Work（工作单元）。
// 类名后的 (DbContextOptions<ClinicDb> options) 是 C# 12 主构造函数：
// 参数直接写在类名后，类体内可用；: DbContext(options) 表示传给基类构造器。
public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    // DbSet<T> ≈ 一张表的入口（≈ JPA 的 EntityManager.createQuery 的强类型版）。
    // => Set<Appointment>() 是表达式体只读属性：每次访问都调用一次 Set<T>()，不是缓存字段。
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<SlotClaim> SlotClaims => Set<SlotClaim>();
    public DbSet<PrerequisiteTask> Tasks => Set<PrerequisiteTask>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<AuditEntry> Audits => Set<AuditEntry>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<DemoUser> Users => Set<DemoUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Resource> Resources => Set<Resource>();

    // OnModelCreating：Fluent API 映射配置（对照 JPA 的 @Entity/@Column/@Index 注解体系，
    // 但用链式代码表达，重构友好、可条件化）。override = Java 的 @Override（C# 必须显式写）。
    protected override void OnModelCreating(ModelBuilder b)
    {
        // Property(...).HasMaxLength(36) ≈ @Column(length = 36)
        b.Entity<SyncAttempt>().Property(x => x.MessageId).HasMaxLength(36);
        b.Entity<SyncAttempt>().Property(x => x.LeaseToken).HasMaxLength(36);
        // HasIndex(...).IsUnique() ≈ @Table(uniqueConstraints = ...)
        b.Entity<SyncAttempt>().HasIndex(x => x.LeaseToken).IsUnique();
        // new { x.MessageId, x.Id } 匿名类型表达复合索引列（≈ @Table(indexes = @Index(columnList=...))）
        b.Entity<SyncAttempt>().HasIndex(x => new { x.MessageId, x.Id });
        b.Entity<Appointment>().Property(x => x.Id).HasMaxLength(36);
        // IsConcurrencyToken ≈ JPA @Version 乐观锁（UPDATE 时带旧值条件，失配抛 DbUpdateConcurrencyException）
        b.Entity<Appointment>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Appointment>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Appointment>().HasIndex(x => new { x.StartUtc, x.Id });
        b.Entity<Appointment>().HasIndex(x => x.ResourceId); // retain FK-supporting index for additive MySQL migration
        b.Entity<Appointment>()
            .HasIndex(x => new
            {
                x.ResourceId,
                x.StartUtc,
                x.Id,
            });
        // HasOne<Patient>().WithMany().HasForeignKey(...) ≈ @ManyToOne + @JoinColumn；
        // DeleteBehavior.Restrict ≈ 禁止级联删除（数据库层面有预约的患者不可删）。
        b.Entity<Appointment>()
            .HasOne<Patient>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Appointment>()
            .HasOne<Resource>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);
        // 复合主键（≈ JPA @IdClass / @EmbeddedId）：资源 + 槽位起始时间唯一。
        b.Entity<SlotClaim>().HasKey(x => new { x.ResourceId, x.SlotStartUtc });
        b.Entity<SlotClaim>()
            .HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SlotClaim>()
            .HasOne<Resource>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PrerequisiteTask>()
            .HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdempotencyRecord>().Property(x => x.Id).HasMaxLength(64);
        b.Entity<IdempotencyRecord>().Property(x => x.Fingerprint).HasMaxLength(64);
        b.Entity<AuditEntry>().HasIndex(x => new { x.AppointmentId, x.Id });
        b.Entity<OutboxMessage>().Property(x => x.Id).HasMaxLength(36);
        b.Entity<OutboxMessage>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.NextAttemptUtc });
        // HasData：种子数据（≈ Flyway 的 seed migration），随 EF Migrations 写入数据库。
        b.Entity<Patient>()
            .HasData(
                new Patient
                {
                    Id = 1,
                    Name = "林晓（模拟）",
                    Identifier = "DEMO-001",
                },
                new Patient
                {
                    Id = 2,
                    Name = "陈晨（模拟）",
                    Identifier = "DEMO-002",
                }
            );
        b.Entity<Resource>()
            .HasData(
                new Resource
                {
                    Id = 1,
                    Name = "预约室 A",
                    Kind = "Consultation",
                },
                new Resource
                {
                    Id = 2,
                    Name = "预约室 B",
                    Kind = "Consultation",
                }
            );
    }
}

// ---- 实体类：普通可变 class（EF 变更跟踪需要 setter）；对照 record DTO 见 Scheduling/Models.cs ----
// { get; set; } 是 C# 自动属性：编译器生成隐藏字段 + get/set 方法，取代 Java 的 getter/setter 样板。
// = "" 是属性初始化器：new 对象时默认空串而非 null。
public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Identifier { get; set; } = "";
}

public class Resource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
}

public class DemoUser
{
    public string Id { get; set; } = "";
    public string Role { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

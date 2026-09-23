using ClinicFlow.Integration;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
namespace ClinicFlow;
public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<SlotClaim> SlotClaims => Set<SlotClaim>();
    public DbSet<PrerequisiteTask> Tasks => Set<PrerequisiteTask>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<AuditEntry> Audits => Set<AuditEntry>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<DemoUser> Users => Set<DemoUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Resource> Resources => Set<Resource>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SyncAttempt>().Property(x => x.MessageId).HasMaxLength(36);
        b.Entity<SyncAttempt>().Property(x => x.LeaseToken).HasMaxLength(36);
        b.Entity<SyncAttempt>().HasIndex(x => x.LeaseToken).IsUnique();
        b.Entity<SyncAttempt>().HasIndex(x => new { x.MessageId, x.Id });
        b.Entity<Appointment>().Property(x => x.Id).HasMaxLength(36);
        b.Entity<Appointment>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Appointment>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Appointment>().HasIndex(x => new { x.StartUtc, x.Id });
        b.Entity<Appointment>().HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Appointment>().HasOne<Resource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SlotClaim>().HasKey(x => new { x.ResourceId, x.SlotStartUtc });
        b.Entity<SlotClaim>().HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SlotClaim>().HasOne<Resource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PrerequisiteTask>().HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IdempotencyRecord>().Property(x => x.Id).HasMaxLength(64);
        b.Entity<IdempotencyRecord>().Property(x => x.Fingerprint).HasMaxLength(64);
        b.Entity<AuditEntry>().HasIndex(x => new { x.AppointmentId, x.Id });
        b.Entity<OutboxMessage>().Property(x => x.Id).HasMaxLength(36);
        b.Entity<OutboxMessage>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.NextAttemptUtc });
        b.Entity<Patient>().HasData(new Patient { Id = 1, Name = "林晓（模拟）", Identifier = "DEMO-001" }, new Patient { Id = 2, Name = "陈晨（模拟）", Identifier = "DEMO-002" });
        b.Entity<Resource>().HasData(new Resource { Id = 1, Name = "预约室 A", Kind = "Consultation" }, new Resource { Id = 2, Name = "预约室 B", Kind = "Consultation" });
    }
}
public class Patient { public int Id { get; set; } public string Name { get; set; } = ""; public string Identifier { get; set; } = ""; }
public class Resource { public int Id { get; set; } public string Name { get; set; } = ""; public string Kind { get; set; } = ""; }

public class DemoUser { public string Id { get; set; } = ""; public string Role { get; set; } = ""; public string PasswordHash { get; set; } = ""; }

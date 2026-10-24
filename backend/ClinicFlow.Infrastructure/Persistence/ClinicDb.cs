using System;
using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow
{
    public class ClinicDb : DbContext
    {
        public ClinicDb(DbContextOptions<ClinicDb> options) : base(options)
        {
        }

        public DbSet<DemoUser> Users => Set<DemoUser>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            OisModel.Configure(b);
            b.Entity<OutboxMessage>().HasKey(x => x.Id);
            b.Entity<OutboxMessage>().Property(x => x.Id).HasMaxLength(36);
            b.Entity<OutboxMessage>().Property(x => x.Type).HasMaxLength(128);
            b.Entity<OutboxMessage>().Property(x => x.LastError).HasMaxLength(256);
            b.Entity<OutboxMessage>().HasIndex(x => new { x.PublishedUtc, x.NextAttemptUtc });
            b.Entity<IdempotencyRecord>().Property(x => x.Id).HasMaxLength(64);
            b.Entity<IdempotencyRecord>().Property(x => x.Fingerprint).HasMaxLength(64);
            b.Entity<Patient>().HasData(
                Patient.Register(1, "林晓（模拟）", "DEMO-001"),
                Patient.Register(2, "陈晨（模拟）", "DEMO-002")
            );
        }
    }

    public class DemoUser
    {
        public string Id { get; set; } = "";
        public string Role { get; set; } = "";
        public string PasswordHash { get; set; } = "";
    }

    public class IdempotencyRecord
    {
        public string Id { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string? Response { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}

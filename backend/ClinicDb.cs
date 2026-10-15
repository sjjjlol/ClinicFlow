using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow;

public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    public DbSet<DemoUser> Users => Set<DemoUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        OisModel.Configure(b);
        b.Entity<IdempotencyRecord>().Property(x => x.Id).HasMaxLength(64);
        b.Entity<IdempotencyRecord>().Property(x => x.Fingerprint).HasMaxLength(64);
        b.Entity<Patient>().HasData(
            new Patient { Id = 1, Name = "林晓（模拟）", Identifier = "DEMO-001" },
            new Patient { Id = 2, Name = "陈晨（模拟）", Identifier = "DEMO-002" }
        );
    }
}

public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Identifier { get; set; } = "";
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

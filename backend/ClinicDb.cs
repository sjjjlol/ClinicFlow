using Microsoft.EntityFrameworkCore;
namespace ClinicFlow;
public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Resource> Resources => Set<Resource>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Patient>().HasData(new Patient { Id = 1, Name = "林晓（模拟）", Identifier = "DEMO-001" }, new Patient { Id = 2, Name = "陈晨（模拟）", Identifier = "DEMO-002" });
        b.Entity<Resource>().HasData(new Resource { Id = 1, Name = "预约室 A", Kind = "Consultation" }, new Resource { Id = 2, Name = "预约室 B", Kind = "Consultation" });
    }
}
public class Patient { public int Id { get; set; } public string Name { get; set; } = ""; public string Identifier { get; set; } = ""; }
public class Resource { public int Id { get; set; } public string Name { get; set; } = ""; public string Kind { get; set; } = ""; }

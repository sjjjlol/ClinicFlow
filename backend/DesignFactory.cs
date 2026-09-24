using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClinicFlow;

public class DesignFactory : IDesignTimeDbContextFactory<ClinicDb>
{
    public ClinicDb CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<ClinicDb>()
                .UseMySql(
                    "Server=localhost;Database=clinicflow;User=design_only",
                    new MySqlServerVersion(new Version(8, 4, 8))
                )
                .Options
        );
}

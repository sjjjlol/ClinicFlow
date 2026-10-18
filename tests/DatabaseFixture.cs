using System;
using System.Threading.Tasks;
using ClinicFlow;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClinicFlow.Tests
{
    public class DatabaseFixture
    {
        readonly string connection = Environment.GetEnvironmentVariable("TEST_DB")
            ?? throw new Exception("TEST_DB must name an isolated *_tests database");
        public ClinicDb Db() => new ClinicDb(new DbContextOptionsBuilder<ClinicDb>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8))).Options);
        public async Task InitializeAsync()
        {
            if (!new MySqlConnectionStringBuilder(connection).Database.EndsWith("_tests"))
                throw new Exception("Refusing non-test database");
            await using var db = Db();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }
}

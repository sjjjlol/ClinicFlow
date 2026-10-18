using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicFlow.Hosting
{
    public static class DatabaseInitialization
    {
        // Preserve startup migration and seed semantics; deployment migration policy is separate.
        public static async Task InitializeClinicDatabaseAsync(this WebApplication app)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
            await db.Database.MigrateAsync();
            await Identity.Seed(db, app.Configuration);
        }
    }
}

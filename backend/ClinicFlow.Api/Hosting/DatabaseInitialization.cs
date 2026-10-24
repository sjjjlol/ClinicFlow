using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicFlow.Hosting
{
    public static class DatabaseInitialization
    {
        // Preserve startup migration and seed semantics; deployment migration policy is separate.
        public static Task InitializeClinicDatabaseAsync(this WebApplication app) =>
            app.Services.InitializeClinicInfrastructure(app.Configuration);
    }
}

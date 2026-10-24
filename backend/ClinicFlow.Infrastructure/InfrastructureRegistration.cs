using System;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace ClinicFlow
{
    public static class InfrastructureRegistration
    {
        public static IServiceCollection AddClinicInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ClinicDb>(options => options.UseMySql(
                configuration.GetConnectionString("Clinic") ?? throw new InvalidOperationException("ConnectionStrings__Clinic required"),
                new MySqlServerVersion(new Version(8, 4, 8))));
            services.AddScoped<ICommandTransaction, EfCommandTransaction>();
            services.AddScoped<IWorkspaceReader, EfWorkspaceReader>();
            services.AddScoped<IStaffAuthenticator, StaffAuthenticator>();
            services.AddScoped<IDatabaseHealth, DatabaseHealth>();
            return services;
        }
        // Opt in only after providing a real transport; the host must schedule DispatchBatch.
        public static IServiceCollection AddClinicOutboxDelivery<TPublisher>(this IServiceCollection services)
            where TPublisher : class, ClinicFlow.Application.Integration.IIntegrationEventPublisher
        {
            services.AddScoped<ClinicFlow.Application.Integration.IIntegrationEventPublisher, TPublisher>();
            services.AddScoped<OutboxDispatcher>();
            return services;
        }
        public static async Task InitializeClinicInfrastructure(this IServiceProvider provider, IConfiguration configuration)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
            await db.Database.MigrateAsync();
            await StaffAccounts.Seed(db, configuration);
        }
    }
    internal sealed class DatabaseHealth : IDatabaseHealth
    {
        private readonly ClinicDb db;
        public DatabaseHealth(ClinicDb db) => this.db = db;
        public Task<bool> CanConnect(CancellationToken ct) => db.Database.CanConnectAsync(ct);
    }
}

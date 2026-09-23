using ClinicFlow;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ClinicDb>(o => o.UseMySql(builder.Configuration.GetConnectionString("Clinic") ?? throw new InvalidOperationException("ConnectionStrings__Clinic required"), new MySqlServerVersion(new Version(8,4,8))));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<ClinicDb>().Database.MigrateAsync();
app.MapGet("/api/health", async (ClinicDb db) => new { status = await db.Database.CanConnectAsync() ? "ready" : "unavailable" });
app.Run();
public partial class Program;

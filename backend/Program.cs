using ClinicFlow;
using ClinicFlow.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddClinicServices(builder.Configuration, builder.Environment);

await using (WebApplication app = builder.Build())
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        ClinicDb db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
        await db.Database.MigrateAsync();
        await Identity.Seed(db, app.Configuration);
    }

    app.UseClinicPipeline();
    await app.RunAsync();
}

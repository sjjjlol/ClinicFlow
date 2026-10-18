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

// 为集成测试暴露入口类型，启动代码由顶层语句生成。
public partial class Program
{
}

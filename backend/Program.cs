using ClinicFlow;
using Microsoft.EntityFrameworkCore;

// 显式 Main 和 Startup：入口负责启动，服务注册和请求管道由 Startup 管理。
public partial class Program
{
    public static async Task Main(string[] args)
    {
        using (IHost host = CreateHostBuilder(args).Build())
        {
            using (IServiceScope scope = host.Services.CreateScope())
            {
                ClinicDb db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
                await db.Database.MigrateAsync();
                await Identity.Seed(db, scope.ServiceProvider.GetRequiredService<IConfiguration>());
            }

            await host.RunAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClinicFlow;

// ===== EF Core 设计时工厂：给 dotnet-ef CLI 工具用的 DbContext 构造入口 =====
// dotnet ef migrations add 等命令不执行 Program.cs 的完整启动（没有环境变量/配置），
// 工具通过 IDesignTimeDbContextFactory<T> 拿到一个能用的 DbContext 来读模型、比对快照。
// 这里的连接串只用于设计时读数据库结构（Never 用于运行时）。
public class DesignFactory : IDesignTimeDbContextFactory<ClinicDb>
{
    public ClinicDb CreateDbContext(string[] args) =>
        // DbContextOptionsBuilder：流式构造 DbContext 配置（new(...) 目标类型 new：ClinicDb 由返回类型推出）。
        new(
            new DbContextOptionsBuilder<ClinicDb>()
                .UseMySql(
                    "Server=localhost;Database=clinicflow;User=design_only",
                    new MySqlServerVersion(new Version(8, 4, 8))
                )
                .Options
        );
}

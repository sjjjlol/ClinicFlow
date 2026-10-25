# EF Core 实战：从接入 MySQL 到完成 CRUD

这是 [ASP.NET Core 入门](aspnet-core-learning.md) 的数据库实战篇，面向有 JPA / Hibernate 或 MyBatis 经验的 Java 开发者。按真实开发顺序，先接数据库，再定义模型和映射、生成迁移、实现查询与写入，最后处理事务、并发和排障。

示例使用与 ClinicFlow 一致的 .NET 10、EF Core 9.0.20、Pomelo 9.0.0 和 MySQL 8.4.8。示例中的 LearningPatient / LearningVisit 是独立教学模型，**不修改 ClinicFlow 的 Patients 表，也不代表它当前已有这些字段或接口**。完整示例只绑定本机回环地址，使用虚构数据；接入实际业务时，按照主文档补齐认证、授权、审计和幂等。

## 阅读顺序

1. 第 1～3 节：认识组件，理解独立示例工程与数据库的组成。
2. 第 4～6 节：阅读模型、连接注册和 CRUD 源码，理解关键配置。
3. 第 7 节：理解迁移生成与数据库升级的关系。
4. 第 8 节：通过请求示例理解增删改查、唯一约束和版本冲突。
5. 第 9～13 节：学习关联查询、事务、批量操作、诊断和项目实际实现。

## 1. EF Core 到底负责什么

```text
端点 / 应用服务
  → DbContext：模型、跟踪与工作单位
  → LINQ 查询 / 实体状态变更
  → EF Core 关系数据库能力
  → Pomelo MySQL Provider：SQL 翻译与数据库特性
  → MySqlConnector：连接、参数与命令执行
  → MySQL
```

| Java 概念 | EF Core 概念 | 区别与边界 |
| --- | --- | --- |
| EntityManager / Persistence Context | DbContext / ChangeTracker | 通常按请求作用域创建，不能跨线程并发使用 |
| JPQL / Criteria / Repository 查询 | LINQ to Entities | IQueryable 表达式由 Provider 翻译成 SQL |
| `persist` / `remove` / flush | `Add` / `Remove` / `SaveChangesAsync` | 标记状态与发 SQL 是不同时间点 |
| `@Entity`、`@Column` 等 | 约定、Data Annotations、Fluent API | Fluent API 可集中配置并覆盖约定和注解 |
| JDBC 驱动、数据库方言 | MySqlConnector、Pomelo Provider | Provider 不只是改几个 SQL 关键字，还提供类型映射和迁移等能力 |
| Flyway / Liquibase | EF Migrations | 从模型与模型快照差异生成迁移，再记录已应用历史 |
| `@Version` | `IsConcurrencyToken()` | 普通整数令牌不会因此自动加一，需要应用维护 |

DbContext 已经提供工作单位和实体集合访问能力。可以按业务需要封装查询服务或 Repository，但不必机械地为每张表再复制一套 Add / Update / Delete 接口。

## 2. 建立学习工程与数据库

以下命令在**仓库外的空目录**执行，要求本机有 .NET 10 SDK、Docker 和 curl。不要在现有 backend 目录执行 `dotnet new`。如果示例端口或容器名已占用，统一改成其他值。

### 2.1 建项目和安装包

```sh
mkdir EfLearning
cd EfLearning
dotnet new web -n Learning.Api -f net10.0
cd Learning.Api

dotnet add package Pomelo.EntityFrameworkCore.MySql --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Relational --version 9.0.20
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.20

dotnet new tool-manifest --output .config
dotnet tool install dotnet-ef --version 9.0.20 --allow-roll-forward
dotnet ef --version
```

| 包 / 工具 | 用处 | 是否必须显式安装 |
| --- | --- | --- |
| Pomelo.EntityFrameworkCore.MySql | 接入 MySQL / MariaDB、提供 UseMySql | MySQL 示例需要 |
| Microsoft.EntityFrameworkCore.Relational | 关系数据库通用能力 | Provider 会传递引用；这里显式固定补丁版本，与主项目一致 |
| Microsoft.EntityFrameworkCore.Design | 生成迁移等设计时能力 | 使用 EF CLI 时需要 |
| dotnet-ef | `dotnet ef` 命令行工具 | 独立于 NuGet 项目依赖；不能只装 Design 包 |

在 `.csproj` 的 Design 包引用中增加 `<PrivateAssets>all</PrivateAssets>`，表示该依赖不向引用此项目的其他项目传递。这不等于编译器不需要它，也不应简单等同于 Maven provided。

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.20">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

`--allow-roll-forward` 允许工具在缺少其目标运行时时使用较新的 .NET 运行时，适合只安装 .NET 10 的本教程环境；它不会升级项目的 EF Core NuGet 版本。[官方工具参数说明](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install)

Local tool manifest 将工具版本记录在 `.config/dotnet-tools.json`。其他开发者取得工程后执行 `dotnet tool restore`，不需要各自猜测全局工具版本。这是新学习工程的目录约定；ClinicFlow 已在仓库根目录使用 `dotnet-tools.json`，在根目录执行 `./scripts/dotnet.sh tool restore` 即可还原其固定工具版本，不必重复创建清单。

Pomelo 9.0.0 对应 EF Core 9.0.x，支持 .NET 8 及以上。SDK 10 并不要求 EF Core 必须使用 10；优先遵循 Provider 的兼容矩阵。[Pomelo 官方兼容说明](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql#compatibility)

### 2.2 启动独立 MySQL

```sh
docker run -d --rm --name clinicflow-ef-learning \
  -p 127.0.0.1:3310:3306 \
  -e MYSQL_RANDOM_ROOT_PASSWORD=yes \
  -e MYSQL_DATABASE=ef_learning \
  -e MYSQL_USER=learning \
  -e MYSQL_PASSWORD=learning-local-only \
  mysql:8.4.8
```

这是临时学习容器。这里的密码是公开的本机示例值，不要用于实际系统。容器没有挂持久化数据卷，停止后会删除容器中的示例数据库。

数据库初始化需要时间；下面命令成功返回 `1` 后再继续。未就绪时稍后重试。

```sh
docker exec -e MYSQL_PWD=learning-local-only clinicflow-ef-learning \
  mysql --protocol=TCP -h127.0.0.1 -ulearning -Def_learning -e 'SELECT 1'
```

在运行 .NET 命令的终端设置连接串：

```sh
export ConnectionStrings__Learning='Server=127.0.0.1;Port=3310;Database=ef_learning;User=learning;Password=learning-local-only'
```

连接串中的 Server 是宿主机连接容器的地址；如果应用也放进同一个 Docker 网络，通常改为数据库服务名和容器内部端口，不能继续假设 localhost 指向数据库。

## 3. 先分清三层配置

| 配置层 | 写在哪里 | 解决什么问题 |
| --- | --- | --- |
| 连接与 Provider | AddClinicServices 的 AddDbContext / UseMySql | 连哪台数据库，按哪种数据库版本生成 SQL |
| 实体映射 | DbContext.OnModelCreating 或 IEntityTypeConfiguration | 表、列、主键、外键、索引、删除行为、并发令牌 |
| 业务输入规则 | DTO 校验、Service / Domain | 名称是否空白、谁能更新、当前状态能否操作 |

`HasMaxLength(100)` 不会帮你自动返回 HTTP 400；`IsRequired()` 不禁止空字符串；修改映射也不会立刻修改已有数据库。它们分别需要输入校验和数据库迁移配合。

## 4. 定义实体与 DbContext

在 Learning.Api 中新建 `Models.cs`，内容如下：

```csharp
using System;
using System.Collections.Generic;
namespace Learning.Api
{
    public class LearningPatient
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Identifier { get; set; } = "";
        public string? Note { get; set; }
        public int Version { get; set; } = 1;
        public List<LearningVisit> Visits { get; set; } = new List<LearningVisit>();
    }

    public class LearningVisit
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public LearningPatient Patient { get; set; } = null!;
        public DateTime StartsUtc { get; set; }
        public decimal Fee { get; set; }
    }

    public class CreatePatientRequest
    {
        public string? Name { get; set; }
        public string? Identifier { get; set; }
        public string? Note { get; set; }
    }

    public class UpdatePatientRequest
    {
        public string? Name { get; set; }
        public string? Note { get; set; }
        public int Version { get; set; }
    }

    public class PatientResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Identifier { get; set; } = "";
        public string? Note { get; set; }
        public int Version { get; set; }

        public PatientResponse(int id, string name, string identifier, string? note, int version)
        {
            Id = id;
            Name = name;
            Identifier = identifier;
            Note = note;
            Version = version;
        }
    }
}
```

Patient 是一端，Visit 是多端；PatientId 是真实外键字段，Visits / Patient 是对象导航属性。导航属性不等于数据库列，不会因为属性存在就自动加载关联数据。`null!` 只是压制编译器警告，未加载导航时不能假定它已有值。

DTO 独立于实体：创建请求不能设置 Id 或 Version，更新请求也不能任意改 Identifier 或关联集合。这样可以避免直接把请求 JSON 反序列化为实体后，意外修改不该由客户端控制的字段。

新建 `LearningDb.cs`：

```csharp
using Microsoft.EntityFrameworkCore;

namespace Learning.Api
{
    public class LearningDb : DbContext
    {
        public LearningDb(DbContextOptions<LearningDb> options) : base(options)
        {
        }

        public DbSet<LearningPatient> Patients => Set<LearningPatient>();
        public DbSet<LearningVisit> Visits => Set<LearningVisit>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var patient = modelBuilder.Entity<LearningPatient>();
            patient.ToTable("learning_patients");
            patient.HasKey(p => p.Id);
            patient.Property(p => p.Id).ValueGeneratedOnAdd();
            patient.Property(p => p.Name).HasMaxLength(100).IsRequired();
            patient.Property(p => p.Identifier).HasMaxLength(40).IsRequired();
            patient.Property(p => p.Note).HasMaxLength(500).IsRequired(false);
            patient.Property(p => p.Version).IsConcurrencyToken();
            patient.HasIndex(p => p.Identifier).IsUnique();

            var visit = modelBuilder.Entity<LearningVisit>();
            visit.ToTable("learning_visits");
            visit.HasKey(v => v.Id);
            visit.Property(v => v.StartsUtc).HasColumnType("datetime(6)");
            visit.Property(v => v.Fee).HasPrecision(10, 2);
            visit.HasIndex(v => new { v.PatientId, v.StartsUtc });
            visit.HasOne(v => v.Patient)
                .WithMany(p => p.Visits)
                .HasForeignKey(v => v.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

### 4.1 每项映射在解决什么

| 配置 | 数据库效果 / 语义 | 常见误解 |
| --- | --- | --- |
| `ToTable("learning_patients")` | 明确表名 | 不依赖类名/DbSet 命名推断，避免无意改名 |
| `HasKey(p => p.Id)` | 指定主键 | Id 也是约定主键名；显式配置更便于教学 |
| `ValueGeneratedOnAdd()` | 新增时生成键值；本例整型由 MySQL 自增 | 字符串主键不一定由数据库自动生成 |
| `HasMaxLength(100)` | 提供列长度元数据 | 不负责请求层字符串长度校验 |
| `IsRequired()` | 非空列 | 非空不等于非空白 |
| `IsRequired(false)` | 允许 SQL NULL | 空字符串与 NULL 仍是两种值 |
| `HasPrecision(10, 2)` | 本例费用映射为相应 decimal 精度 | 10 是总位数，2 是小数位数；不是小数位 10 位 |
| `HasColumnType("datetime(6)")` | 显式使用 MySQL 时间类型及微秒精度 | 列类型不保存 DateTime.Kind，也不负责时区转换 |
| `HasIndex(...).IsUnique()` | 用数据库保证标识唯一 | 写前查重仍可能并发冲突，不能替代唯一索引 |
| `IsConcurrencyToken()` | UPDATE / DELETE 条件包含原版本 | 不自动生成或递增普通 int Version |
| `HasForeignKey(...)` | 明确依赖端外键 | 不是只在 C# 内存中维持关联 |
| `OnDelete(Restrict)` | 有关联记录时拒绝直接删父记录 | 不会自动实现软删除 |

Fluent API 可以配置精度和列类型等 Provider 相关细节；C# 可空分析也会影响模型约定。开发时要审查生成的迁移，而不是只看实体编译是否成功。[官方属性映射说明](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties)

### 4.2 关系、删除行为与索引设计

本例 `HasOne(v => v.Patient).WithMany(p => p.Visits)` 表示多条 Visit 属于一位 Patient。PatientId 非可空意味着每条 Visit 必须指向已有患者；它不要求每个患者都必须有 Visit。

`Cascade` 会按配置级联删除子记录；`Restrict` 保留约束并阻止有依赖记录的删除；`SetNull` 要求外键可空。临床历史不应因为删除患者而被顺手级联删除，因此本例和 ClinicFlow 的关联采用 Restrict。具体能否删除还需要业务层决定。[官方一对多关系说明](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many)

联合索引 `(PatientId, StartsUtc)` 对“某患者按时间查询记录”更匹配；它不等于两个独立索引，也不自动保证任意两条预约时间段不重叠。MySQL 的字符串比较受字符集、排序规则影响：大小写、重音是否视为相同，应与业务标识规则一起设计并验证。

### 4.3 模型变大以后如何拆配置

可将每个实体的映射移到 `IEntityTypeConfiguration<T>`，然后在 OnModelCreating 中注册：

```csharp
using Microsoft.EntityFrameworkCore;

// 组织方式示意：不是要把它与上面的重复映射一起添加
public class PatientConfiguration : IEntityTypeConfiguration<LearningPatient>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<LearningPatient> b)
    {
        b.ToTable("learning_patients");
        b.HasKey(p => p.Id);
        // 将该实体的其余映射移到这里
    }
}
// OnModelCreating 中：
// modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDb).Assembly);
```

配置只保留一份。不要在多个地方对同一属性写相互冲突的设置，然后依靠调用顺序碰运气。ClinicFlow 当前使用 `OisModel.Configure` 组织映射，理解职责后再考虑是否需要拆文件。

### 4.4 默认值、转换与全局过滤也要区分层次

| 配置或写法 | 实际语义 | 使用时机 |
| --- | --- | --- |
| `public int Version { get; set; } = 1` | C# 创建对象时初始化 | 不会为其他 SQL 客户端插入的数据自动补值 |
| `Property(...).HasDefaultValue(1)` | 配置数据库默认值并进入迁移 | 希望省略该列的 INSERT 也获得默认值时 |
| `Property(...).HasDefaultValueSql(...)` | 使用数据库表达式作为默认值 | 函数、表达式语法和生成策略依赖 Provider；需审查实际 DDL |
| `Property(...).HasConversion<string>()` | 例如将枚举以字符串持久化 | 需要先有对应枚举属性；重命名枚举成员时考虑历史值兼容 |
| `Ignore(...)` / `[NotMapped]` | 排除不应持久化的属性 | 临时计算结果或展示字段 |
| `HasQueryFilter(...)` | 默认给实体查询附加过滤条件 | 软删除、租户过滤；需要在模型中定义相关字段和上下文 |

这些是扩展配置能力，完整 CRUD 示例没有全部启用。数据库默认值只在相应 INSERT 情况下生效，不会每次 UPDATE 自动刷新；最后更新时间通常需要应用逻辑、拦截器或数据库机制维护。全局过滤也不是权限系统：原生 SQL、IgnoreQueryFilters、批量操作和跨租户写入仍须单独审查。

Code First 是本篇的路线：先定义模型再生成迁移。如果要接入已有数据库，也可以通过 `dotnet ef dbcontext scaffold` 反向生成实体和上下文；它与 migrations add 的方向相反。反向生成之前确定目标表、命名和连接串来源，后续选择清楚由哪套流程管理结构，不要反复覆盖手写业务代码。

## 5. 注册数据库连接：看懂每个参数

后面的学习工程在 Program.cs 中注册以下代码；ClinicFlow 则将注册封装在 AddClinicServices 中：

```csharp
var connection = builder.Configuration.GetConnectionString("Learning")
    ?? throw new InvalidOperationException("ConnectionStrings__Learning required");

builder.Services.AddDbContext<LearningDb>(options =>
{
    options.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8)), mysql =>
    {
        mysql.CommandTimeout(30);
    });
    if (builder.Environment.IsDevelopment())
        options.EnableDetailedErrors();
});
```

| 配置 | 含义 | 何时需要关注 |
| --- | --- | --- |
| `GetConnectionString("Learning")` | 读取 `ConnectionStrings:Learning` | 键名与环境变量保持一致 |
| `AddDbContext<LearningDb>` | 注册上下文及其 Options；默认 Scoped | 每次 HTTP 请求通常共享同一个上下文 |
| `UseMySql` | 选用 Pomelo Provider | SQL Server / PostgreSQL 需要各自 Provider 与 Use 方法 |
| `MySqlServerVersion` | 指定服务器版本特性 | 要与实际服务器相符；MariaDB 应使用对应版本类型 |
| `ServerVersion.AutoDetect(connection)` | 另一种做法：连接服务器探测版本 | 会引入连接依赖；设计时或启动阶段未就绪可能失败 |
| `CommandTimeout(30)` | 单条数据库命令超时秒数 | 不等于连接建立超时，也不等于整个 HTTP 请求超时 |
| `EnableDetailedErrors()` | 对查询结果读取等提供更详细错误 | 主要供开发诊断，有额外开销 |
| `EnableSensitiveDataLogging()` | 可在日志中包含参数值等敏感内容 | 本例不启用；真实患者信息、口令等不应进入日志 |
| `UseQueryTrackingBehavior(NoTracking)` | 可改变默认查询跟踪模式 | 本例保留默认 TrackAll，纯读查询逐处加 AsNoTracking |
| `EnableRetryOnFailure()` | Provider 执行策略可重试部分瞬态故障 | 本例不启用；启用后要重新设计显式事务与副作用边界 |

DbContext 作用域、DbContext 池和数据库连接池是三个概念。AddDbContext 不等于每请求永久占用一个连接；连接通常在操作时打开、随后归还底层连接池。`AddDbContextPool` 复用上下文实例，需要注意请求专属状态，入门阶段不用为了“性能”盲目开启。

## 6. 完整 CRUD 入口：先跑通，再拆业务服务

将学习工程按下面的 Program 和 Controller 组织。所需模型来自第 4 节。运行时使用 .NET 10，语法上限仍为 C# 9；入口采用顶层语句，API 采用 Controller。该局部持久化例子在 Controller 中直接操作 DbContext；回到 ClinicFlow 时，业务写操作仍应走 CommandExecutor 的事务、规则、审计与幂等边界。

`Program.cs`：

```csharp
using System;
using Learning.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var connection = builder.Configuration.GetConnectionString("Learning")
    ?? throw new InvalidOperationException("ConnectionStrings__Learning required");
builder.Services.AddDbContext<LearningDb>(options =>
{
    options.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8)), mysql =>
    {
        mysql.CommandTimeout(30);
    });
    if (builder.Environment.IsDevelopment())
        options.EnableDetailedErrors();
});

var app = builder.Build();
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (DbUpdateConcurrencyException)
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new { code = "version_conflict" });
    }
    catch (DbUpdateException ex) when (IsMySqlError(ex, 1062))
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new { code = "duplicate_identifier" });
    }
    catch (DbUpdateException ex) when (IsMySqlError(ex, 1451))
    {
        ctx.Response.StatusCode = 409;
        await ctx.Response.WriteAsJsonAsync(new { code = "has_related_visits" });
    }
});
app.MapControllers();
app.Run();

static bool IsMySqlError(DbUpdateException exception, int number)
{
    MySqlException? mysql = exception.InnerException as MySqlException;
    return mysql != null && mysql.Number == number;
}
```

`Controllers/PatientsController.cs`：

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Learning.Api
{
    [ApiController]
    [Route("api/patients")]
    public class PatientsController : ControllerBase
    {
        private readonly LearningDb db;

        public PatientsController(LearningDb db)
        {
            this.db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePatientRequest input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100
                || string.IsNullOrWhiteSpace(input.Identifier) || input.Identifier.Length > 40
                || input.Note?.Length > 500)
                return BadRequest(new { code = "invalid_patient" });

            var patient = new LearningPatient
            {
                Name = input.Name.Trim(),
                Identifier = input.Identifier.Trim(),
                Note = input.Note
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync(ct);
            return Created($"/api/patients/{patient.Id}", ToResponse(patient));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get([FromRoute] int id, CancellationToken ct)
        {
            var patient = await db.Patients.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PatientResponse(p.Id, p.Name, p.Identifier, p.Note, p.Version))
                .SingleOrDefaultAsync(ct);
            if (patient == null)
                return NotFound();
            return Ok(patient);
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct, [FromQuery] string? keyword = null, [FromQuery] int page = 1, [FromQuery] int size = 20)
        {
            if (page < 1 || page > 100000 || size < 1 || size > 100)
                return BadRequest(new { code = "invalid_page" });

            var query = db.Patients.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(p => p.Name.Contains(keyword));
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(p => p.Id)
                .Skip((page - 1) * size).Take(size)
                .Select(p => new PatientResponse(p.Id, p.Name, p.Identifier, p.Note, p.Version))
                .ToListAsync(ct);
            return Ok(new { total, page, size, items });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdatePatientRequest input, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100
                || input.Note?.Length > 500 || input.Version < 1)
                return BadRequest(new { code = "invalid_patient" });

            var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (patient == null)
                return NotFound();
            if (patient.Version != input.Version)
                return Conflict(new { code = "version_conflict" });

            patient.Name = input.Name.Trim();
            patient.Note = input.Note;
            patient.Version++;
            await db.SaveChangesAsync(ct);
            return Ok(ToResponse(patient));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id, [FromQuery] int version, CancellationToken ct)
        {
            if (version < 1)
                return BadRequest(new { code = "invalid_version" });
            var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (patient == null)
                return NotFound();
            if (patient.Version != version)
                return Conflict(new { code = "version_conflict" });
            db.Patients.Remove(patient);
            await db.SaveChangesAsync(ct);
            return NoContent();
        }

        private static PatientResponse ToResponse(LearningPatient patient)
        {
            return new PatientResponse(patient.Id, patient.Name, patient.Identifier, patient.Note, patient.Version);
        }
    }
}
```

### 6.1 Create：为什么 Add 后还要 SaveChanges

`Add` 将实体置为 Added；`SaveChangesAsync` 才将 INSERT 发到数据库，并将生成的自增 Id 回填到实体。成功后实体通常变为 Unchanged。201 的 Location 指向新资源，不要在保存前就假设自增 Id 已存在。

重复标识由数据库唯一索引兜底，MySQL 1062 在本例被映射成 409。可以先查重改善提示，但并发下最终仍要处理数据库约束异常。本例只有一个业务唯一索引，所以错误命名为 duplicate_identifier；真实系统多个约束应分别识别，不能把所有 DbUpdateException 都叫“重复”。

### 6.2 Read：查询什么时候发 SQL

`Where` / `OrderBy` / `Select` 构建查询；`CountAsync` 发一次计数 SQL，`ToListAsync` 再发一次分页 SQL。两者之间如果有其他事务写入，total 与 items 可能短暂不一致；需要一致视图时应按业务要求设计读取事务或接口契约。

`Select` 只投影所需列；本例纯 DTO 投影也不会产生需要跟踪的实体，AsNoTracking 同时表达了只读意图。分页一定有确定排序；大页码可以改为基于上一条 Id 的游标分页。Contains 不一定能利用普通 B-tree 索引高效搜索，实际要看生成 SQL 与执行计划。

`FindAsync` 先查当前上下文已跟踪实体，再按主键查数据库；`SingleOrDefaultAsync` 期望零或一条，超过一条会抛异常；`FirstOrDefaultAsync` 取第一条，但业务上要求唯一时不应靠它掩盖重复数据。查询本身不会像 JPA 常见 AUTO flush 那样替你保存尚未 SaveChanges 的修改。

### 6.3 Update：跟踪与整对象覆盖

本例先查询得到被跟踪实体，只改允许的字段，再 SaveChanges；无需额外调用 `db.Update(patient)`。对外部输入构造的整对象调用 Update，可能把没有传来的字段也标记为更新，造成覆盖或越权赋值。

ChangeTracker 常见状态：Detached（未跟踪）、Added（待新增）、Unchanged（未变化）、Modified（待更新）、Deleted（待删除）。`AsNoTracking()` 查出的实体是 Detached，只修改它的属性后 SaveChanges 不会保存这些修改。

### 6.4 Version：为什么先比较，还需要并发令牌

先比较请求 Version 与查询结果，能发现“客户端拿旧数据修改”的情况；但比较之后、真正 UPDATE 之前，另一个请求仍可能更新同一行。并发令牌让保存时再检查一次。

```sql
-- 示意 SQL，参数名与实际生成结果可能不同
UPDATE learning_patients
SET Name = @name, Note = @note, Version = @newVersion
WHERE Id = @id AND Version = @originalVersion;
```

受影响行数为零时，EF Core 抛 `DbUpdateConcurrencyException`。普通 int Version 需要每次有效更新主动加一；只配置 IsConcurrencyToken 却从不改变版本，不能阻止后续覆盖。MySQL 示例不要直接照搬 SQL Server 的 rowversion / IsRowVersion 配置。[官方并发控制说明](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

### 6.5 Delete：删除也需要考虑版本和关系

本例 DELETE 要带 query 参数 version，同样检查旧版本及保存期间的并发变化。存在 Visit 时 Restrict 外键会阻止删除，返回 409。软删除则需要新增标记、调整查询过滤和唯一约束等，它不是 Remove 的自动行为。

## 7. 生成迁移并应用数据库

本节继续在学习工程 Learning.Api 目录执行，连接串保持指向 ef_learning。运行时注册能被工具成功解析时不必额外写设计时工厂。

```sh
dotnet build
dotnet ef migrations add InitialLearning --output-dir Migrations
dotnet ef migrations script 0 InitialLearning --output initial-learning.sql
```

先打开生成文件检查：

| 产物 | 检查重点 |
| --- | --- |
| `时间戳_InitialLearning.cs` | Up 创建哪些表/索引/外键；Down 如何逆向 |
| `时间戳_InitialLearning.Designer.cs` | 此迁移对应的目标模型元数据 |
| `LearningDbModelSnapshot.cs` | 下一次生成迁移的模型比较基准 |
| `initial-learning.sql` | 实际 DDL、自增、长度、decimal 精度、唯一索引和删除行为 |

确认无误后应用到独立学习数据库，再启动 API：

```sh
dotnet ef database update
dotnet ef migrations list
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://127.0.0.1:5099
```

EF 使用 `__EFMigrationsHistory` 记录已应用迁移。`migrations add` 生成代码，不自动修改数据库；`database update` 执行迁移，不自动生成你忘记添加的模型修改。建库过程至少区分：当前实体模型、模型快照、已生成迁移、数据库实际结构、已应用历史。

### 7.1 后续加字段如何走完一次变更

例如给 LearningPatient 新增 `public string? Email { get; set; }`，增加 `HasMaxLength(200)`，再执行：

```sh
dotnet ef migrations add AddPatientEmail
dotnet ef migrations script InitialLearning AddPatientEmail --output add-patient-email.sql
# 审查后仅应用到学习数据库
dotnet ef database update
```

新增非空字段时，已有行没有值：需要设计默认值或采用“先加可空列 → 回填 → 改非空”的多步迁移。重命名字段要检查是否被识别成 DropColumn + AddColumn；必要时改成 RenameColumn 保留原数据。Down 能删除新列，不代表删除的数据能恢复。[官方迁移管理说明](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)

`migrations remove` 适用于撤销尚未应用、尚未共享的最后一个迁移。已发布迁移一般通过后续修正迁移演进，不通过删除历史伪造状态。生产部署可生成 `--idempotent` SQL 或迁移 bundle，由受控流程执行；幂等 SQL 依据迁移历史跳过已应用迁移，不会修复所有人为改表造成的漂移。[官方应用迁移说明](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)

### 7.2 设计时工厂何时有用

多项目结构、宿主启动有其他依赖、命令行难以构造上下文时，可实现 `IDesignTimeDbContextFactory<T>`。工具找到工厂后用它构造上下文；工厂必须与运行时的 Provider / 映射选项保持一致。

```csharp
using System;
// 可选文件 LearningDbFactory.cs；完整 CRUD 示例不要求添加
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Learning.Api
{
    public class LearningDbFactory : IDesignTimeDbContextFactory<LearningDb>
    {
        public LearningDb CreateDbContext(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Learning")
                ?? throw new InvalidOperationException("Learning connection required");
            var options = new DbContextOptionsBuilder<LearningDb>()
                .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 8)))
                .Options;
            return new LearningDb(options);
        }
    }
}
```

ClinicFlow 的 DesignFactory 使用 design_only 连接串，适合生成模型迁移，不能假定它可执行真实数据库更新；实际演示环境由 Program.cs 调用 DatabaseInitialization 中的 MigrateAsync 应用迁移。`--project` 指定迁移所在项目，`--startup-project` 指定启动项目；多上下文时通过 `--context` 明确目标。

## 8. 用 HTTP 走完一次 CRUD

保持 API 终端运行，在另一终端执行。以下以全新数据库首个 Id 为 1 演示；如果已插过数据，以创建响应的 id 为准。

```sh
# 201，响应含 id 和 version: 1
curl -i http://127.0.0.1:5099/api/patients \
  -H 'Content-Type: application/json' \
  -d '{"name":"模拟患者","identifier":"DEMO-EF-001","note":"初次登记"}'

# 200，查询单条与分页
curl -i http://127.0.0.1:5099/api/patients/1
curl -i 'http://127.0.0.1:5099/api/patients?page=1&size=10'

# 200，成功更新，响应 version: 2
curl -i -X PUT http://127.0.0.1:5099/api/patients/1 \
  -H 'Content-Type: application/json' \
  -d '{"name":"模拟患者（已更新）","note":"复核完成","version":1}'

# 409，旧版本更新；不能静默覆盖
curl -i -X PUT http://127.0.0.1:5099/api/patients/1 \
  -H 'Content-Type: application/json' \
  -d '{"name":"旧页面提交","note":null,"version":1}'

# 409，重复唯一标识
curl -i http://127.0.0.1:5099/api/patients \
  -H 'Content-Type: application/json' \
  -d '{"name":"另一位模拟患者","identifier":"DEMO-EF-001","note":null}'

# 204，按当前版本删除；随后 GET 应为 404
curl -i -X DELETE 'http://127.0.0.1:5099/api/patients/1?version=2'
curl -i http://127.0.0.1:5099/api/patients/1
```

还应试验空名称、超长 Note、非法页码、缺少 version。重复唯一键可能消耗自增序号，自增 Id 不保证连续，不应把“没有跳号”作为正确性标准。

示例环境的停止方式是：API 终端使用 Ctrl+C，容器使用 `docker stop clinicflow-ef-learning`。该命令仅针对示例容器。

## 9. 关联查询、UTC 与参数化 SQL

以下是基于相同模型的局部方法体示例，`db` 是注入的 LearningDb，`ct` 是 CancellationToken，不需要全部粘进 Controller。

### 9.1 创建关联对象

```csharp
var patient = new LearningPatient { Name = "模拟患者", Identifier = "DEMO-EF-002" };
patient.Visits.Add(new LearningVisit
{
    StartsUtc = DateTime.UtcNow.AddDays(1),
    Fee = 80.50m
});
db.Patients.Add(patient);
await db.SaveChangesAsync(ct);
```

EF 根据关系保存对象图：先写 Patient，获得主键，再写引用它的 Visit。`m` 是 decimal 字面量后缀。这个过程适合一起创建的对象，不表示应该把客户端任意提交的整个对象图直接交给 Add / Update。

### 9.2 三种读取关联的方式

```csharp
// 加载实体图，适合后续确实需要实体操作的场景
var patient = await db.Patients
    .Include(p => p.Visits)
    .SingleAsync(p => p.Id == id, ct);

// 直接投影接口所需字段，通常更适合只读接口
var detail = await db.Patients.AsNoTracking()
    .Where(p => p.Id == id)
    .Select(p => new
    {
        p.Id,
        p.Name,
        Visits = p.Visits.OrderBy(v => v.StartsUtc)
            .Select(v => new { v.Id, v.StartsUtc, v.Fee }).ToList()
    })
    .SingleOrDefaultAsync(ct);

// 已跟踪患者存在时，显式加载集合
await db.Entry(patient).Collection(p => p.Visits).LoadAsync(ct);
```

Include 不是默认必需项：DTO 投影可直接利用导航表达查询。Lazy loading 需要额外配置，单纯添加 virtual 不会默认启用。循环访问关联并逐条查库会造成 N+1；多个集合 Include 也可能造成结果膨胀，需要评估投影或 AsSplitQuery。拆分查询有多次往返和并发一致性取舍，不是一律更快。[官方关联加载说明](https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager)

### 9.3 UTC 的边界

MySQL datetime 保存日期时间数值，不携带时区或 DateTime.Kind。入口接收 DateTimeOffset 时先明确偏移并转换到 UTC；写入时统一 UTC，输出时明确以 UTC 序列化。给已有时间简单标记 Kind 不等于做了时区换算。

ClinicFlow 的 API 命令使用 DateTimeOffset? 表达输入，持久化使用约定的 UTC DateTime，再由 UtcDateTimeConverter 统一 JSON 输出。这是应用约定，不能期待 EF 的列类型替你实现。

### 9.4 原生 SQL 不要拼接用户输入

```csharp
var identifier = "DEMO-EF-002";
var patients = await db.Patients
    .FromSql($"SELECT * FROM learning_patients WHERE Identifier = {identifier}")
    .AsNoTracking()
    .ToListAsync(ct);
```

这里直接传入插值表达式，EF 将值变为参数。不要先生成普通 string 再传给 FromSqlRaw。表名、列名不能像普通值那样参数化，动态排序字段应通过白名单映射。原生 SQL 返回实体时要满足所需列映射；常规查询优先 LINQ。[官方 SQL 查询说明](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries)

## 10. 事务与批量更新

### 10.1 显式事务何时需要

本例创建患者及其 Visit 使用一次 SaveChanges，关系数据库 Provider 默认在事务内完成。需要多个 SaveChanges 共用一个原子边界时，可以显式开启事务：

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
var patient = new LearningPatient { Name = "模拟患者", Identifier = "DEMO-EF-003" };
db.Patients.Add(patient);
await db.SaveChangesAsync(ct);

db.Visits.Add(new LearningVisit
{
    PatientId = patient.Id,
    StartsUtc = DateTime.UtcNow.AddDays(2),
    Fee = 120.00m
});
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

未 Commit 就离开作用域，未提交事务会回滚。不要在持锁事务内等待人工确认或慢速外部 HTTP；数据库回滚也无法撤销已经发出的邮件或外部设备操作。需要可靠外部副作用时，另行设计 Outbox 等交付机制。若启用重试执行策略，要以整个事务为重试单位，并处理重复副作用，不能只给 UseMySql 加一个开关。[官方事务说明](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

### 10.2 ExecuteUpdate / ExecuteDelete 与跟踪方式不同

```csharp
var affected = await db.Patients
    .Where(p => p.Id == id && p.Version == expectedVersion)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.Note, "批量接口示例")
        .SetProperty(p => p.Version, p => p.Version + 1), ct);
```

此处 `expectedVersion` 来自已校验的请求，affected 为零时需要按业务约定处理冲突或不存在。ExecuteUpdate 立即发 SQL，不需要 SaveChanges，不自动同步已跟踪实体，也不自动应用普通保存流程中的并发检查。多次 Execute 操作不会自动组合成一个整体事务。

避免在同一上下文混用“已加载实体更新”和“直接批量更新”同一行，否则旧跟踪值可能覆盖新数据。ClinicFlow 的业务规则、审计和幂等都依赖既定写入路径，不能为了少写几行代码改成绕过规则的批量写入。[官方批量更新说明](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)

## 11. 种子数据、连接配置与性能诊断

### 11.1 HasData 是模型管理数据

HasData 适合固定 Id、稳定的小型参考数据；相关变更可进入迁移。不要在 OnModelCreating 中用 DateTime.Now、Guid.NewGuid 或随机密码生成不稳定的种子，否则每次模型比较都可能出现变化。用户密码初始化等运行时行为，应有独立且可重复执行的初始化流程。

ClinicFlow 用 HasData 配置固定资源和虚构患者，用 StaffAccounts.Seed 在启动时按配置创建演示用户。这两种初始化不是一个机制。

### 11.2 如何看到实际 SQL

在学习工程 appsettings.Development.json 中增加：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Development 环境运行一次列表查询，观察计数和分页两条 SQL、选择的列与参数。局部调试也可以用 `query.ToQueryString()` 查看查询 SQL，不执行查询；它不展示后续 SaveChanges 的 UPDATE / INSERT，终结操作也可能调整最终 SQL。

EF 可通过标准 ILogger 管道记录日志；本例不默认启用敏感参数值输出。查看 SQL 后再用数据库执行计划判断扫描、索引和排序问题，不能凭 LINQ 外观看出性能。[官方日志说明](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/simple-logging)

### 11.3 排障表

| 现象 | 优先检查 | 修复思路 |
| --- | --- | --- |
| 无法解析 DbContext | 是否 AddDbContext；构造函数是否接收 DbContextOptions<T> | 注册与泛型类型保持一致 |
| `dotnet ef` 不存在 | 是否安装工具；是否位于 manifest 目录下 | dotnet tool restore / 检查工具版本 |
| 找不到 Provider / UseMySql | 包版本、using、项目引用 | 确认 Pomelo 及 Microsoft.EntityFrameworkCore 命名空间 |
| Access denied / 连接超时 | 用户授权、连接串、宿主与容器端口 | 先用同一账号和地址执行 SELECT 1 |
| Table / column 不存在 | 指向哪个库；迁移是否应用 | 对照迁移历史和实际结构，不靠重启猜测 |
| 改对象后没保存 | 是否 AsNoTracking；是否调用 SaveChangesAsync | 明确实体状态和保存边界 |
| 查询不能翻译 | 自定义 C# 方法是否可译为 SQL | 改成可翻译表达式，避免无意全表客户端计算 |
| 第二次操作在前一次完成前启动 | 同一 DbContext 是否并行查询 | 逐个 await；独立任务使用独立上下文 |
| 同主键实体重复跟踪 | 是否查询后又 Attach 另一实例 | 一个上下文中同一键使用同一个实体实例 |
| 唯一 / 外键错误 | 1062、1451、1452 等具体约束原因 | 转成有意义的业务响应，不统一吞掉异常 |
| DbUpdateConcurrencyException | 行是否已变更/删除、原版本是否正确 | 刷新并由用户或规则决定重试，不静默覆盖 |
| 请求很慢 | SQL 次数、分页、索引、事务锁和连接池 | 先观测再调优，不直接加 CommandTimeout |

## 12. 回到 ClinicFlow：逐项解释实际配置

| 位置与代码 | 实际作用 | 阅读时要注意 |
| --- | --- | --- |
| AddClinicServices：AddDbContext + UseMySql | Scoped ClinicDb、读取 Clinic 连接串、指定 MySQL 8.4.8 | 不是扫描配置文件自动发现数据库 |
| ClinicDb：Users / Patients / Idempotency | 给常用实体提供 DbSet 属性 | 其他实体可用 db.Set<T>()，不必为每种实体都写属性 |
| OisModel.Configure<T>：HasKey + HasMaxLength(36) | OIS 字符串 Id 主键及长度 | Id 在应用中生成，不是 MySQL 自增 |
| OisModel.Configure<T>：IsConcurrencyToken | 保存时检查旧 Version | 所属模块/领域对象维护 Version++ |
| TreatmentPlan：(CaseId, Revision) 唯一索引 | 同诊疗档案的计划版本号唯一 | 除业务检查外还有数据库兜底 |
| Reservation：(ResourceId, StartUtc) 等索引 | 支持资源/患者维度的预约查找 | 普通索引不保证时段不重叠 |
| TreatmentDelivery：ReservationId 唯一索引 | 同一预约的执行记录唯一 | 用约束配合业务幂等与状态规则 |
| PhysicalValidation：ReservationId 主键及外键 | 每个实验预约至多一份签署结果 | 独立实体不实现 IOisEntity，也不按可变状态维护 Version；重测使用新预约 |
| HasOne<T>().WithMany() + Restrict | 即使无导航属性也配置外键及删除行为 | 无导航不代表无数据库关系 |
| OisStateStore.Load | 加载实体和只读患者列表 | 大部分 OIS 对象需要跟踪，供规则变更后保存 |
| EfCommandTransaction.Track | 将规则中新建的 Detached 对象 Add 进上下文 | 已查询的跟踪对象无需再次 Add |
| OisCommandLock + 占用复合主键 | 多个 API 进程按患者/资源/目标互斥，唯一约束防止重复占格 | 是本项目显式设计，不是 EF 通用默认机制 |
| SaveChanges + Commit | 业务、审计、幂等记录一起提交 | 不能拆成分别提交的“先业务后审计” |
| Hosting/DatabaseInitialization.cs：MigrateAsync / StaffAccounts.Seed | 演示启动时应用迁移及初始化账号 | 生产升级和多副本需独立设计 |

可以按“配置 → 模型 → 迁移 → Load → OisSession → 所属模块 → Track → Save → Commit”的顺序，选一个创建计划或预约命令跟踪到数据库。每看到一个配置，都回答：它影响 C#、模型元数据、SQL 结构，还是请求执行？这能避免把不同层的保证混在一起。

## 13. 将持久化过程串起来

以预约为例，`OisStateStore.Load` 返回内部数据容器，已有实体大多处于跟踪状态；`OisSession` 将各集合交给所属模块。`SchedulingModule` 修改已有预约，或向状态列表加入新的 Reservation；此时修改还发生在内存中。`EfCommandTransaction.Track` 将新增对象加入 DbContext，随后 `SaveChangesAsync` 把业务变更、审计、幂等响应及 Outbox 写入同一事务，`CommitAsync` 才完成提交。

Version 有两层作用：规则执行前比较客户端版本与当前实体版本，避免基于过期页面操作；保存时 EF 根据并发令牌检测数据库行是否已改变。普通整数令牌不会自行递增，更新操作必须按业务语义维护它。预约范围冲突由时段预检查与 ReservationOccupancy 复合主键共同保护，局部锁协调设备和计划状态，不能仅靠 Version。

模型变更是另一条链路：实体与映射变化 → 生成迁移 → 审查 Up / Down 与模型快照 → 在隔离环境验证升级 → 应用迁移。修改 C# 属性不会直接修改已存在的 MySQL 表，历史迁移也不应为了匹配当前实体而被随意重写。

完整业务背景见 [项目开发顺序详解](project-development-learning.md)。本篇保留独立示例代码与命令用于解释机制，不要求另建工程或完成练习。

当前验证记录已通过 SeparatePhysicalValidation 迁移从 Reservation 拆入 PhysicalValidation，不能直接删除旧列而不复制数据。映射位于 `backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs`，实体位于 ClinicFlow.Domain 的业务目录；非公共 setter 可由 EF 实体化，外部调用者通过模块命令改变状态。读取工作台时使用 ReservationResponse 保持原 JSON，详见[数据库与升级](database-design.md)。

当前 Outbox 是实际持久化机制：领域事件由应用层同步处理，外部通知由独立投递事务处理；失败和重复投递不能靠 EF 业务事务回滚外部系统，见[事件文档](events.md)。

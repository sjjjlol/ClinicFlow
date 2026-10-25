# 从 Java 到 ASP.NET Core：按项目开发顺序入门

本文面向写过 Java、熟悉 Spring Boot 和关系数据库的开发者。目标是理解一个 ASP.NET Core Web API 从需求、建项目、编码到测试和部署的完整过程，沿着 ClinicFlow 的现有代码理解每个开发阶段的输入、实现与结果。本文以讲解和代码导读为主，不设置动手任务。

本文以当前仓库配置为准：.NET SDK `10.0.401`、目标框架 `net10.0`、ASP.NET Core、EF Core `9.0.20`、Pomelo MySQL Provider `9.0.0`。代码风格采用 .NET 5 时期的 C# 9 上限，使用普通类、显式构造函数及 Controller；运行时仍是 .NET 10。SDK 版本来自 `global.json`，Web 依赖版本来自 `backend/ClinicFlow.Api/ClinicFlow.Api.csproj`，EF 依赖版本来自 `backend/ClinicFlow.Infrastructure/ClinicFlow.Infrastructure.csproj`。这些版本承担不同职责，不要求数字全部相同；升级时要核对 Provider 与 EF Core 的兼容性，不能只把所有包改为与 .NET 相同的主版本。

## 阅读路线

建议先读 [ClinicFlow 项目开发顺序详解](project-development-learning.md)，建立业务与工程的全貌，再按本篇章节补充 C# 和 ASP.NET Core 知识。每一步先了解“为什么要做”，再定位本项目实现。

| 开发阶段 | 要解决的问题 | 本项目入口 |
| --- | --- | --- |
| 1. 明确需求 | 谁操作什么，哪些规则必须成立 | [产品范围](ois-mvp.md)、[领域语言](../CONTEXT.md) |
| 2. 建立工程 | SDK、项目、依赖和启动方式是什么 | `global.json`、`ClinicFlow.sln`、`backend/ClinicFlow.Api/ClinicFlow.Api.csproj` |
| 3. 读懂 C# | 如何表达数据、依赖和异步 I/O | `backend/ClinicFlow.Infrastructure/Persistence/ClinicDb.cs`、`backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs` |
| 4. 配置与装配 | 配置从哪里来，对象由谁创建 | `backend/ClinicFlow.Api/Program.cs`、`backend/ClinicFlow.Api/Hosting/`、`scripts/api.sh` |
| 5. 建模与持久化 | 表、关系、查询和升级如何实现 | `backend/ClinicFlow.Infrastructure/Persistence/ClinicDb.cs`、`backend/ClinicFlow.Infrastructure/Persistence/Migrations/` |
| 6. 实现业务 | 规则、事务、并发、幂等放在哪里 | `backend/ClinicFlow.Application/UseCases/`、`backend/ClinicFlow.Application/CommandExecutor.cs` |
| 7. 暴露 HTTP API | 请求如何绑定，响应如何生成 | `ReservationsController.BookTreatment` / `PrescriptionsController.SignPrescription` 等 Action、`backend/ClinicFlow.Api/ApiDocumentation.cs` |
| 8. 横切能力 | 错误、身份、权限、日志怎么统一 | `backend/ClinicFlow.Api/Hosting/`、`backend/ClinicFlow.Api/Middleware/`、`backend/ClinicFlow.Api/Identity.cs` |
| 9. 测试与交付 | 怎么证明正确，怎么运行产物 | `tests/`、`Dockerfile`、`.github/workflows/ci.yml` |

## 1. 先明确需求、接口和模块边界

新项目先写出角色、用例、输入输出和不变量，再选框架写接口。例如“预约治疗”至少要明确操作者、患者、资源、起止时间、资源冲突、失败响应，以及同一个请求重复提交时的结果。

ClinicFlow 的一次写操作可沿下面的路径阅读：

```text
HTTP POST /api/ois/reservations/treatment
  → 认证、授权、CSRF 校验
  → ReservationsController：绑定专用请求，提取当前用户
  → CommandExecutor.Execute → ICommandTransaction / EfCommandTransaction：事务、锁、幂等、加载数据
  → OisSession.Scheduling.BookTreatment：模块规则与领域状态变化
  → EF Core SaveChangesAsync + CommitAsync
  → HTTP JSON 响应
```

当前工程已把这些职责拆成 API、Application、Domain、Infrastructure 四个程序集，以项目引用约束依赖。业务上下文仍为模块化单体，不等于四个服务；无需给每个类加接口或给每张表包一层 Repository。

本项目的分工是：Controller 处理 HTTP；CommandExecutor 协调一次业务操作；三个业务模块及其领域对象执行业务规则，OisSession 装配模块依赖；DbContext 负责数据库映射和持久化。先理解这些职责，比记住目录名字更有用。

## 2. 认识 .NET 工程，然后让它运行起来

### 2.1 Java 概念对照

以下是理解职责的近似对照，不表示实现机制相同。

| Java / Spring 经验 | .NET 中的对应概念 | 需要调整的理解 |
| --- | --- | --- |
| Java 语言、JVM | C# 语言、.NET Runtime / CLR | C# 是语言，.NET 是平台，二者不要混称 |
| JDK | .NET SDK | SDK 包含构建工具；运行部署产物通常只需对应运行时 |
| Spring Boot / Spring MVC | ASP.NET Core | 本文讨论现代 ASP.NET Core，不讨论旧版 System.Web / Web Forms |
| 内嵌 Tomcat | Kestrel | Web 服务器；可以部署在反向代理后 |
| Maven `pom.xml` | `.csproj` | 声明目标框架、项目引用、包引用和构建设置 |
| Maven 多模块工程 | `.sln` + 多个 `.csproj` | Solution 聚合项目；Project 是编译单元 |
| Maven Central / dependency | NuGet / `PackageReference` | `packages.lock.json` 锁定解析后的依赖 |
| `application.yml` | `appsettings.json` + 配置提供程序 | 环境变量、命令行等也能覆盖配置 |
| `@Bean` / 构造器注入 | `services.Add…` / 构造器注入 | 内置 DI 通常显式注册，无默认组件包扫描 |
| JPA / Hibernate | EF Core | 需要学习 LINQ、跟踪、迁移和 Provider 行为 |
| JUnit | xUnit | `[Fact]`、`[Theory]` 与断言 |

### 2.2 先运行现有项目

从仓库根目录操作。第一次建议使用 [运行手册](runbook.md) 中的 Docker 入口：

```sh
./scripts/start.sh
```

学习后端调试时，按运行手册启动 MySQL 和本机 API，再启动 Vite。不要同时启动占用相同端口的本机 API 和应用容器。

仅查看环境、还原与编译可以使用：

```sh
./scripts/dotnet.sh --info
./scripts/dotnet.sh restore ClinicFlow.sln --locked-mode
./scripts/dotnet.sh build ClinicFlow.sln --no-restore
```

`restore` 解析依赖，`build` 编译，`run` 构建并运行开发项目，`test` 运行测试，`publish` 生成部署产物。`bin/`、`obj/` 是构建输出，不应手工修改。

仓库的 `scripts/dotnet.sh` 优先使用 PATH 中的 `dotnet`，否则尝试项目约定的本地 SDK 路径；它本身不安装 SDK。`global.json` 选择 SDK，`TargetFramework` 决定编译目标，二者是不同设置。`Directory.Build.props` 为后端、测试和升级工具指定 C# 9、可空分析、禁用隐式 using 和启用依赖锁文件。源码必须显式写 using；`Directory.Build.targets` 关闭会生成 C# 11+ 代码的 OpenAPI XML 注释生成器。

### 2.3 读懂工程文件与常用开发命令

下面是本项目工程文件的精简解释，不是要求重新覆盖现有 csproj：

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../SomeLibrary/SomeLibrary.csproj" />
    <PackageReference Include="Some.Package" Version="具体版本" />
  </ItemGroup>
</Project>
```

`Sdk.Web` 引入 Web 构建约定和 ASP.NET Core 共享框架；TargetFramework 决定可用框架 API；ProjectReference 引用本地项目；PackageReference 引用 NuGet 包。上面的 SomeLibrary / Some.Package 只是占位名称，当前 backend 没有这样的引用。

常用开发操作按目的选择：

| 目的 | 命令 | 产物或行为 |
| --- | --- | --- |
| 查看 SDK / 运行时 | `dotnet --list-sdks` / `dotnet --list-runtimes` | 区分“能构建”与“能运行” |
| 给项目加依赖 | `dotnet add 项目路径 package 包名 --version 版本` | 更新 csproj；仓库启用锁文件时也要检查锁文件变化 |
| 引用本地项目 | `dotnet add 项目路径 reference 依赖项目.csproj` | 建立编译依赖，不等于部署成另一个服务 |
| 热更新开发 | `dotnet watch --project backend/ClinicFlow.Api` | 监听变更，部分变更会重启进程；仍需准备连接串等环境 |
| 发布 Release | `dotnet publish backend/ClinicFlow.Api -c Release -o 输出目录` | 生成可部署文件，不自动启动服务 |

本仓库若使用脚本提供的 SDK，将命令中的 dotnet 换成 `./scripts/dotnet.sh`。添加依赖后需要有意更新和审查 packages.lock.json；CI 的 locked-mode 用于验证依赖可复现，不能期待它自动接受新依赖。

### 2.4 从零创建一个学习工程

下面只用于仓库外的临时目录，不要在现有 backend 中重新生成模板。先确认已安装 .NET 10 SDK。

```sh
mkdir AspNetLearning
cd AspNetLearning
dotnet new web -n Learning.Api -f net10.0
dotnet run --project Learning.Api --urls http://localhost:5099
```

另一个终端执行 `curl http://localhost:5099/`，应看到 `Hello World!`。`web` 是空 Web 模板，适合先理解入口；`webapi` 可生成更完整的 API 模板，使用 `--use-controllers` 可选择 Controller 风格。模板选项以本机 `dotnet new webapi --help` 为准。

## 3. 补齐读项目需要的 C#，不用先学完整门语言

### 3.1 类型、属性与空值

```csharp
public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CreatePatientRequest
{
    public string Name { get; set; } = "";
    public string Identifier { get; set; } = "";
}
```

`{ get; set; }` 是属性，调用方用 `patient.Name` 访问。本项目的实体和 DTO 都采用普通 class；通过 `new CreatePatientRequest { Name = "模拟患者" }` 初始化字段，避免依赖 record 和 with 简写。

`string?` 表示允许 null 的引用；`int?` 是允许 null 的值类型。启用 Nullable 后，非空引用约定主要由编译器分析，不能替代运行时请求校验。`!` 只是告诉编译器“我确信它非空”，不会自动检查或阻止空引用异常。

常见语法：

| C# 写法 | 含义 |
| --- | --- |
| `var patient = new Patient();` | 编译期推断类型，不是动态类型 |
| `value ?? fallback` | 左侧为 null 时取右侧 |
| `user?.Id` | user 为 null 时结果为 null |
| `new Patient { Name = "模拟患者" }` | 对象初始化器，设置属性 |
| `new ReviewPlanRequest { Id = id, Version = 1 }` | 显式类型和对象初始化器 |
| `new List<string> { "a", "b" }` | 显式创建并初始化集合 |
| `namespace ClinicFlow { ... }` | 块命名空间；目录结构不强制决定命名空间 |
| `this IServiceCollection services` | 静态扩展方法参数，例如 `services.AddClinicOpenApi()` |
| `public CommandExecutor(ICommandTransaction transaction, TimeProvider clock) { ... }` | 普通构造函数，DI 通过参数提供依赖 |

`using ClinicFlow;` 类似 import；`using var scope = ...` 则是资源管理，类似 try-with-resources。`await using` 对应异步释放，例如数据库事务。

### 3.2 async / await 与 CancellationToken

```csharp
var patients = await db.Patients
    .AsNoTracking()
    .OrderBy(p => p.Id)
    .ToListAsync(ct);
```

`Task<T>` 表示将来产生 T 的操作；`await` 在等待未完成的异步 I/O 时让出执行，不等于新建线程，也不等于把同步操作自动变快。数据库和 HTTP 调用优先使用异步 API，并沿调用链传入 `CancellationToken`。

不要用 `.Result` / `.Wait()` 阻塞异步调用，也不要为普通数据库请求包一层 `Task.Run`。`DbContext` 不支持并行操作：不能对同一个实例使用 `Task.WhenAll` 同时执行两个查询；应逐个 await，或在确有需要时使用独立上下文。

### 3.3 LINQ 与 Java Stream 的关键差异

`Where`、`Select`、`OrderBy`、`Any` 看起来类似 Stream，但要看数据源：

- `IEnumerable<T>` 通常在内存中遍历。
- EF Core 的 `IQueryable<T>` 构建表达式，Provider 尝试将其翻译成 SQL。
- `ToListAsync`、`SingleOrDefaultAsync` 等操作才真正执行数据库查询。

因此先过滤、排序、投影、分页，再物化结果；不要先 `ToListAsync` 把全表读进来再分页。不是所有 C# 方法都能翻译成 SQL。检查生成 SQL、索引和查询次数，避免循环查库造成 N+1。

## 4. 配置、启动与依赖注入：先把对象装配好

### 4.1 精简宿主与显式装配

本项目入口使用 `WebApplication.CreateBuilder` 和 C# 9 顶层语句，无需手写 Main 或 Startup。核心装配流程如下，完整代码见 `backend/ClinicFlow.Api/Program.cs`：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddClinicServices(builder.Configuration, builder.Environment);
await using var app = builder.Build();
await app.InitializeClinicDatabaseAsync();
app.UseClinicMiddleware();
app.MapClinicEndpoints();
await app.RunAsync();
```

`Hosting/ServiceCollectionExtensions.cs` 的 AddClinicServices 在 Build 前注册依赖；`Hosting/WebApplicationExtensions.cs` 的 UseClinicMiddleware 装配中间件，MapClinicEndpoints 调用 MapControllers 映射 Controller。Controller 继续用特性声明接口，精简宿主与 Minimal API 是两个独立选择。

`Add…` 通常表示注册能力，`Use…` 通常表示加入请求管道，`Map…` 通常表示映射端点。注册认证服务之后，还需要认证中间件和授权规则。运行时仍为 .NET 10，语言上限仍为 C# 9。

### 4.2 配置来源与密钥

默认应用配置中，常用来源按覆盖优先级从低到高为：`appsettings.json`、`appsettings.{Environment}.json`、Development 下的 User Secrets、环境变量、命令行。自定义配置提供程序可以改变顺序。层级键在代码中写成 `ConnectionStrings:Clinic`，环境变量使用 `ConnectionStrings__Clinic`。[官方配置说明](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)

本项目的 `scripts/api.sh` 显式读取 `.env` 并导出连接串、监听地址和 Development 环境。ASP.NET Core 默认不会自动加载仓库 `.env`；这是脚本提供的行为。容器配置则由 Compose 注入。

新项目可以用 `IOptions<T>` 将配置绑定为强类型对象，并在启动时校验必填项。密码和连接凭据通过本地秘密存储或部署环境注入，不写进源码。User Secrets 供开发使用，不是生产密钥管理服务。

### 4.3 强类型配置：把配置绑定与启动校验写完整

在新项目中，可以把一个模块的设置集中为 Options 类。下面以预约提前量为例，示意配置与注入方式，不代表 ClinicFlow 现在已有此限制。

appsettings.json：

```json
{
  "Scheduling": {
    "MaxAdvanceDays": 30
  }
}
```

Options 类和使用它的服务可以放在单独文件：

```csharp
using Microsoft.Extensions.Options;

public class SchedulingOptions
{
    public const string SectionName = "Scheduling";
    public int MaxAdvanceDays { get; set; }
}

public class BookingWindow
{
    private readonly IOptions<SchedulingOptions> options;
    private readonly TimeProvider clock;

    public BookingWindow(IOptions<SchedulingOptions> options, TimeProvider clock)
    {
        this.options = options;
        this.clock = clock;
    }

    public bool Allows(DateTimeOffset start)
    {
        var now = clock.GetUtcNow();
        return start > now && start <= now.AddDays(options.Value.MaxAdvanceDays);
    }
}
```

本项目风格是在 AddClinicServices 中注册：

```csharp
services.AddOptions<SchedulingOptions>()
    .Bind(configuration.GetSection(SchedulingOptions.SectionName))
    .Validate(o => o.MaxAdvanceDays >= 1 && o.MaxAdvanceDays <= 365,
        "Scheduling:MaxAdvanceDays must be between 1 and 365")
    .ValidateOnStart();
services.AddSingleton(TimeProvider.System);
services.AddScoped<BookingWindow>();
```

这条链路先读取配置节，再绑定属性、注册校验，最后在启动时触发校验。缺少配置时 int 默认为 0，会被校验拒绝；这样比等到首次业务请求才发现配置无效更容易定位。环境变量 `Scheduling__MaxAdvanceDays=60` 可以覆盖 JSON。

`IOptions<T>` 适合读取固定配置；`IOptionsSnapshot<T>` 是 Scoped，适合按请求获得快照；`IOptionsMonitor<T>` 可供 Singleton 使用并观察支持重载的配置来源。是否真的热更新，还取决于提供程序；不要假设修改环境变量就会自动改变已运行进程。[官方 Options 文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)

### 4.4 DI 生命周期与 Spring 默认作用域不同

| 注册方法 | 生命周期 | 适合的依赖 |
| --- | --- | --- |
| `AddTransient<T>()` | 每次从容器解析时创建 | 轻量、无共享状态的组件 |
| `AddScoped<T>()` | 每个作用域一个实例；HTTP 中通常每请求一个 | DbContext、需要它的业务服务 |
| `AddSingleton<T>()` | 根容器存续期间共享一个实例 | 线程安全的共享服务 |

Spring Bean 默认通常是 singleton；不要把这个习惯直接套到有 DbContext 的服务上。本项目的 `AddDbContext<ClinicDb>` 默认注册 Scoped，`CommandExecutor` 也是 Scoped，`TimeProvider.System` 作为 Singleton 注册。

Singleton 不能直接持有 Scoped 服务，否则会让请求状态跨请求存活。启动任务或后台服务需要访问数据库时，显式创建作用域；本项目启动迁移使用 `app.Services.CreateAsyncScope()`。一般业务类优先构造器注入，而不是到处调用 `GetRequiredService`。生命周期与作用域规则见 [官方 DI 文档](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0)。

### 4.5 从注册到注入：一个可跟踪的服务示例

需要替换实现或明确边界时，再引入接口。例如在仓库外学习工程中定义：

```csharp
public interface IGreetingService
{
    string Greet(string name);
}

public class GreetingService : IGreetingService
{
    public string Greet(string name) => $"你好，{name.Trim()}";
}
```

在 AddClinicServices 中调用 `services.AddScoped<IGreetingService, GreetingService>()`，Controller 构造函数即可接收 `IGreetingService service`，在 Action 中调用 `service.Greet(input.Name)`。这是“注册抽象到实现 → 容器创建对象 → 构造函数请求依赖”的完整过程；直接注入未注册的接口会失败。

业务类同样可以通过构造器接收依赖。容器解析 CommandExecutor 时，会继续解析它的 ICommandTransaction 和 TimeProvider 参数；前者由 EfCommandTransaction 实现，再依赖 ClinicDb。排查“Unable to resolve service”时，沿构造参数逐个检查注册，不要只看最外层服务。

## 5. 数据库：先定义模型与约束，再生成迁移

本节先建立整体认识。需要进一步理解安装、连接、模型映射和 CRUD 时，继续阅读 [EF Core 实战：从接入 MySQL 到完成 CRUD](ef-core-learning.md)，里面提供独立工程的完整代码、关键配置逐项解释、迁移命令与验证请求。

### 5.1 EF Core 的工作单位

`ClinicDb : DbContext` 类似一次工作单位中的 EntityManager：管理实体跟踪、查询和保存。`DbSet<Patient>` 是实体查询/操作入口，不是已经加载到内存的患者列表。

`OnModelCreating` 中的 Fluent API 定义主键、外键、字段长度、索引和并发标记。本项目的 `OisModel.Configure` 集中配置 OIS 模型。业务校验给用户解释，数据库约束保护最终一致性，两者各有职责。

查询出来的实体默认被跟踪，修改属性后调用 `SaveChangesAsync` 保存；`Add` 只把实体标记为待新增，不会立即执行 INSERT。纯读查询可使用 `AsNoTracking()`。接口响应优先使用 DTO，避免直接暴露完整实体图或敏感字段。[DbContext 官方说明](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

### 5.2 迁移不是改类后自动改表

开发新字段的一般顺序：修改实体 → 配置映射/约束 → 生成迁移 → 审查迁移 SQL → 在隔离数据库验证 → 部署时受控执行。

仓库根目录的 `dotnet-tools.json` 已固定本地工具版本，先还原工具。下面新增迁移的命令是学习分支示例，只有实际改模型时才执行，不要为阅读教程创建空迁移。

```sh
# 还原仓库清单中的工具，并检查实际版本
./scripts/dotnet.sh tool restore
./scripts/dotnet.sh tool list --local
./scripts/dotnet.sh ef migrations add AddPatientNote --project backend/ClinicFlow.Infrastructure
./scripts/dotnet.sh ef migrations script --idempotent --project backend/ClinicFlow.Infrastructure --output migration.sql
```

`backend/ClinicFlow.Infrastructure/Persistence/DesignFactory.cs` 为 EF CLI 创建设计时 DbContext。生成迁移主要比较当前模型与模型快照，不是读取线上数据库后反推差异。这里的设计时连接串不是运行时连接串；需要连接数据库的命令不能直接拿它操作真实环境。

本项目在启动时调用 `MigrateAsync()`，方便演示环境。生产多副本部署通常将迁移作为受控步骤，审查 SQL、备份、控制并发并准备恢复方案。不要删除已应用的历史迁移来“修复”升级，也不要用 `EnsureCreated` 与 Migrations 混着管理同一个库。项目的升级和数据删除范围见 [数据库设计](database-design.md)。

## 6. 业务层：明确事务、并发和重试的边界

### 6.1 规则与 I/O 分开

各模块的操作方法根据所属状态、命令、角色和传入时间决定能否执行；跨模块条件通过契约查询。它不负责打开数据库事务，也不自己访问系统时钟，因此能直接构造数据做规则测试。

`CommandExecutor.Execute` 通过 ICommandTransaction 调用基础设施事务适配器，后者加载数据、执行委托、保存审计、幂等结果及 Outbox。应用层委托调用用例并处理领域事件。时间通过注入的 `TimeProvider` 获取，再传给规则层。这与 Spring 中应用服务协调领域对象的思路相近。

### 6.2 不要寻找必须存在的 @Transactional

EF Core 对支持事务的 Provider，单次 `SaveChanges` 默认在事务内完成；多个保存步骤、读取后判断再写入等场景，需要按业务边界决定显式事务。ASP.NET Core 本身不会因为一个类叫 Service 就自动给它加事务。

本项目写入先开启 ReadCommitted 事务，认领幂等请求、获取患者/资源/目标局部锁并复查关联，再加载状态、执行业务、同步时间格及实际执行占用，最后提交。时间格复合主键防止同一患者或资源重复占用；快照读取使用 RepeatableRead 保持跨表视图一致。全量加载仍限制规模，不应把移除全局锁等同于完成生产扩展。

事务提供原子性，但不自动解决所有并发问题。预约重叠涉及范围冲突，单靠某行的版本字段不足以解决；要结合数据库约束、锁策略与业务规则设计。

### 6.3 区分三个容易混淆的概念

| 概念 | 解决的问题 | 本项目做法 |
| --- | --- | --- |
| 事务 | 多处写入一起成功或一起失败 | 业务、待办、审计、幂等结果一起提交 |
| 并发控制 | 多个操作者基于旧状态互相覆盖 | 命令带 Version，实体配置并发标记，并配合写锁 |
| 幂等 | 请求超时重试造成重复执行 | `Idempotency-Key` + 请求指纹 + 已保存响应 |

同一次操作重试应保留原幂等键；相同键对应不同输入应报冲突。幂等不等于“任何重复请求都忽略”，事务也不等于幂等。

## 7. API：Controller 与显式请求绑定

### 7.1 本项目的参数从哪里来

在 `Controllers/ReservationsController.cs` 中，基础类声明 `[Route("api/ois")]`，BookTreatment 方法声明 `[HttpPost("reservations/treatment")]`。各资源拥有独立 Controller 和专用请求类型：

| 参数或依赖 | 来源 |
| --- | --- |
| 业务方法 | Action 通过 OisSession 直接选择所属模块的方法，不按字符串分发 |
| `string id` | 路由目标 ID；正文不能覆盖 |
| `[FromBody] BookTreatmentRequest input` | POST JSON 请求体 |
| `CommandExecutor service` | Controller 构造函数注入 |
| `HttpContext`、`User`、`Request` | ControllerBase 提供的当前请求属性 |
| `CancellationToken ct` | 当前请求的取消信号 |
| `Idempotency-Key` | 通过 `Request.Headers` 读取，交由 CommandExecutor 校验 |

### 7.2 一个可独立运行的小接口

在第 2 节的临时工程中，Program.cs 注册和映射 Controller：

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

该学习工程同样采用顶层语句和精简宿主。另建 `Controllers/GreetingsController.cs`：

```csharp
using System;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/greetings")]
public class GreetingsController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] GreetingRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { code = "invalid_name" });
        return Ok(new { message = $"你好，{input.Name.Trim()}" });
    }
}

public class GreetingRequest
{
    public string? Name { get; set; }
}
```

运行后发送 `curl -i http://localhost:5099/api/greetings -H 'Content-Type: application/json' -d '{"name":"Java 开发者"}'`，预期为 200；空名称返回 400。

### 7.3 Controller 的职责

`[ApiController]`、`[HttpPost]` 类似 Spring MVC 的类/方法注解。继承 ControllerBase 提供 Ok、Unauthorized、NoContent、BadRequest、NotFound 等方法；返回 IActionResult 或 Task<IActionResult> 表达 HTTP 响应。依赖通过构造函数注入，业务逻辑委托给 Service。

本项目按业务资源拆分 Controller，工作台查询单独保留。请求类型位于 ClinicFlow.Api/Contracts/Requests，显式 ToCommand 后进入应用层；每个 Action 直接调用明确业务方法，CommandExecutor 通过 ICommandTransaction 共用事务、幂等和审计。历史格式只保留在 Compatibility 内。继续新增接口时应遵守同样职责划分。

### 7.4 绑定、校验、业务规则分开理解

JSON 无法解析、字段类型错误、缺失请求体属于绑定问题；字段内容和长度由输入校验处理；资源冲突、角色约束属于业务规则。

`[ApiController]` 会在 ModelState 无效时自动返回错误。AddClinicServices 将错误统一成 400 和 `{ code: "invalid_request", message, correlationId }`，并关闭非空引用类型隐式 Required 校验，使缺省值和显式 null 继续交由 CommandExecutor 与所属模块规则处理。

MVC 使用 AddJsonOptions 注册 UTC DateTime 转换器；中间件 WriteAsJsonAsync 使用独立的 HTTP JSON 配置，两处都需要保持契约一致。OpenAPI 由 AddClinicOpenApi 和 ApiDocumentation.cs 生成，`/api/openapi/v1.json` 要求 OIS 授权。

### 7.5 用请求契约约束一次开发

写业务端点前，先记录请求来源、必填字段、字段上限、角色、成功响应、失败码和重复请求语义。例如患者分页查询，应明确 page 默认 1、size 范围、keyword 来源、排序规则以及返回 DTO；不要让前端猜测实体内部字段。

一条接口开发可以按这个顺序推进：

1. 定义请求 / 响应 DTO，明确 null 与缺省值的含义。
2. 在端点做 HTTP 绑定，传入当前用户和 CancellationToken。
3. 在所属业务模块和领域对象中校验业务规则；写操作设计事务与并发边界。
4. 将结果映射成约定的 HTTP 状态码和 JSON。
5. 更新 OpenAPI 元数据，再验证成功、无效输入、无权限和冲突路径。

最常见的三个边界错误是：把请求体当可信实体直接 Update；把登录成功当成拥有所有操作权限；保存业务数据成功后，再单独保存必须与它一致的审计记录。阅读本项目时，可以对照 Controller、业务模块和 CommandExecutor 检查它们如何分别处理这些问题。

## 8. 请求管道、错误处理与安全

### 8.1 Middleware 是有顺序的调用链

```csharp
app.Use(async (context, next) =>
{
    // 请求进入时执行
    await next();
    // 下游返回后执行
});
```

理解为包裹式调用：A 进入 → B 进入 → 端点 → B 返回 → A 返回。不调用 next 会短路。异常处理要放在希望捕获的下游之前；认证应先于授权；限流、CORS 等中间件还要考虑路由和策略所需信息。[官方中间件说明](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0)

本项目在 UseClinicMiddleware 中显式调用 UseRouting，再执行认证、授权、限流和 CSRF；MapClinicEndpoints 调用 MapControllers 映射 Controller。阅读时要同时看中间件顺序与 Controller 的特性。

本项目先设置关联标识和响应头，再统一处理异常，随后执行认证、授权、限流和自定义 CSRF 校验。OisCommandController 的 `[Authorize(Policy = "ois")]` 作用于整个 Controller，所属模块还会校验具体业务操作的责任角色。

### 8.2 统一错误与日志

项目将 `BusinessException` 映射成业务响应，将格式问题映射为 400，未预期错误记录日志并返回通用 500。常见状态码含义：400 输入无效、401 未认证、403 已认证但无权限、404 不存在、409 状态或并发冲突、429 限流。

新项目可以采用 `AddProblemDetails`、`UseExceptionHandler` 等框架能力统一错误契约；本项目已有 `{ code, message, correlationId }` 约定，扩展时应保持一致。不是所有框架产生的错误都会自动经过自定义异常分支，需通过 HTTP 测试验证响应。

使用 `ILogger<T>` 的结构化参数记录日志，例如 `logger.LogInformation("Loaded patient {PatientId}", id)`。保留错误码、操作类型和关联标识，避免记录密码、Cookie 或完整敏感请求。项目用 `HttpContext.TraceIdentifier` 关联一次请求，它不自动等于跨服务的分布式 Trace ID。

### 8.3 认证、授权、CSRF 和 CORS 各管什么

| 能力 | 回答的问题 | 本项目对应实现 |
| --- | --- | --- |
| Authentication | 你是谁 | Cookie 认证，还原 ClaimsPrincipal |
| Authorization | 你能做什么 | `[Authorize(Policy = "ois")]` + 业务角色检查 |
| Antiforgery / CSRF | 浏览器自动带 Cookie 时，写请求是否来自合法交互 | CSRF Cookie 与 `X-CSRF-TOKEN` 配合校验 |
| CORS | 浏览器是否允许前端跨源访问 API | 开发时 Vite 代理、部署时同源，当前无需额外开放跨源 |

本项目的 Cookie 认证票据由 Data Protection 保护，并非必须通过内存 session id 查找服务器会话。`PasswordHasher<DemoUser>` 用于密码哈希，不是完整 ASP.NET Core Identity 用户体系，也不是简单加密后可还原密码。

测试写接口时，先取 `/api/auth/csrf` 并保存 Cookie，登录时携带令牌，登录后重新获取令牌，再提交业务命令；业务 POST 还需幂等键。具体请求流程参考 `tests/auth.mjs` 与 [操作手册](ois-runbook.md)。CORS 不能替代认证或 CSRF 校验。

部署中需要 HTTPS、正确配置代理和持久化 Data Protection 密钥；多副本还需要可共享的密钥配置。项目 Docker/Compose 已有密钥卷设计。外部系统调用可使用 `IHttpClientFactory`，后台循环任务可使用 `BackgroundService`，但不要在请求结束后继续使用该请求的 Scoped DbContext。

## 9. 测试：按层次验证，而不是只确认能启动

| 层次 | 验证目标 | 本项目例子 |
| --- | --- | --- |
| 规则单元测试 | 状态转换、权限、边界时间、失败后状态不变 | `tests/OisTests.cs` 中 `OisRulesTests` |
| 数据库集成测试 | MySQL 约束、事务、幂等、并发与持久化 | `tests/DatabaseFixture.cs` 及数据库测试 |
| HTTP 测试 | 绑定、Cookie、CSRF、状态码、接口契约 | `tests/http-ois.mjs`、`tests/auth.mjs` |
| 浏览器与部署冒烟 | 工作台流程、静态资源、打包后可访问 | 前端 E2E、`tests/http-ui.mjs` |

xUnit 的 `[Fact]` 表示普通测试，`[Theory]` + `[InlineData]` 用于参数化测试。一般 ASP.NET Core 项目也常用 `WebApplicationFactory<Program>` 测试完整 HTTP 管道；本项目当前 HTTP 测试通过 Node 脚本访问运行中的服务，没有使用 Factory 测试，因此不额外声明 public Program 类型。

开始学习时先运行不依赖数据库的规则测试：

```sh
./scripts/dotnet.sh test tests --filter FullyQualifiedName~OisRulesTests
```

接着按 [运行手册](runbook.md) 使用隔离的测试数据库执行 `./scripts/test.sh`，最后再运行 HTTP / 浏览器测试。数据库 Provider 行为是测试的一部分，EF InMemory 无法替代 MySQL 对 SQL、锁和事务的验证。HTTP 测试会写数据，应使用隔离演示环境。

### 9.1 规则单元测试怎么写

规则应尽量接受显式输入并返回结果或抛业务异常。例如下面是独立教学规则，并非本项目现有类：

```csharp
public static class VisitTimeRule
{
    public static bool IsValid(DateTimeOffset start, DateTimeOffset end) =>
        end > start && end - start <= TimeSpan.FromHours(2);
}
```

xUnit 测试放在测试项目中：

```csharp
using Xunit;

public class VisitTimeRuleTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(60, true)]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void Rejects_invalid_duration(int minutes, bool expected)
    {
        var start = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, VisitTimeRule.IsValid(start, start.AddMinutes(minutes)));
    }
}
```

测试覆盖零时长、反向时间、边界内和边界外，不依赖当前系统时间。对数据库事务则另写集成测试，验证失败后没有留下部分数据；对接口再验证绑定和认证。不同层测试回答不同问题。

### 9.2 HTTP 集成测试如何装配

在一般新项目中，可在测试工程引用 Web 项目，添加与目标框架匹配的 `Microsoft.AspNetCore.Mvc.Testing`，使用 WebApplicationFactory。以下代码假定提供了 `/api/health` 并且测试环境依赖已准备好：

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class HealthTests
{
    [Fact]
    public async Task Health_endpoint_responds()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }
}
```

上述示例需要测试项目能访问入口类型；如果未来引入 Factory 测试，可在入口补充 `public partial class Program { }`，或通过 InternalsVisibleTo 向测试程序集开放访问。目前项目没有这种依赖，入口只保留顶层语句。Factory 创建 TestServer，测试可以覆盖服务注册和配置。由于本项目的启动代码会执行迁移及账号初始化，Factory 测试必须在启动前准备好隔离数据库配置，不可直接指向应用数据库。

本仓库已有 HTTP 脚本，不要求再增加一套同功能的 Factory 测试。学习重点是理解测试如何启动应用，以及如何隔离它使用的依赖。

## 10. 发布、运行和排障

`build` 证明代码可编译；`publish` 生成部署产物；运行时环境仍需正确的配置、数据库和静态文件。普通框架依赖发布可用：

```sh
./scripts/dotnet.sh publish backend/ClinicFlow.Api -c Release -o /tmp/clinicflow-publish
```

这条命令只发布后端，不会自动构建 React 并把产物放进 `wwwroot`。本项目完整交付由 Dockerfile 完成：Node 构建前端 → .NET SDK 发布后端并拷入前端资源 → ASP.NET Runtime 镜像运行 `ClinicFlow.dll`。最终镜像无需 SDK 和 Node。框架依赖发布需要匹配的运行时；自包含发布是另一种交付选择。

`.github/workflows/ci.yml` 固化构建、测试、迁移恢复和容器验证。开发本机能运行，只证明当前环境可运行；交付前还要验证依赖锁定、数据库升级、部署配置、健康检查和静态资源。

本项目 `/api/health` 返回数据库连接状态，但 HTTP 200 本身不代表 JSON 中的业务状态为 ready；监控应按当前契约检查内容，或在设计新的探针时明确失败状态码。

遇到问题按链路排查：

1. 无法启动：核对 SDK、端口、连接串、数据库就绪情况和迁移日志。
2. 401 / 403：检查登录 Cookie、认证配置、端点策略和业务角色。
3. 400：检查 JSON、字段类型和 CSRF；不要把所有 400 都当成业务规则失败。
4. 409：检查版本、资源冲突及幂等键是否与原请求一致。
5. 500：用关联标识定位日志，检查 SQL、事务和配置；不把堆栈返回给用户。
6. 页面正常但 API 失败：检查 Vite 代理/部署路径、Cookie 与 CSRF；API 正常但页面空白则检查前端构建产物和静态资源路径。

### 10.1 发布前逐项确认配置归属

| 项目 | 开发环境 | 部署环境 |
| --- | --- | --- |
| 运行程序 | SDK + dotnet run / watch | publish 产物 + Runtime，或自包含产物 |
| 数据库地址 | 宿主端口，例如 127.0.0.1:3308 | 容器服务名或正式数据库地址 |
| 配置与凭据 | 本地脚本 / User Secrets | 部署环境注入，不依赖开发者 shell |
| 前端 | Vite 开发服务器及代理 | 构建后的静态资源放入 wwwroot |
| 数据库升级 | 隔离库验证迁移 | 备份、受控执行、失败恢复 |
| Cookie 密钥 | 本机开发密钥 | 持久化；多副本时配置共享与一致应用标识 |
| 日志与探针 | 控制台便于调试 | 日志收集、关联标识、明确探针失败语义 |

不要把 `dotnet run` 能启动等同于完整交付。先验证后端 publish，再验证前端资源是否随包交付，最后使用最终运行方式做冒烟；这也是项目 CI 分别验证本机流程和容器流程的原因。

## 11. 按项目开发顺序串起知识点

项目的业务开发主线是“诊疗档案 → 处方 → 计划 → 审核与实验验证 → 治疗执行”，工程支撑则是“宿主与身份 → 数据模型 → 事务执行器 → 资源接口 → 工作台 → 测试与交付”。两条线相互配合：只有表和接口无法保证业务正确，只有内存规则也无法处理持久化和并发。

[项目开发顺序详解](project-development-learning.md) 按这两条线展开，包括真实提交演进、各阶段的设计原因、实际源码入口、一次请求的完整过程，以及失败后的状态变化。下表用于把本篇基础概念放回项目中：

| 开发阶段 | 本篇对应知识 | 项目中形成的能力 |
| --- | --- | --- |
| 确认角色和流程 | 第 1 节 | 确定谁能签署、谁能排期、何时允许执行 |
| 建立宿主和身份 | 第 2～4、8 节 | 服务装配、Cookie 身份和受保护的接口 |
| 建模并保存状态 | 第 5 节及 EF Core 专篇 | 历史版本、外键、约束和迁移 |
| 实现写入主线 | 第 6～7 节 | 明确请求、规则、事务、审计和幂等的职责 |
| 连接工作台 | 项目详解第 9 节 | 表单请求、聚合快照、阻塞原因和刷新 |
| 验证与交付 | 第 9～10 节 | 规则、数据库、HTTP、浏览器和升级的不同证据 |

## 12. 学完后回到这些代码

按这个顺序读，能逐步解释一次请求为何得到最终结果：

1. [`backend/ClinicFlow.Api/Program.cs`](../backend/ClinicFlow.Api/Program.cs)：宿主、启动迁移；再读 [`backend/ClinicFlow.Api/Hosting/ServiceCollectionExtensions.cs`](../backend/ClinicFlow.Api/Hosting/ServiceCollectionExtensions.cs) 的 DI 配置及 [`backend/ClinicFlow.Api/Hosting/WebApplicationExtensions.cs`](../backend/ClinicFlow.Api/Hosting/WebApplicationExtensions.cs) 的请求管道。
2. [`backend/ClinicFlow.Infrastructure/Persistence/ClinicDb.cs`](../backend/ClinicFlow.Infrastructure/Persistence/ClinicDb.cs) 与 [`backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs`](../backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs)：实体、关系、索引和并发标记。
3. [`backend/ClinicFlow.Application/CommandExecutor.cs`](../backend/ClinicFlow.Application/CommandExecutor.cs)：从 [`backend/ClinicFlow.Api/Controllers/ReservationsController.cs`](../backend/ClinicFlow.Api/Controllers/ReservationsController.cs) 跟进 Execute；查询单独看 [`backend/ClinicFlow.Infrastructure/Queries/EfWorkspaceReader.cs`](../backend/ClinicFlow.Infrastructure/Queries/EfWorkspaceReader.cs) 的 Snapshot。
4. [`OisSession`](../backend/ClinicFlow.Application/OisSession.cs) 的模块装配 → [`ReservationsOperations.cs`](../backend/ClinicFlow.Application/UseCases/Scheduling/ReservationsOperations.cs) 的预约入口 → [`SchedulePolicy.cs`](../backend/ClinicFlow.Domain/Scheduling/SchedulePolicy.cs) 的冲突规则 → [`Reservation`](../backend/ClinicFlow.Domain/Scheduling/Reservation.cs) 的状态变化。
5. [`backend/ClinicFlow.Api/Identity.cs`](../backend/ClinicFlow.Api/Identity.cs) 与 [`backend/ClinicFlow.Domain/Shared/BusinessException.cs`](../backend/ClinicFlow.Domain/Shared/BusinessException.cs)：用户与错误如何进入业务链路。
6. [`tests/OisTests.cs`](../tests/OisTests.cs)：把一条失败规则与对应测试对照起来。
7. [`Dockerfile`](../Dockerfile) 与 [运行手册](runbook.md)：解释开发启动与最终交付的差别。

继续查阅时，以 Microsoft Learn 的 [.NET 文档](https://learn.microsoft.com/zh-cn/dotnet/)、[ASP.NET Core 文档](https://learn.microsoft.com/zh-cn/aspnet/core/?view=aspnetcore-10.0) 和 [EF Core 文档](https://learn.microsoft.com/zh-cn/ef/core/) 为入口，并选择与项目一致的版本。不要将旧 ASP.NET Framework 的配置方式、其他版本模板或其他数据库 Provider 的行为直接套到本项目。

分层后建议再读[领域事件与集成事件](events.md)：聚合记录事实，应用层同步联动，基础设施将独立集成契约写入 Outbox。API 不直接访问 EF，Application 不引用 HTTP Request 类型。

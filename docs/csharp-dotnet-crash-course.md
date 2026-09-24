# Java 开发者 C# / .NET 服务端速成手册

> 面向有 Java 后端经验（Spring Boot / JPA / Maven）的开发者。
> 以 ClinicFlow 项目为实例，覆盖 C# 语法、ASP.NET Core、EF Core 的核心知识点。
> 配套阅读：`docs/java-reading-guide.md`（代码阅读路线）、`docs/architecture.md`（架构决策）。

---

## 目录

1. [工程体系对照：从 Maven/Gradle 到 .NET](#1-工程体系对照)
2. [C# 语言速成（对照 Java）](#2-c-语言速成)
3. [ASP.NET Core 服务端核心](#3-aspnet-core-服务端核心)
4. [EF Core 数据访问（对照 JPA/Hibernate）](#4-ef-core-数据访问)
5. [异步编程：async/await](#5-异步编程)
6. [ClinicFlow 项目导读](#6-clinicflow-项目导读)
7. [速查表](#7-速查表)

---

## 1. 工程体系对照

| Java 世界 | .NET 世界 | 本项目实例 |
|---|---|---|
| Maven `pom.xml` / Gradle | `*.csproj`（SDK 风格 XML） | `backend/ClinicFlow.csproj` |
| 多模块 `parent pom` | `*.sln` 解决方案（可含多个 csproj） | `ClinicFlow .sln` |
| Maven Central 依赖 | NuGet 包（`<PackageReference>`） | Pomelo.EntityFrameworkCore.MySql |
| `mvn compile` | `dotnet build` | |
| `mvn spring-boot:run` | `dotnet run` | |
| `mvn test` (JUnit) | `dotnet test` (xUnit) | `tests/` |
| JDK 21 | .NET 10 SDK（`<TargetFramework>net10.0</TargetFramework>`） | |
| `application.yml` + 环境变量 | `appsettings.json` + 环境变量（`ConnectionStrings__Clinic` 双下划线表嵌套） | |
| JAR（字节码 + JVM） | DLL（IL + CLR），可发布为单文件/自包含 | |
| `jpackage` / Docker 镜像 | `dotnet publish` + Docker | `Dockerfile` |

**命令速查：**

```bash
dotnet build                # 编译（= mvn compile）
dotnet run --project backend # 运行（= mvn spring-boot:run）
dotnet test                 # 跑测试（= mvn test）
dotnet ef migrations add X  # 生成数据库迁移（≈ Flyway/Liquibase 手写 SQL 的自动化版）
dotnet ef database update   # 应用迁移
dotnet restore              # 还原 NuGet 依赖（首次拉代码后）
```

**Java 与 C# 的一眼差异：**

- 方法名大驼峰：`getUserById` → `GetUserById`（PascalCase，C# 约定）
- 命名空间即包：`package com.foo;` → `namespace ClinicFlow.Scheduling;`（文件作用域写法，免一层缩进）
- `import` → `using`（注意 `using` 在 C# 还有"释放资源"的第二含义，见 2.9）

---

## 2. C# 语言速成

### 2.1 属性（Property）—— 取代 getter/setter

Java 要写 `getName()/setName()`；C# 把"字段 + 访问器"合成属性：

```csharp
// backend/ClinicDb.cs — Patient 实体
public class Patient
{
    public int Id { get; set; }            // 自动属性：编译器生成隐藏字段 + get/set
    public string Name { get; set; } = ""; // = "" 是属性初始化器，避免 null
    public string? CompletedBy { get; set; } // string? = 可空引用（见 2.3）
}
```

- `public string Code { get; }` —— 只读属性（只有 getter），见 `Models.cs` 的 `BusinessException`。
- 调用方写法像字段：`patient.Name = "x"`，实际是方法调用，以后可加逻辑不改签名。
- `=> Set<Appointment>()` 是**表达式体属性**：每次访问都执行一次方法体，不是缓存的字段。见 `ClinicDb.cs` 的 `Appointments`。

### 2.2 record —— 不可变值对象（对照 Java 16+ record）

```csharp
// backend/Scheduling/Models.cs
public record Booking(int PatientId, int ResourceId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
```

这一行 = 不可变类 + 4 个只读属性 + 构造器 + `Equals/GetHashCode`（按值比较）+ `ToString`。和 Java record 几乎一样。

**`with` 表达式**（Java 没有）：基于原对象生成"改了几字段"的副本：

```csharp
// SchedulingService.Create 中：把时间统一转 UTC 后再处理
input with { StartUtc = input.StartUtc.ToUniversalTime(), EndUtc = ... }
```

**项目约定**：入站 DTO（`Booking`/`Mutation`）用 record（值语义、不可变）；数据库实体（`Appointment`）用可变 class（EF 变更跟踪需要 set）。不要机械照搬 JPA 实体全用 record。

### 2.3 可空引用类型（Nullable Reference Types）

C# 8 起编译器做静态空分析（类似 Kotlin 的空安全，但是编译期警告级别）：

| 写法 | 含义 | 本项目实例 |
|---|---|---|
| `string` | 不可为 null（赋值 null 会警告） | `Appointment.Status` |
| `string?` | 可为 null | `PrerequisiteTask.CompletedBy` |
| `int?` / `DateTime?` | 可空值类型（= Java 的 `Integer`，但无装箱开销，是 `Nullable<int>` struct） | `Mutation.ResourceId` |
| `x!` | 空断言："我保证非空，别警告"（**不生成运行时代码**） | `ctx.User.Identity!.Name!` |
| `x ?? y` | null 合并：x 为 null 则取 y（≈ `Optional.orElse`） | `page ?? 1` |
| `x?.Y` | 安全导航（≈ `Optional.map`） | `ctx.Connection.RemoteIpAddress?.ToString()` |
| `is null` / `is not null` | 模式匹配判空 | `if (status is not null)` |

注意：`!` 只是对编译器说话，运行时不判空——JSON 反序列化进来的对象仍要做业务校验。

### 2.4 主构造函数（C# 12）

```csharp
// backend/ClinicDb.cs
public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options) { ... }

// backend/Scheduling/SchedulingService.cs
public class SchedulingService(ClinicDb db, ITransactionProbe probe) { ... }
```

参数直接写在类名后，类体内可当字段用（`db`、`probe`）。等价于 Java 里手写"构造器注入 + 赋值给 final 字段"。配合 DI 容器天然契合（对照 Spring 的构造器注入，连 `@Autowired` 都不用写）。

### 2.5 var、目标类型 new、集合表达式

```csharp
var builder = WebApplication.CreateBuilder(args);  // var = 编译器推断类型（≠ 动态类型）
db.Users.Add(new() { Id = "x", Role = "Admin" });  // 目标类型 new()：类型由上下文推出
string[] roles = ["Scheduler", "TaskOperator"];     // C# 12 集合表达式（≈ List.of 但编译期）
await db.Users.FindAsync([input.Username ?? ""]);   // 这里 [..] 是 object[] 主键参数
```

### 2.6 switch 表达式与模式匹配

```csharp
// FhirAdapter.cs —— switch 表达式：是"值"不是语句（≈ Java 21 switch 表达式）
var status = a.Status switch
{
    "Pending" => "pending",
    "Confirmed" => "booked",
    "Cancelled" => "cancelled",
    _ => throw new ArgumentException("..."),   // _ = default 分支
};

// 属性模式 / 关系模式（SchedulingService.Execute 的 catch 过滤器中）：
ex is MySqlException { Number: 1213 or 1205 or 1062 }
// 读作：ex 是 MySqlException 且 Number 为 1213/1205/1062 之一
```

### 2.7 LINQ —— C# 版 Stream API，但更强

```csharp
// Endpoints.cs
var q = db.Appointments.AsNoTracking().AsQueryable();
if (status is not null) q = q.Where(x => x.Status == status);   // 条件拼接，延迟执行
var total = await q.CountAsync(ct);                              // 这里才真正发 SQL
var items = await q.OrderByDescending(x => x.StartUtc).Skip(...).Take(s).ToListAsync(ct);
```

关键区别（对比 Java Stream）：

- `IEnumerable<T>`：内存集合的 LINQ（≈ Stream），立即逐条求值。
- `IQueryable<T>`：查询被构建成**表达式树**交给 provider（EF Core）翻译成 SQL。`Where/OrderBy` 只是拼表达式，`ToListAsync/CountAsync/SingleAsync` 才是执行边界。
- 因此可以在 `if` 里条件拼接 `Where`——这在 Java Stream 里很难做到（Stream 不可复用、不可分步拼接）。
- `slots.Contains(x.SlotStartUtc)`（`CheckFree` 中）会被翻译成 SQL `IN (...)`。

### 2.8 匿名类型与字符串内插

```csharp
new { code = ex.Code, message = ex.Message, correlationId = ctx.TraceIdentifier }
// 匿名类型：编译器生成只读类，常用于 JSON 响应。≈ 一次性 Map.of(...) 但有静态类型。

$"SELECT * FROM Appointments WHERE Id={id} FOR UPDATE"
// $"" 字符串内插（≈ Java STR."..." 模板）。配合 FromSqlInterpolated 时参数会被自动参数化防注入。
```

### 2.9 using / await using —— try-with-resources 对应物

```csharp
await using var tx = await db.Database.BeginTransactionAsync(...);
```

- `IDisposable`（≈ `AutoCloseable`）：`using var x = ...;` 离开作用域自动 `Dispose()`。
- `IAsyncDisposable`（异步释放）：`await using`，事务/连接的释放可能是 IO，需要异步。
- 与 Java `try (var tx = ...)` 的差异：`using var` 声明到方法/块结束自动释放，不用显式 try 块；异常离开同样释放。
- 同一文件里 `using System.Text;`（导入）与 `using var`（释放）是两个完全不同的语言特性。

### 2.10 扩展方法

```csharp
// Scheduling/Endpoints.cs
public static void MapScheduling(this WebApplication app) { ... }
// 调用：app.MapScheduling(); —— 像给 WebApplication 加了成员方法
// ≈ Java 的静态工具方法 + 更顺眼的调用语法。ASP.NET/EF 的流式 API 全靠它。
```

### 2.11 其他高频语法点

| 语法 | 说明 | 实例 |
|---|---|---|
| `static class` | 全是静态成员、不能实例化（≈ Java 工具类私有构造器） | `Rules`、`Endpoints` |
| `Math.Clamp(v, lo, hi)` | 值域截断 | 分页参数 |
| `TimeSpan.FromMinutes(15)` | 时长类型（= `Duration`） | 限流窗口 |
| `DateTime.UtcNow` / `DateTimeOffset` | `Instant` / `OffsetDateTime` 对应物；项目持久化 UTC `DateTime` | `Appointment.StartUtc` |
| `Guid.NewGuid().ToString()` | `UUID.randomUUID()` | 实体 Id |
| `Convert.ToHexString(SHA256.HashData(...))` | 哈希 | 幂等键 |
| 文件作用域 namespace | `namespace X;` 顶格写，全文件生效 | 所有 .cs |
| 顶层语句（Top-level statements） | `Program.cs` 免 `Main` 方法，文件体即入口 | `Program.cs` |
| `public partial class Program;` | partial 类：供 WebApplicationFactory 测试找到入口类型 | `Program.cs` 末行 |

---

## 3. ASP.NET Core 服务端核心

### 3.1 应用启动模型（对照 Spring Boot）

```csharp
var builder = WebApplication.CreateBuilder(args);  // ≈ SpringApplication.run 前的准备
builder.Services.AddDbContext<ClinicDb>(...);      // 注册服务到内置 DI 容器
var app = builder.Build();                          // 容器定型
app.UseAuthentication();                            // 装中间件（顺序即执行顺序）
app.MapGet("/api/health", ...);                     // 路由
app.Run();                                          // 启动 Kestrel 监听
```

- 没有 `@SpringBootApplication`、没有包扫描——一切注册都是**显式代码**。
- `Program.cs` 就是全部装配代码，没有 `web.xml`/`DispatcherServlet` 概念。
- 内置 Web 服务器 Kestrel（≈ 内嵌 Tomcat），默认直接对外，前面可放 Nginx。

### 3.2 内置依赖注入（对照 Spring IoC）

```csharp
builder.Services.AddScoped<SchedulingService>();                        // 每请求一个实例
builder.Services.AddSingleton<ITransactionProbe, NoTransactionProbe>(); // 全局单例
builder.Services.AddSingleton(TimeProvider.System);                     // 注册现成实例
builder.Services.AddHttpClient<Dispatcher>(...);                        // HttpClient 工厂模式
```

| 生命周期 | 含义 | Spring 对应 | 本项目 |
|---|---|---|---|
| `Singleton` | 全应用一个实例 | `@Scope("singleton")`（默认） | `ITransactionProbe`、`TimeProvider` |
| `Scoped` | 每个请求作用域一个 | `@RequestScope` | `DbContext`、`SchedulingService` |
| `Transient` | 每次注入新建 | `@Scope("prototype")` | — |

**铁律（本项目就有体现）**：Scoped 的 `DbContext` 不能被 Singleton/后台服务长期持有。后台 `OutboxWorker` 每轮 `CreateAsyncScope()` 取一个新的 `Dispatcher`（其内部持有 Scoped `ClinicDb`），用完即释放——对照 Spring 里在异步线程里用 `ObjectProvider`/新建 scope。

注入方式只有一种：构造器注入。不需要注解，容器按构造器参数类型自动解析。

### 3.3 Minimal API（对照 Spring MVC）

本项目用 Minimal API（.NET 6+），不写 Controller：

```csharp
group.MapPost("/appointments",
    async (Booking input, SchedulingService service, HttpContext ctx, CancellationToken ct) =>
        await service.Create(input, ...))
    .RequireAuthorization("schedule");
```

参数来源按类型/名字自动绑定：

| 参数 | 来源 | Spring 对应 |
|---|---|---|
| `Booking input`（record/类） | JSON body | `@RequestBody` |
| `string id` | 路由占位 `{id}` | `@PathVariable` |
| `int? page, string? status` | query string | `@RequestParam` |
| `ClinicDb db, SchedulingService service` | DI 容器 | 构造器注入的 bean |
| `HttpContext ctx` | 当前请求上下文 | `HttpServletRequest/Response` 合体 |
| `CancellationToken ct` | 客户端断开/超时信号 | 无直接对应（≈ 请求取消回调） |
| `{id:int}` | 路由约束：非 int 直接 404 | `@PathVariable int`（自动转换） |

返回值约定：返回对象 → 自动 JSON 序列化 200；`Results.Ok/NotFound/NoContent/Json(...)` → 显式控制（≈ `ResponseEntity`）。抛 `BusinessException` → 由全局异常中间件统一映射（见 3.4），≈ `@ControllerAdvice + @ExceptionHandler`。

**端点元数据**：`.RequireAuthorization("schedule")`、`.RequireRateLimiting("login")`、`.ExcludeFromDescription()` 跟在映射后链式声明，≈ `@PreAuthorize` 等注解，但是显式代码。

### 3.4 中间件管道（对照 Servlet Filter / Interceptor）

`Program.cs` 中 `app.Use(...)` 的顺序就是请求流经的顺序：

```
请求 → 关联ID/安全头 → 全局异常映射 → Authentication → Authorization → RateLimiter → CSRF校验 → 端点
```

```csharp
app.Use(async (ctx, next) => { /* 前 */ await next(); /* 后 */ });
```

- `await next()` 把请求交给下一个中间件；不调用 = 短路（≈ Filter 里不调 `chain.doFilter`）。
- 异常中间件写在最外层：内层任何异常在这里被捕获并映射为统一错误 JSON（业务 409 / 参数 400 / 未知 500）。
- `catch (Exception ex) when (ex is not OperationCanceledException)` —— **异常过滤器** `when`：Java 没有，表达"只捕获满足条件的异常"。

### 3.5 认证与授权（对照 Spring Security）

- **认证**：Cookie 方案（`AddAuthentication().AddCookie(...)`）。登录时 `ctx.SignInAsync(...)` 写入加密 Cookie；之后每个请求由认证中间件还原出 `ClaimsPrincipal`（≈ `SecurityContextHolder.getContext().getAuthentication()`）。
- **Claims**：`ClaimsIdentity` 里放 `NameIdentifier/Name/Role` 三条 claim（≈ `UserDetails` 的 username + authorities）。业务代码用 `ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)` 取。
- **授权策略**：

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("schedule", p => p.RequireRole("Scheduler"))   // 命名策略 = 角色要求
    .AddPolicy("admin", p => p.RequireRole("Admin"));
// 端点上 .RequireAuthorization("schedule") ≈ @PreAuthorize("hasRole('Scheduler')")
```

- API 风格改造：`OnRedirectToLogin` 返回 401 而非 302 跳登录页（Spring 里要配 `AuthenticationEntryPoint`）。
- CSRF：Cookie 会话必须防 CSRF。`AddAntiforgery` + 自定义中间件对写操作校验 `X-CSRF-TOKEN` 头（≈ Spring Security 的 CsrfFilter，这里显式编排）。
- 限流：`AddRateLimiter` 固定窗口按 IP 限制登录接口（≈ Bucket4j/Resilience4j）。

### 3.6 配置系统

```csharp
builder.Configuration.GetConnectionString("Clinic")   // 读 ConnectionStrings:Clinic
builder.Configuration["Integration:Enabled"]           // 读 Integration:Enabled
config["INTEGRATION_TOKEN"]                            // 环境变量直接读
```

来源优先级（后者覆盖前者）：`appsettings.json` → `appsettings.{Env}.json` → 环境变量 → 命令行。环境变量里双下划线表嵌套：`ConnectionStrings__Clinic=Server=...`。

### 3.7 后台服务（对照 @Scheduled / 独立线程）

```csharp
builder.Services.AddHostedService<OutboxWorker>();   // 随应用启停的后台任务

public class OutboxWorker(...) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) { ... }
    }
}
```

- `BackgroundService` 由宿主管理生命周期：应用关闭时取消 `stoppingToken`（≈ 优雅停机钩子）。
- 每轮用 `IServiceScopeFactory.CreateAsyncScope()` 开新作用域取 Scoped 依赖——这是后台服务消费 `DbContext` 的标准姿势。

### 3.8 OpenAPI

`Microsoft.AspNetCore.OpenApi` 从端点签名自动生成 OpenAPI 文档（≈ springdoc-openapi）。`ApiDocumentation.cs` 用 `AddOperationTransformer` 统一补充说明和 `Idempotency-Key`/`X-CSRF-TOKEN` 头参数——因为这两来自中间件/手写逻辑，签名里看不到。

---

## 4. EF Core 数据访问

### 4.1 DbContext ≈ EntityManager + Unit of Work

```csharp
public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();  // ≈ 表的入口
    protected override void OnModelCreating(ModelBuilder b) { ... } // ≈ JPA 注解映射，这里用 Fluent API
}
```

| JPA/Hibernate | EF Core | 本项目实例 |
|---|---|---|
| `@Entity` + `@Id` | 约定：类名/Id 属性即映射；Fluent API 显式覆盖 | `HasMaxLength(36)`、`HasIndex(...)` |
| `EntityManager.persist` | `db.Appointments.Add(a)` | 创建预约 |
| `em.remove` | `db.SlotClaims.RemoveRange(...)` | 改期先删旧槽位 |
| `em.flush` | `db.SaveChangesAsync(ct)` | **生成并执行 SQL，但事务未必提交** |
| `@Transactional` | 显式 `BeginTransactionAsync` + `CommitAsync` | `SchedulingService.Execute` |
| `@Version` 乐观锁 | `.IsConcurrencyToken()` | `Appointment.Version` |
| `@OneToMany` 等关系注解 | `HasOne<Patient>().WithMany().HasForeignKey(...)` | 预约-患者外键 |
| `EntityManager` 只读查询 | `.AsNoTracking()` | 所有 GET 端点 |
| Flyway/Liquibase | `dotnet ef migrations` + `MigrateAsync()` | `backend/Migrations/` |
| 复合主键 `@IdClass` | `HasKey(x => new { x.ResourceId, x.SlotStartUtc })` | `SlotClaim` |
| 种子数据 | `HasData(...)` | 内置患者/资源 |

### 4.2 变更跟踪（Change Tracking）

- 从 `DbSet` 查出的实体默认被跟踪；改属性后 `SaveChangesAsync` 自动生成 `UPDATE`（≈ JPA 脏检查）。
- 只读场景加 `AsNoTracking()` 省开销、避免意外写回。
- 跨请求改同一实体：`Mutate` 里先用 `AsNoTracking` 预读做业务校验，再 `FOR UPDATE` 加锁重读（被跟踪）——**预读不是锁**，这是本项目的并发设计核心。
- `db.ChangeTracker.Clear()`（Dispatcher 中）：长存对象复用 DbContext 时清空跟踪，防止内存膨胀和状态污染。

### 4.3 事务与锁

```csharp
await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
// ... 多次 SaveChangesAsync（= flush，未提交）
await tx.CommitAsync(ct);   // 才真正提交；异常离开 using 作用域自动回滚
```

与 `@Transactional` 的差异：本项目**显式编排**——先插幂等记录拿唯一键锁 → 行锁重读 → 业务校验 → flush → commit。`Mutate` 改期时"先 flush 删除槽位、再插入新槽位"利用的是：同一事务内 flush 让删除对后续唯一性检查生效，但整体仍未提交，异常即整体回滚。

原生 SQL 混合 LINQ：

```csharp
db.Appointments.FromSqlInterpolated($"SELECT * FROM Appointments WHERE Id={id} FOR UPDATE")
// FromSqlInterpolated：内插值自动参数化（防注入），结果仍映射为实体并跟踪。
// ≈ JPA nativeQuery 但类型安全。MySQL FOR UPDATE / FOR UPDATE SKIP LOCKED 都靠它。
```

### 4.4 并发异常的统一翻译

```csharp
catch (Exception ex) when (ex is DbUpdateConcurrencyException
    || ex is MySqlException { Number: 1213 or 1205 or 1062 }   // 死锁/锁等待超时/唯一键冲突
    || ex.InnerException is MySqlException { Number: 1213 or 1205 or 1062 })
{ await tx.RollbackAsync(ct); throw new BusinessException("concurrent_conflict", ...); }
```

把数据库层的并发信号统一转成业务 409——对照 Spring 的 `DataAccessException` 翻译体系，这里显式做，因为要精确区分"可重试的并发冲突"与"业务冲突"。

### 4.5 Migrations

`backend/Migrations/` 由 `dotnet ef migrations add Xxx` 生成：`Xxx.cs`（Up/Down）+ `Xxx.Designer.cs`（快照）+ `ClinicDbModelSnapshot.cs`（当前模型全量快照）。启动时 `db.Database.MigrateAsync()` 自动应用（本项目的选择；生产常用独立步骤）。`DesignFactory.cs` 给 dotnet-ef 工具提供设计时 DbContext（工具不走 `Program.cs` 的完整启动）。

---

## 5. 异步编程

### 5.1 Task ≈ CompletableFuture，但语法内建

```csharp
public async Task<Appointment> Create(...)   // Task<T> = 异步结果的句柄（≈ CompletableFuture<T>）
{
    var slots = Rules.Slots(...);                 // 同步代码照写
    await LockResources([input.ResourceId], ct);  // await = thenCompose 的语法糖，但不占线程
    return a;
}
```

| Java | C# |
|---|---|
| `CompletableFuture<T>` | `Task<T>`（无返回值用 `Task`） |
| `.thenApply/.thenCompose` 链式 | `await` 顺序书写，代码看起来像同步 |
| 方法返回已包装的 future | 标 `async` 的方法，编译器生成状态机 |
| `future.join()`（阻塞） | `task.Result`/`.Wait()`（**服务端禁用，易死锁**） |

**心智模型**：`await` 一个 IO 时，当前线程被释放回线程池；IO 完成后由任意线程续跑。**没有新开线程**，也不要给 IO 套 `Task.Run`（那是 CPU 密集任务的卸载手段）。

### 5.2 CancellationToken —— 协作式取消

每个端点/后台循环都收 `CancellationToken ct` 并一路传给 EF/HttpClient/`Task.Delay`：客户端断开或应用停机时，整条链一起取消，半途操作抛 `OperationCanceledException`。本项目特意**不**把取消映射成 500（见全局异常过滤器的 `when` 子句），Dispatcher 也用"租约过期"代替"猜测 HTTP 结果"。

### 5.3 常见陷阱（对照 Java 经验）

- 一个 `DbContext` 不能并发跑两个 `await` 查询（≈ 一个 `EntityManager` 非线程安全）。
- 后台单例不能持有 Scoped `DbContext`——每轮新建 scope（`OutboxWorker` 示范）。
- 不要用 `Thread.Sleep` 模拟并发；测试里用 `TaskCompletionSource` 做受控屏障（`ITransactionProbe` 就是为此预留的接缝）。

---

## 6. ClinicFlow 项目导读

> 细读路线见 `docs/java-reading-guide.md`；这里按"知识点 → 文件"索引，帮你带着语法问题找到实例。

| 想学的点 | 文件 | 看什么 |
|---|---|---|
| 应用装配、DI、中间件顺序 | `backend/Program.cs` | `builder.Services.*` 注册区 + `app.Use*` 管道区 |
| Minimal API、参数绑定、LINQ 分页 | `backend/Scheduling/Endpoints.cs` | `MapPost/MapGet`、`int?` 可选参数、条件拼接 `Where` |
| 实体映射、Fluent API、种子数据 | `backend/ClinicDb.cs` | `OnModelCreating`、复合键、并发令牌 |
| record vs class、可空、静态规则类 | `backend/Scheduling/Models.cs` | `Booking/Mutation` record、`Rules.Slots` |
| 显式事务、行锁、幂等、异常过滤器 | `backend/Scheduling/SchedulingService.cs` | `Execute` 模板方法、`FOR UPDATE`、`catch ... when` |
| 后台服务、HttpClient、租约 | `backend/Integration/Dispatcher.cs` | `OutboxWorker : BackgroundService`、`CreateAsyncScope` |
| Cookie 认证、Claims、密码哈希 | `backend/Identity.cs` | `SignInAsync`、`ClaimsIdentity`、`PasswordHasher` |
| 匿名类型构造 JSON、switch 表达式 | `backend/Fhir/FhirAdapter.cs` | FHIR 资源组装、`Status switch` |
| 自定义 JSON 转换器 | `backend/UtcDateTimeConverter.cs` | `JsonConverter<DateTime>`（≈ Jackson `JsonSerializer`） |
| OpenAPI 定制 | `backend/ApiDocumentation.cs` | 操作转换器补 header 参数 |
| EF 设计时工厂 | `backend/DesignFactory.cs` | `IDesignTimeDbContextFactory` |
| 单元/集成测试（xUnit） | `tests/*.cs` | `[Fact]`、真实 MySQL 事务测试 |

**建议的三天速成路径（每天 2–3 小时）：**

1. **D1 语法日**：读本手册第 2 章 → 对照 `Models.cs`、`Endpoints.cs` 找每个语法点的实例 → 给 `Rules.Slots` 加一条校验（如最长 2 小时），`dotnet build` 通过。
2. **D2 框架日**：读第 3、4 章 → 对照 `Program.cs` 画中间件顺序图 → 给 `GET /api/appointments` 加一个 `patientId` 过滤参数（练 LINQ 条件拼接 + 参数绑定）。
3. **D3 并发日**：读第 5 章 + `SchedulingService.Execute` → 用两个并发请求改同一预约，观察 409 → 读 `Dispatcher.ClaimNext` 理解 `FOR UPDATE SKIP LOCKED` 租约。

---

## 7. 速查表

```csharp
// —— 类型与成员 ——
public class Foo { public int Id { get; set; } }           // 类 + 自动属性
public record Bar(int X, string? Y);                        // 不可变值对象
public record Baz(string A) { public int B { get; init; } } // init = 只能初始化时赋值
public static class Util { public static int F() => 42; }   // 静态类 + 表达式体方法

// —— 空处理 ——
string? maybe = null;
var len = maybe?.Length ?? 0;        // 安全导航 + null 合并
var sure = maybe!;                    // 空断言（仅编译期）
if (maybe is { Length: > 3 }) { }    // 属性模式

// —— 集合与 LINQ ——
var list = new List<int> { 1, 2, 3 };
int[] arr = [1, 2, 3];                // 集合表达式
list.Where(x => x > 1).Select(x => x * 2).ToList();
list.Any(x => x == 2); list.FirstOrDefault(x => x == 9);   // ≈ anyMatch / findFirst

// —— 异步 ——
async Task<int> GetAsync(CancellationToken ct)
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    var n = await db.Foos.CountAsync(ct);
    await tx.CommitAsync(ct);
    return n;
}

// —— 杂项 ——
var (a, b) = (1, "x");                // 元组解构
_ = int.TryParse("5", out var v);     // out 参数内联声明；_ 丢弃
nameof(Appointment.Id)                // 编译期取名字符串
$"id={id}, when={DateTime.UtcNow:O}"  // 内插 + 格式
```

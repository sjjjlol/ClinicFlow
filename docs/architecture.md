# OIS 架构

浏览器 → 同源 ASP.NET Core Controller → OisService → OisWorkflow → MySQL。

后端保留 .NET 10 / ASP.NET Core，使用显式 `Program.Main`、`Startup` 和 MVC Controller。语法采用 .NET 5 时期的 C# 9 上限，通过 `Directory.Build.props` 的 `LangVersion=9.0` 强制检查；运行时和目标框架仍为 `net10.0`。关闭隐式 global using，源码显式声明 using。

- `frontend/src/OisApp.tsx`：三个角色工作台、表单预览确认、待办、历史和执行。
- `frontend/src/OisCalendar.tsx`：日/周日历、患者预约及机器占用。
- `backend/Program.cs`：创建宿主，在独立作用域内迁移数据库、初始化演示账号，然后启动服务。
- `backend/Startup.cs`：`ConfigureServices` 注册 DI、Cookie 认证、授权策略、登录限流、MVC JSON 和 OpenAPI；`Configure` 装配请求管道和端点。
- `backend/Controllers/AuthController.cs`：登录、退出、当前用户和 CSRF 令牌；登录输入为 `Contracts/LoginRequest.cs`。
- `backend/Controllers/OisController.cs`：工作区快照和业务命令，通过构造函数注入 OisService，显式绑定路由及请求体，每个业务操作有独立 Action（如 Book、Cancel、Prescribe），通过私有 ExecuteCommand 共用调用逻辑，返回 IActionResult。
- `backend/Controllers/HealthController.cs`：数据库健康查询。
- `backend/Controllers/FallbackController.cs`：未知 API 和页面的 404，不显示在 OpenAPI 中；根页面由静态文件及文件回退端点处理。
- `backend/Middleware/`：响应头、业务及系统异常映射、写请求 CSRF 校验。
- `backend/Identity.cs`：演示账号初始化、允许的工作人员角色和 Cookie 登录凭据。
- `backend/Ois/Workflow.cs`：确定性领域规则、角色约束、冲突检测、计划放行、状态转换。
- `backend/Ois/Service.cs`：一致性快照、写入事务、幂等响应和审计。
- `backend/ClinicDb.cs` 与 `backend/Ois/Models.cs`：当前持久化模型。

所有写命令先锁定 OisGate 单行，再读取状态并执行规则；领域变更、待办、审计和幂等结果同事务提交。读取快照用 RepeatableRead。客户端传目标版本，开始治疗还传已核对的计划 id，防止版本被静默替换。

部署只包含应用和 MySQL。前端由 ASP.NET 静态托管；开发时 Vite 代理 API。没有外部同步、影像服务器或模型服务运行依赖。

这是小规模演示设计：全量快照和单行串行写入不适用于生产规模；附件只是外部编号/引用，不上传、不解析。

## 请求与兼容性

中间件顺序为：响应头 → 异常处理 → 默认文件/静态文件 → 路由 → 认证 → 授权 → 登录限流 → CSRF → Controller / OpenAPI / 根页面回退。

Controller 使用 `[ApiController]`、`[Route]`、`[HttpGet]` / `[HttpPost]`，OIS Controller 使用 `[Authorize(Policy = "ois")]`；退出及当前用户接口要求登录，登录接口使用 `[EnableRateLimiting("login")]`。业务角色限制仍由 Workflow 校验。

接口路径及响应字段保持兼容：`/api/auth/*`、`/api/ois/workspace`、`/api/ois/commands/{operation}`、`/api/health`，以及受授权保护的 `/api/openapi/v1.json`。已支持操作的实际 URL 不变，例如 `/api/ois/commands/book`。OpenAPI 逐个列出 21 个命令路径及请求体，不再展示通用 `{operation}` 分发入口；未支持操作由隐藏的 UnknownCommand 返回 `invalid_operation`。写请求仍需要 `X-CSRF-TOKEN`，业务命令还需要 `Idempotency-Key`。

MVC 的 `AddJsonOptions` 注册 UtcDateTimeConverter，保持 camelCase 与 UTC `Z` 日期输出；中间件的 `WriteAsJsonAsync` 使用单独配置的 HTTP JSON 选项。`[ApiController]` 的模型绑定失败统一返回 400 和 `{ code: "invalid_request", message, correlationId }`。关闭非空引用类型的隐式 Required 校验，以保留 Service / Workflow 对缺省字段、显式 null 和业务规则的原有处理。

数据库模型、迁移、事务、并发与幂等算法未调整。新增业务接口时将 HTTP 绑定放进 Controller，将事务与规则保留在 Service / Workflow。

验证命令：`./scripts/dotnet.sh build ClinicFlow.sln --no-restore`、`./scripts/test.sh --no-restore`，启动新版本 API 后执行 `./scripts/http-tests.sh`。HTTP 测试包含认证、OIS 业务及 Controller 的模型绑定、UTC 序列化、404、OpenAPI 回归；设置 `VERIFY_LOGIN_RATE_LIMIT=1` 可额外验证登录限流。

## 后端代码风格

- 全部手写 C#（后端、测试和升级工具）采用块命名空间、普通类、显式构造函数和完整的对象创建类型；DTO 使用属性初始化器。
- 不使用 record / with、主构造函数、顶层语句、目标类型 new()、文件作用域命名空间或集合表达式。条件分支优先使用普通 if / switch 和显式比较。
- 保留 async/await、LINQ、泛型、Nullable、简单表达式体成员和 await using，保持异步、类型检查及资源释放行为。
- 保留 .NET 10 提供的 TimeProvider、限流及 OpenAPI API；语言风格约束不代表运行时依赖回退到 .NET 5。
- OisCommand 的属性顺序和默认值与原 record 保持一致，尤其 Available 默认为 true，避免改变持久化幂等指纹。升级演练验证历史请求可重放且不会重复写入。
- 历史 EF 迁移和快照不重写；它们随整个工程在 C# 9 下编译验证。

`Directory.Build.targets` 只移除 Microsoft.AspNetCore.OpenApi 的 XML 注释生成器，因为它自动生成 C# 11+ 文件类型。现有文档由运行时 ApiExplorer / ApiDocumentation 生成；仍保留其他编译分析器。Docker 构建也复制此配置文件。

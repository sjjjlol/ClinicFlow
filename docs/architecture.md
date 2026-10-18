# OIS 架构

浏览器 → 资源 Controller（专用请求类型）→ CommandExecutor（事务）→ 对应 Operations（业务规则）→ MySQL。

查询由 WorkspaceController → WorkspaceQuery 返回具名 WorkspaceResponse，保持原有聚合快照、首次加载、成功写入后刷新和 30 秒轮询。

后端保留 .NET 10 / ASP.NET Core，入口使用 `WebApplication.CreateBuilder` 和顶层语句，HTTP API 使用 MVC Controller。语法采用 .NET 5 时期的 C# 9 上限，通过 `Directory.Build.props` 的 `LangVersion=9.0` 强制检查；运行时和目标框架仍为 `net10.0`。关闭隐式 global using，源码显式声明 using。

- `frontend/src/OisApp.tsx`：三个角色工作台、表单预览确认、待办、历史和执行。
- `frontend/src/OisCalendar.tsx`：日/周日历、患者预约及机器占用。
- `backend/Program.cs`：创建宿主，在独立作用域内迁移数据库、初始化演示账号，然后启动服务。
- `backend/Hosting/ServiceCollectionExtensions.cs`：`AddClinicServices` 注册 DI、Cookie 认证、授权策略、登录限流、MVC JSON 和 OpenAPI。
- `backend/Hosting/WebApplicationExtensions.cs`：`UseClinicMiddleware` 装配中间件；`MapClinicEndpoints` 映射 Controller、OpenAPI 和根页面回退。
- `backend/Controllers/AuthController.cs`：登录、退出、当前用户和 CSRF 令牌；登录输入为 `Contracts/LoginRequest.cs`。
- `backend/Controllers/`：Cases、Prescriptions、Plans、Reservations、TreatmentDeliveries、Machines、Tasks 按业务资源拆分；Workspace 独立聚合查询。抽象 OisCommandController 只复用身份、请求头和事务调用，没有业务分发。
- `backend/Controllers/HealthController.cs`：数据库健康查询。
- `backend/Controllers/FallbackController.cs`：未知 API 和页面的 404，不显示在 OpenAPI 中；根页面由静态文件及文件回退端点处理。
- `backend/Middleware/`：响应头、业务及系统异常映射、写请求 CSRF 校验。
- `backend/Identity.cs`：演示账号初始化、允许的工作人员角色和 Cookie 登录凭据。
- `backend/Ois/Operations/`：专用请求直接调用明确业务方法；门诊、实验独立处理，预约恢复与改期共用排期不变量，处方/计划共用草稿持久化规则。
- `backend/Ois/OisState.cs`：事务内状态和跨资源不变量，不再有按操作字符串分发的 Apply。
- `backend/Ois/Requests/`：每个操作独立请求类型；目标 ID 由路由赋值，正文不能覆盖。四种预约分别定义创建请求。
- `backend/Ois/CommandExecutor.cs`：同一事务中的锁、幂等、状态加载、业务委托、持久化和审计。
- `backend/Ois/Compatibility/`：冻结旧字段顺序、默认值及审计操作名称，只用于历史存储兼容，不作为 HTTP 或业务处理契约。
- `backend/Hosting/DatabaseInitialization.cs`：在独立异步作用域中迁移与初始化，保持既有启动行为。
- `backend/ClinicDb.cs` 与 `backend/Ois/Models.cs`：当前持久化模型。

所有写命令先锁定 OisGate 单行，再读取状态并执行规则；领域变更、待办、审计和幂等结果同事务提交。读取快照用 RepeatableRead。客户端传目标版本，开始治疗还传已核对的计划 id，防止版本被静默替换。

部署只包含应用和 MySQL。前端由 ASP.NET 静态托管；开发时 Vite 代理 API。没有外部同步、影像服务器或模型服务运行依赖。

这是小规模演示设计：全量快照和单行串行写入不适用于生产规模；附件只是外部编号/引用，不上传、不解析。

## 请求与兼容性

中间件顺序为：响应头 → 异常处理 → 默认文件/静态文件 → 路由 → 认证 → 授权 → 登录限流 → CSRF → Controller / OpenAPI / 根页面回退。

Controller 使用 `[ApiController]`、`[Route]`、`[HttpGet]` / `[HttpPost]`，OIS Controller 使用 `[Authorize(Policy = "ois")]`；退出及当前用户接口要求登录，登录接口使用 `[EnableRateLimiting("login")]`。业务角色限制由对应 Operations 校验；取消等操作继续按资源实际类型判定角色。

业务路径改为资源和明确动作，例如 `POST /api/ois/plans/{id}/review`、`POST /api/ois/reservations/{id}/experiment-results`；机器可用性和草稿使用 PUT。旧 `/api/ois/commands/*` 已删除并返回 404，前后端必须同步升级。认证、健康、workspace 和 OpenAPI 路径保持不变。写请求需要 CSRF，业务写操作还需要幂等键；OpenAPI 按 `[Idempotent]` 元数据描述幂等头，不依赖路径前缀。完整路径见 [操作与接口](ois-runbook.md)。

MVC 的 `AddJsonOptions` 注册 UtcDateTimeConverter，保持 camelCase 与 UTC `Z` 日期输出；中间件的 `WriteAsJsonAsync` 使用单独配置的 HTTP JSON 选项。`[ApiController]` 的模型绑定失败统一返回 400 和 `{ code: "invalid_request", message, correlationId }`。关闭非空引用类型的隐式 Required 校验，以保留 CommandExecutor / Operations 对缺省字段、显式 null 和业务规则的原有处理。

数据库模型、历史迁移、事务、并发和历史幂等指纹算法保持不变。新增业务接口时将 HTTP 绑定放进 Controller，将事务与规则保留在 CommandExecutor / Operations。

验证命令：`./scripts/dotnet.sh build ClinicFlow.sln --no-restore`、`./scripts/test.sh --no-restore`，启动新版本 API 后执行 `./scripts/http-tests.sh`。HTTP 测试包含认证、OIS 业务及 Controller 的模型绑定、UTC 序列化、404、OpenAPI 回归；设置 `VERIFY_LOGIN_RATE_LIMIT=1` 可额外验证登录限流。

## 后端代码风格

- 入口 Program.cs 使用 C# 9 顶层语句；其余手写 C#（后端、测试和升级工具）采用块命名空间、普通类、显式构造函数和完整的对象创建类型；DTO 使用属性初始化器。
- 不使用 record / with、主构造函数、目标类型 new()、文件作用域命名空间或集合表达式。条件分支优先使用普通 if / switch 和显式比较。
- 保留 async/await、LINQ、泛型、Nullable、简单表达式体成员和 await using，保持异步、类型检查及资源释放行为。
- 保留 .NET 10 提供的 TimeProvider、限流及 OpenAPI API；语言风格约束不代表运行时依赖回退到 .NET 5。
- Compatibility/LegacyCommandRecord 的属性顺序和默认值与历史格式保持一致，尤其 Available 默认为 true，避免改变持久化幂等指纹。升级演练验证历史请求可重放且不会重复写入。
- 历史 EF 迁移和快照不重写；它们随整个工程在 C# 9 下编译验证。

`Directory.Build.targets` 只移除 Microsoft.AspNetCore.OpenApi 的 XML 注释生成器，因为它自动生成 C# 11+ 文件类型。现有文档由运行时 ApiExplorer / ApiDocumentation 生成；仍保留其他编译分析器。Docker 构建也复制此配置文件。

现代宿主入口只负责 CreateBuilder → 服务注册 → Build → 数据库初始化 → 中间件配置 → 端点映射 → RunAsync。服务注册仍由 IServiceCollection 管理；Controller、CommandExecutor、WorkspaceQuery 和 ClinicDb 继续通过构造函数注入。使用精简宿主不等于使用 Minimal API，本项目业务接口仍由 Controller 定义。

## 原库升级

启动仍执行 MigrateAsync 与 Identity.Seed，本次不新增迁移、不清库。新请求由兼容模块映射到冻结的历史指纹格式，因此相同账号、角色、key 和业务输入可跨升级重放；输入变化仍冲突。历史审计保留 action 与 detail.command.text，旧备注、更正直接可读；草稿 Payload 表单字段不重命名，原草稿可继续打开和提交。签署成功后仅删除当前账号的对应草稿。

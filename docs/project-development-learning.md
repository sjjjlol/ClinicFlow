# ClinicFlow：按项目开发顺序理解完整实现

本文面向有 Java / Spring Boot 基础、希望读懂本项目的开发者。以当前 OIS 代码为准，按功能之间的依赖顺序讲解需求、工程、数据库、业务规则、接口、前端和交付。每个阶段说明为什么需要它、源码如何实现、会给后续阶段提供什么能力；不安排动手任务。

C#、DI、Middleware 等语法与框架细节见 [ASP.NET Core 入门](aspnet-core-learning.md)，映射、迁移和 CRUD 细节见 [EF Core 专篇](ef-core-learning.md)。本文负责把这些知识放回真实业务链路。

## 1. 先区分真实演进历史与理解项目的顺序

这个仓库并不是从第一天就只有 OIS。下面是 Git 历史中的关键节点，可用提交短号定位设计变化：

| 节点 | 真实变化 | 对当前代码的意义 |
| --- | --- | --- |
| `f0a64f2` | 建立 .NET、React、MySQL 工程 | 形成前后端与数据库的基础运行方式 |
| `17dcf41` | 演示身份与工作台 | 形成工作人员登录基础 |
| `097b42f`～`b6f1547` | 预约、改期、并发和权限 | 早期探索了排期不变量及可靠写入 |
| `32f4efb` | 确认 OIS MVP 范围 | 将目标收敛到放疗协作流程 |
| `ebef476`、`7050e2c` | OIS 事务业务及角色工作台 | 建立现有系统的业务主线和界面 |
| `afb15d5` | 删除旧模块 | 当前运行范围只保留 OIS，历史迁移仍需保留 |
| `50bf3bb`、`c29ad94` | 角色工作区、结构化验证和失败跟进 | 从正常流程扩展到真实的协作与异常处理 |
| `ca0a0a0`～`30a76c5` | Controller、C# 9 风格与精简宿主 | 形成当前 ASP.NET Core 入口和编码风格 |
| `0ff496f` | 按资源拆分 API 与业务操作 | 形成 Requests、Operations、CommandExecutor；现已进一步按领域模块拆分 |

历史中也出现过集成、FHIR、AI 助手和影像模块。它们属于旧阶段，不能作为当前功能入口。旧迁移仍存在，是为了让已有数据库能够升级到当前模型。

后文按当前系统的依赖关系组织，不声称每个类都是按章节顺序提交的。比如当前资源 Controller 是后期重构结果，但理解一次请求时仍应从它进入业务层。

## 2. 先明确业务对象，再决定表和接口

项目首先要解决三个工作人员角色之间的协作，而不是单纯管理几张表。预约人员安排患者时间；诊疗人员负责诊疗、处方、计划审核、实验排期及治疗执行；物理师登记计划、签署验证结果并处理设备相关问题。具体职责见 [产品范围](ois-mvp.md) 与 [领域语言](../CONTEXT.md)。

这些职责决定了业务对象的分工：

| 对象 | 代表什么 | 为什么需要独立表达 |
| --- | --- | --- |
| Patient | 患者身份 | 同一患者可以关联不同诊疗档案和预约 |
| CareCase | 一次诊疗档案及当前处方、计划引用 | 为临床流程提供共同归属 |
| Prescription | 已签署的处方版本 | 修订需要保留旧内容和签署信息 |
| TreatmentPlan | 与处方关联的计划版本、审核和验证信息 | 放行必须针对确定的计划版本 |
| Reservation | 门诊、治疗、实验或维护的资源占用 | 排期解决时间冲突，不等于临床放行 |
| TreatmentDelivery | 一次治疗实际执行的记录 | 记录当时采用的计划与实际结果 |
| OisTask / OisAudit | 待处理工作 / 已发生操作的记录 | 待办推动协作，审计解释历史，两者用途不同 |

最关键的边界是：**有预约不等于可治疗**。预约可以先占用时段，但开始治疗时还要检查当前处方、计划审核、物理验证、机器可用性和身份核对。把这些都压成一个“已完成”字段，会丢失谁完成了哪一步以及当前还缺什么。

因此业务开发主线可以写成：

```text
诊疗档案 → 签署处方 → 登记计划 → 审核计划 → 实验排期 → 签署验证
                                       ↓
治疗预约 + 当前计划满足放行条件 + 身份核对 + 处于预约时段 → 开始治疗 → 记录结果
```

这张关系图表达依赖，不表示治疗预约必须等物理验证通过后才能创建。

## 3. 建立可启动的工程，再装配依赖与身份

源码入口是 [Program.cs](../backend/ClinicFlow.Api/Program.cs)。它只描述应用启动的大步骤：

```text
CreateBuilder
  → AddClinicServices：注册依赖和框架能力
  → Build：建立应用
  → InitializeClinicDatabaseAsync：迁移与演示数据初始化
  → UseClinicMiddleware：配置请求管道
  → MapClinicEndpoints：映射 Controller 等端点
  → RunAsync：接收请求
```

这里存在两种不同的“顺序”。启动顺序决定对象如何被装配；请求管道顺序决定一次请求先经过谁。启动时注册 CommandExecutor，不会立刻执行某个预约。请求到达 Controller 后，容器才按作用域提供所需的对象。

[ServiceCollectionExtensions.cs](../backend/ClinicFlow.Api/Hosting/ServiceCollectionExtensions.cs) 注册 ClinicDb、CommandExecutor、EfWorkspaceReader、时钟、Cookie 认证及 MVC 等能力。ClinicDb 与依赖它的服务按请求作用域使用；时钟经 TimeProvider 注入，业务层接收明确的 now，避免把机器当前时间藏在规则内部。

身份部分由 [Identity.cs](../backend/ClinicFlow.Api/Identity.cs)、[AuthController.cs](../backend/ClinicFlow.Api/Controllers/AuthController.cs) 和 [CsrfMiddleware.cs](../backend/ClinicFlow.Api/Middleware/CsrfMiddleware.cs) 配合完成。登录 Cookie 表示当前用户身份，OIS 授权策略限制访问者范围，所属模块再检查具体职责。CSRF 校验保护使用 Cookie 的写请求；这些检查不能相互替代。

这样，后续业务方法拿到的是服务端确定的 actor 和 role，而不是相信请求正文里声称的操作者。

## 4. 定义持久化模型，让业务历史能够保存

数据库入口是 [ClinicDb.cs](../backend/ClinicFlow.Infrastructure/Persistence/ClinicDb.cs)，OIS 实体按所有权放在 ClinicFlow.Domain 中的业务目录，映射集中在 [OisModel.cs](../backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs)。模型不仅要保存页面上的字段，还要表达关联、唯一性、版本和历史。

例如 CareCase 保存当前处方和计划的引用，Prescription 和 TreatmentPlan 则保存各个具体版本。更新“当前指向”不会删除旧记录。TreatmentDelivery 保存执行时的 PlanId，因此后续计划修订不会让既往治疗记录自动指向新计划。

数据库约束与业务规则分别承担不同职责：

| 机制 | 能保证的事情 | 仍需要业务代码处理的事情 |
| --- | --- | --- |
| 外键及 Restrict | 引用对象存在，限制破坏关联的删除 | 当前角色是否允许操作、计划是否仍是当前版本 |
| 计划 `(CaseId, Revision)` 唯一索引 | 同一档案不出现重复计划版本号 | 新版本内容是否完整、是否允许修订 |
| 执行记录 `ReservationId` 唯一索引 | 同一预约不生成多条执行记录 | 开始时间、身份核对及放行条件 |
| Version 并发令牌 | 保存时检查旧版本是否仍匹配 | 请求中的版本是否过期，以及何时递增 |
| 预约查询索引 | 支持资源和时间相关的查询 | 时间区间是否相交、患者与机器是否冲突 |

迁移把模型变化转成数据库升级步骤。`backend/ClinicFlow.Infrastructure/Persistence/Migrations/` 同时保留历史迁移和当前快照；[DatabaseInitialization.cs](../backend/ClinicFlow.Api/Hosting/DatabaseInitialization.cs) 在启动作用域内应用迁移。模型定义、迁移文件和数据库中的实际结构是三个不同对象，只有迁移成功执行后，已有数据库才会得到相应变化。

## 5. 先建立统一写入边界，再接入具体业务

一次业务操作往往改变多个对象。例如签署新处方会增加处方、更新档案当前引用、使旧计划不再作为当前计划、生成待办，并写审计。它们必须一起成功或一起失败。

[CommandExecutor.cs](../backend/ClinicFlow.Application/CommandExecutor.cs) 负责应用编排，通过 ICommandTransaction 交给 [EfCommandTransaction.cs](../backend/ClinicFlow.Infrastructure/Transactions/EfCommandTransaction.cs) 实现持久化边界。实际顺序是：

1. 根据专用请求类型取得历史操作名称，归一化兼容记录，检查请求标识和字符串输入。
2. 计算幂等记录标识与请求指纹，开启 ReadCommitted 事务。
3. 使用 `SELECT ... FOR UPDATE` 锁定 OisGate 的固定行。
4. 查已有幂等记录：同输入返回保存的响应，输入不同则报冲突。
5. 通过 [OisStateStore.cs](../backend/ClinicFlow.Infrastructure/Persistence/OisStateStore.cs) 加载当前状态，取得统一时间，记录相关对象的操作前数据。
6. 用 OisSession 将所属集合装配到三个业务模块，调用 Controller 指定的模块方法，执行规则并改变内存状态。
7. 将新实体纳入跟踪，处理草稿删除，添加审计和幂等响应。
8. 应用层处理领域事件并映射集成事件，`SaveChangesAsync` 将 Outbox 与其他变更一起写入，`CommitAsync` 提交事务，返回 targetId。

OisGate 的作用是让遵守同一入口的写入先后执行，避免两个请求都在旧状态下判断“时段空闲”。它是当前小规模演示的设计选择；如果有写入绕过该入口，这项保证就不完整。

幂等检查位于业务执行之前。因此一次成功请求即使已经改变版本，使用原请求标识和原输入重试，仍可回放原响应，不必重新执行已经完成的规则。相同标识换了输入则不能被当作重试。

[OisState.cs](../backend/ClinicFlow.Application/OisState.cs) 是 Application 内的 internal 工作集，由 Infrastructure 装载。Current 由 DomainRules 提供，EnsureCurrentPlan 和放行条件归 PlanningModule，CheckSlot 委托领域 SchedulePolicy，资源受阻编排归 SchedulingModule。模块只接收所属集合，跨模块使用只读摘要和明确命令；事务由 Infrastructure 的 EfCommandTransaction 提交。

## 6. 从建档开始，逐步增加处方与计划

### 6.1 建档是最小业务链路

[ClinicalModule.CreateCase](../backend/ClinicFlow.Application/UseCases/Clinical/CasesOperations.cs) 检查 Clinician 角色、患者存在和标题内容，再向状态集合加入 CareCase，返回新 ID。此时并没有在方法中直接执行 INSERT；外层执行器会识别新对象并保存。

这条最小链路已经包含后面所有写操作的基本结构：请求输入、可信身份、规则检查、状态变化、审计、持久化和响应。之后增加复杂业务时，复用这个执行框架即可。

### 6.2 处方签署引入版本与下游失效

[ClinicalModule.SignPrescription](../backend/ClinicFlow.Application/UseCases/Clinical/PrescriptionsOperations.cs) 先检查档案版本、是否终止、是否有正在进行的治疗，再检查结构化临床信息。成功后新建 Prescription，通过 SupersedesId 关联旧处方，更新 CurrentPrescriptionId，并清空 CurrentPlanId。

清空当前计划很关键：原计划依据旧处方产生，新处方不能自动继承旧计划的审核和验证。档案 Version 随之递增，物理师收到登记新计划的待办。签署成功只清除当前操作者对应的处方草稿。

草稿与签署也具有不同语义。草稿允许未完成的输入，保存编辑进度；签署会形成正式业务记录并影响后续放行，因此需要更严格的校验。

### 6.3 计划登记建立确定的版本关系

[PlanningModule.RegisterPlan](../backend/ClinicFlow.Application/UseCases/Planning/PlansOperations.cs) 由 Physicist 执行，要求存在当前处方及完整临床信息。新计划绑定具体 PrescriptionId 和 MachineId，并保存验证方法、设备、测量条件、预期值、标准版本、通过标准和允许偏差等结构化信息。

登记新计划会更新档案的 CurrentPlanId 和 Version，产生诊疗人员待办。新计划不会继承旧计划的审核或验证结果。`ReviewPlan` 由 Clinician 对当前计划版本审核，记录审核人、时间并增加计划 Version。

这里的 Revision 是业务上的计划版本序号，Version 是并发控制字段。一次审核增加 Version，不表示创建了新的计划 Revision；登记新计划才会产生新的计划记录和修订序号。

## 7. 接入预约、实验验证与失败处理

### 7.1 排期先解决时间与资源占用

[ReservationsOperations.cs](../backend/ClinicFlow.Application/UseCases/Scheduling/ReservationsOperations.cs) 提供四种明确入口：门诊、治疗由 Scheduler 创建，实验由 Clinician 创建，维护由 Physicist 创建。它们共享内部排期过程，但使用不同请求类型和角色规则。

实验预约关联 PlanId，要求计划已经审核、允许验证、机器与计划一致，且没有重复的待执行实验预约。治疗预约与患者、档案关联；最终可否执行仍由开始治疗时的检查决定。

改期时先构造候选 Reservation，再通过 CheckSlot 检查，最后把候选时间和资源写回原预约。恢复取消预约同样重新检查冲突，因为原时段可能已被其他预约占用。取消并不永久保留资源。

### 7.2 验证结果改变多个对象

[PlanningModule.SubmitExperimentResult](../backend/ClinicFlow.Application/UseCases/Planning/ExperimentsOperations.cs) 由 Physicist 执行。它检查实验预约状态与版本、实验开始时间、当前计划和审核状态、机器可用性，以及测量结果和附件引用等内容。

签署后，Planning 保存独立的 PhysicalValidation，并更新计划的验证结论、签署人和时间；Scheduling 将实验预约变为 Completed。计划和预约分别递增 Version。每次实验的签署结果独立保留，后续重测不会抹掉先前事实；工作台响应仍将结果投影到预约字段中。

### 7.3 失败分类决定后续工作

| 失败类型 | 当前规则的影响 | 为什么不直接重置为“待验证” |
| --- | --- | --- |
| Plan | 通知物理师处理，并让诊疗人员知晓 | 已签署失败记录需要保留，后续修订必须可追溯 |
| Clinical | 要求诊疗人员修订处方或终止流程 | 问题来自临床依据，仅修改物理计划不足以解决 |
| Equipment | 处理失败结果并停用机器，产生相关待办 | 机器风险会影响其他预约，不能只改一条计划状态 |

设备恢复与计划放行是不同操作。机器重新可用只解决设备这一项阻塞，不会把失败计划自动变为通过。对应事实可以在 `Machine_recovery_does_not_release_failed_plan` 等规则测试中看到。

## 8. 最后连接治疗执行，保留实际发生的历史

[SchedulingModule.StartTreatment](../backend/ClinicFlow.Application/UseCases/Scheduling/TreatmentDeliveriesOperations.cs) 是临床主线的汇合点。它要求预约为 Booked 的治疗预约、当前时间处于预约时段、身份已经核对、Blockers 为空，并且患者和机器不存在其他未结束治疗。

请求还必须带操作者已核对的 PlanId。即使预约 Version 没变，只要当前计划变了，服务端仍可通过这个 ID 拒绝沿用旧确认。验证通过后，新建 TreatmentDelivery，固定实际采用的 PlanId，预约改为 InProgress。

`FinishTreatment` 接收 Completed 或 Interrupted 结果，保存说明、结束人和时间。两种结果都会结束预约，预约状态成为 Completed；是否中断保存在执行记录的 Outcome 中，中断还会产生人工跟进待办。因此页面不能仅看预约 Completed 就解释为“治疗正常完成”。

未实际开始的治疗可以登记 NotExecuted；这与已经开始后 Interrupted 的含义不同。已签署执行结果需要通过追加更正保留历史，不能直接覆盖原结果。

## 9. 用资源接口连接工作台

### 9.1 Controller 负责把 HTTP 转为明确业务调用

以治疗预约为例，请求链路为：

```text
OisApp 表单
  → ois-api.sendOisRequest("bookTreatment", request, key)
  → POST /api/ois/reservations/treatment
  → ReservationsController.BookTreatment
  → OisCommandController.Run
  → Request.ToCommand（HTTP DTO → 应用 Command）
  → CommandExecutor.Execute → ICommandTransaction / EfCommandTransaction
  → OisSession 装配事务内模块
  → SchedulingModule.BookTreatment
  → SchedulingModule.CheckSlot
  → 保存与提交 → { targetId }
```

[ReservationsController.cs](../backend/ClinicFlow.Api/Controllers/ReservationsController.cs) 使用 [BookTreatmentRequest.cs](../backend/ClinicFlow.Api/Contracts/Requests/BookTreatmentRequest.cs) 绑定正文。请求里的 Kind 是服务端固定的 Treatment，不需要客户端自由选择。更新操作的目标 ID 则由路由赋给请求对象，正文不能覆盖路由目标。

[OisCommandController.Run](../backend/ClinicFlow.Api/Controllers/OisCommandController.cs) 从 Claims 读取账号和角色，从请求头读取 Idempotency-Key，并将关联标识和取消信号一起传入执行器。Controller 直接选择业务方法；没有通过一个任意字符串选择所有业务的通用入口。

### 9.2 写入响应与页面查询分开

写入返回 targetId，只说明操作针对哪个结果对象。完整工作台由 [EfWorkspaceReader.Snapshot](../backend/ClinicFlow.Infrastructure/Queries/EfWorkspaceReader.cs) 聚合读取：档案、处方、计划、资源、预约、执行记录、待办、患者、审计以及当前用户草稿。

快照在 RepeatableRead 事务内跨表读取，并为 Booked 预约计算 Blockers。这样页面显示的“为何不能开始”来自服务端规则。快照还按操作者过滤草稿及草稿审计，避免把其他人的编辑过程混入当前个人草稿。

### 9.3 前端把业务能力组织成角色工作区

[OisApp.tsx](../frontend/src/OisApp.tsx) 组织工作台与表单，[OisCalendar.tsx](../frontend/src/OisCalendar.tsx) 展示日周资源日历。[ois-api.ts](../frontend/src/ois-api.ts) 明确每个操作的 HTTP 方法、路径和允许发送的字段，[api.ts](../frontend/src/api.ts) 处理公共请求行为。

表单先形成预览和确认，再提交专用请求。成功写入后刷新快照，并通过周期轮询获取其他角色造成的变化。这个流程解释了为什么界面可能因新处方、新计划或机器停用而出现新的阻塞原因。

前端隐藏按钮改善操作体验，真正的角色和状态限制仍在服务端。页面打开时允许某动作，也不保证提交时状态尚未改变；因此请求仍要携带相应 Version，开始治疗还要携带确认过的 PlanId。

## 10. 测试与交付分别证明什么

测试与功能同步演进，这里集中讲它们如何为各层提供证据。具体命令和运行前提见 [运行手册](runbook.md)，不需要另行完成练习。

| 证据层次 | 代码入口 | 证明的内容 |
| --- | --- | --- |
| 规则 | [OisTests.cs](../tests/OisTests.cs) 中 OisRulesTests | 角色、状态、时间、版本、失败分类及放行规则 |
| 模块边界 | [ModuleBoundariesTests.cs](../tests/ModuleBoundariesTests.cs) | 所有权依赖、只读摘要和非公共实体 setter |
| 数据库 | OisDatabaseTests、[ModuleTransactionTests.cs](../tests/ModuleTransactionTests.cs) 与 [DatabaseFixture.cs](../tests/DatabaseFixture.cs) | 预约竞争、处方修订/开始治疗互斥、跨模块原子性、幂等回放 |
| HTTP | [http-ois.mjs](../tests/http-ois.mjs)、[http-controllers.mjs](../tests/http-controllers.mjs) | Cookie、CSRF、请求绑定、路径、序列化及 OpenAPI 契约 |
| 浏览器 | `frontend/e2e/` | 跨角色操作、表单确认和日历交互 |
| 升级 | [UpgradeCompatibilityTests.cs](../tests/UpgradeCompatibilityTests.cs)、[PhysicalValidationMigrationTests.cs](../tests/PhysicalValidationMigrationTests.cs)、[upgrade-drill.sh](../scripts/upgrade-drill.sh) | 历史数据升级、幂等兼容及备份恢复 |
| 打包 | [Dockerfile](../Dockerfile)、[http-ui.mjs](../tests/http-ui.mjs) | 前端资源随服务交付、根页面与静态资源可访问 |

规则测试使用可控时间，能表达实验开始之后、治疗窗口之内等条件。数据库测试需要真实 MySQL，因为内存集合无法证明锁与事务行为。HTTP 测试访问运行中的 API，会写入目标应用数据库；它与脚本指定的隔离数据库集成测试不是同一运行方式。

交付时先构建 React 静态资源，再发布后端并把前端产物放入 wwwroot，最终由 ASP.NET 托管页面和 API。[CI](../.github/workflows/ci.yml) 将构建、测试、升级及容器验证串起来。本机后端编译成功只证明源码可编译，不能单独证明完整页面和数据库升级可用。

## 11. 为什么重构后仍保留兼容模块

资源 Controller 与专用请求是当前对外契约；[Compatibility/CommandHistory.cs](../backend/ClinicFlow.Application/Compatibility/CommandHistory.cs) 和 [LegacyCommandRecord.cs](../backend/ClinicFlow.Application/Compatibility/LegacyCommandRecord.cs) 维护历史存储格式。

已有幂等记录的指纹依赖历史操作名称、字段顺序和默认值。仅仅把 HTTP 请求类型拆分，就直接改变指纹算法，会让相同业务输入在升级后无法回放。因此新请求会映射到冻结的兼容格式；业务执行仍调用所属模块的具名方法，不重新引入旧的字符串分发结构。

审计中的历史 action 和 detail.command.text 也需要保持可读。兼容层的存在说明一次接口重构除了改 Controller 和前端路径，还要考虑已落库的审计、草稿和幂等结果。当前结构的详细边界见 [架构说明](architecture.md)。


## 12. 模块化演进：从全局状态到三个领域模块

先按业务能力确定数据所有权，再改调用方式：诊疗模块拥有档案和处方，计划模块拥有计划与签署验证记录，排程模块拥有机器、预约和执行。角色权限仍在每个操作入口检查，三个角色不等于三个模块。

原 Operations 文件现在是各模块 partial class 的一部分，Controller 调用 `session.Clinical`、`session.Planning` 或 `session.Scheduling`。OisSession 只负责装配；业务模块不引用它，也不读取全局 OisState。跨模块查询返回 CaseSummary、PlanSummary、ExperimentSlot，更新则交给数据所有者。

例如提交验证时，Planning 取得 Scheduling 检查后的实验摘要，在自己的 PhysicalValidation 中签署事实，再要求 Scheduling 完成预约。二者以及计划结论、停机、待办、审计和幂等响应一起提交。这一步建立了模块边界，但仍保留本地强一致性；以后是否拆微服务，需要另行解决跨进程的一致性协议。

数据库新增验证表，并通过迁移搬移旧列；工作台仍投影出原 JSON。阅读顺序建议为 [上下文地图](../CONTEXT-MAP.md) → [OisSession](../backend/ClinicFlow.Application/OisSession.cs) → 任一模块入口 → 领域对象 → [映射](../backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs) → [架构及事务取舍](architecture.md)。

## 当前分层与事件的源码复盘

历史开发顺序说明功能如何演进；当前代码已落实四层程序集。阅读一个“提交验证结果”用例时，沿 API 的 SubmitExperimentResultRequest.ToCommand → Application 的 PlanningModule.SubmitExperimentResult → Domain 的 PhysicalValidation.Sign / TreatmentPlan.RecordValidation → Application 的 PlanValidated 处理器 → Infrastructure 的 EfCommandTransaction / OutboxMessage 跟读。

验证失败时，应用事件处理器完成实验占用、生成待办并按失败分类停机，与验证结果一起提交；任何处理失败整体回滚。独立 V1 集成事件此时只写入 Outbox，另一次投递事务才能通知外部。默认未配置真实 publisher。重放请求不会再创建事件；投递却可能重复，两者的幂等边界不同。参见[事件文档](events.md)和[分层决策](adr/0002-layered-events.md)。

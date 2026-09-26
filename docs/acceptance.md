# 功能状态与验收记录

<a id="current-status"></a>

## 当前结论（2026-09-26）

作为本地学习、演示和面试项目，约定的预约主线与账号、Agent、影像扩展已实现，并有本地验证记录。尚不能称为生产医疗系统，也不能把早期远程 CI 的成功套用到最新影像提交。

| 能力 | 状态 | 实现与验证入口 |
|---|---|---|
| 注册、四角色、本人归属 | 已实现 | Identity / AppointmentAccess / AccountLifecycleTests；A24–A25 |
| 创建、任务、确认、改期、取消、完成 | 已实现 | SchedulingService / SchedulingTests / AccountLifecycleTests；A02–A12、A26 |
| 锁、Version、幂等、审计 | 已实现 | Execute / Mutate、真实 MySQL 回滚与并发测试 |
| Outbox、租约、去重、防乱序、人工重试 | 已实现 | Dispatcher / receiver；A13–A17 |
| FHIR R4 Patient / Appointment 只读 | 限定范围已实现 | FhirAdapter / FhirTests；A20 |
| Pi + Kimi、SSE、候选确认与冲突恢复 | 已实现；真实模型需要本地密钥 | AgentTests / KimiModelTests / runtime.test.mjs；A27–A29 |
| DICOMweb 查询、关联、下载和 Stone 查看器 | 限定合成对象已验证 | ImagingTests、真实 Orthanc HTTP 与浏览器；I01–I08 |
| 打包、故障实验、升级恢复、CI 配置 | 本地交付能力已具备 | scripts、labs、工作流及历史证据 |

### 本次文档整合时重新执行

- `./scripts/test.sh --no-restore`：80 通过，0 失败，0 跳过（真实隔离 MySQL，约 19 秒）。
- `npm --prefix frontend run build`：TypeScript / Vite 构建通过。
- `npm --prefix agent-runtime test`：3 项通过。
- `node --test mock-external/receiver.test.mjs`：1 项通过。
- 容器状态：app / db / mock / orthanc 正在运行，db 与 orthanc 健康。
- 文档检查：整合后公开 Markdown 由40份收敛为34份，约减少三分之一篇幅；本地文件链接及章节锚点检查无错误，`git diff --check` 通过。

本次没有重跑浏览器、HTTP 集成、停机实验、真实 Kimi、空库启动或升级恢复；下文的相关结果来自之前已记录的执行。没有运行远程 CI，也没有生产环境验证。

### 后续完善的优先级

1. **面试使用**：亲手复现事务、同步和影像链路，留下自己的修改与解释；不用继续堆功能来替代理解。
2. **远程交付**：在授权推送后核验对应提交的全量 CI；当前最新影像提交只有本地证据。
3. **生产化另立范围**：HTTPS/生产凭据、多实例迁移与会话、密钥轮换、备份调度及影像恢复、保留清理、可观测性和负载测试均需补设计与验收。
4. **医疗接入另立范围**：真实医院/设备对接、厂商对象矩阵、完整标准符合性及适用合规要求尚未验证。

<a id="acceptance-matrix"></a>

## 历史需求与验收证据

执行环境：macOS 26.6.2 / Apple Silicon / MySQL 8.4.8；2026-09-23至24。GitHub Actions在Linux执行相同自动化核心。测试方法名是可检索的入口；完整测试命令在README。此表不等于医疗产品认证。

| ID | 设计/代码 | 实际证据与状态 |
|---|---|---|
| A01 | Program、Identity、迁移、Compose | 初始空库迁移/种子/健康检查；HTTP三角色登录；Playwright A01通过。独立干净克隆/新卷启动与全部HTTP/6项浏览器流程通过，见evidence/clean-start.md |
| A02 | SchedulingService.Create、SlotClaim | A02_AtomicMultiSlot通过；HTTP/浏览器创建详情通过 |
| A03 | 资源父锁+占用主键 | A03_CompetingIndependentConnections通过；验证一预约/4占用/1幂等记录 |
| A04 | Execute事务 | A04_PartialConflictRollsBackEverything通过；无部分预约/占用/Outbox/幂等残留 |
| A05 | Mutate改期事务 | A05_ConflictAndInjectedFailureRestoreOldClaims通过；after-release注入故障后恢复旧状态/版本/占用/记录 |
| A06 | 删除flush仍未提交→新占用 | A06_OverlapWithOwnSlots通过；浏览器跨资源改期通过 |
| A07 | 资源ID全序锁 | A07_OppositeResourceMovesHaveConsistentOrder通过；两个独立连接、预读屏障、15秒边界 |
| A08 | 预约锁+Version/resource重验 | A08_ConcurrentOldVersionAndChangedPreread、A08_TaskAndCancelUseSameVersionBoundary通过 |
| A09 | 当前任务完成、状态约束 | A09_TasksConfirmationAndReset及HTTP前置失败/角色测试通过 |
| A10 | IdempotencyRecord唯一锁+规范化指纹 | A10_A11_ConcurrentAndLostResponseReplay通过；顺序/并发/异内容 |
| A11 | 成功响应先于版本检查重放 | A11_ReplayPrecedesCurrentVersionAfterLaterChanges通过；HTTP代理在API成功后实际断开连接，再用同Key取得原结果且只有一次审计/Outbox效果 |
| A12 | 取消释放与终态 | A12_CancelReplayReleasesOnceAndRebookingWorks通过；浏览器取消通过 |
| A13 | Outbox与本地事务独立 | A13_OutageDoesNotRollbackLocalBookingAndRecovers；http-integration实际Docker停机/恢复通过 |
| A14 | SQLite持久去重事务 | receiver.test.mjs实际保存后断响应、进程重启、同MessageId重放通过 |
| A15 | 接收端条件版本更新 | receiver.test.mjs取消v3后收到v2仍保持v3通过 |
| A16 | SKIP LOCKED与有期限token租约 | A16_ParallelClaimsAndLeaseRecoveryRejectOldOwner通过；多连接/注入时钟 |
| A17 | Dispatcher及RetrySync | A17_RetryLimitAndIdempotentManualRetryPreserveIdentityHistory、PermanentFailureDoesNotRetryAutomatically通过 |
| A18 | 认证/CSRF/角色策略 | auth/http-scheduling/http-tasks/http-integration覆盖401、各类业务写403、角色分工及CSRF；管理员浏览器只显示同步权限 |
| A19 | UTC、分页、稳定排序、索引 | SlotsValidateBoundaries、A19_AdjacentSlotsDoNotOverlap；HTTP验证UTC Z、分页/筛选/上限 |
| A20 | FhirAdapter | FhirTests三种状态+Patient映射4项通过；HTTP metadata/读取/无效ID/查询/写入边界通过 |
| A21 | Scheduling页面/角色协作 | Playwright完整创建→任务→确认→改期→取消通过；占用冲突和同步队列/attempt页面通过；受控UI fixture验证失败提示、网络错误后的同Key重试 |
| A22 | ResourceScheduleIndex及UpgradeDrill | 实际旧schema样例→备份→升级保留数据→导入备份恢复旧schema，通过；evidence/upgrade-drill.txt |
| A23 | 锁文件、CI、Dockerfile、提交历史 | M0–M8a独立commit/push，扩展远程CI已通过并生成制品；Docker与新卷部署通过；最终修复提交由README实时CI链接核验 |

额外证据：L1前后并发覆盖、L2前后副作用计数、L3固定10万数据集计划/计时/同序结果均在evidence目录。`tests/IntegrationTests.cs`的HTTP Handler用于可控错误分类；真实容器停机另由http-integration证明，不能混为同一测试。

未覆盖的生产能力：大规模压测、多节点会话/迁移协调、密钥轮换、备份调度、数据保留、真实医疗协议profile验证与合规认证。它们不在本次spec验收范围。

## 2026-09-26：账号注册与Completed扩展

本地后端59项通过（原有46项加注册、归属、完成边界及第四种FHIR状态）；完整HTTP回归通过，包括注册CSRF、客户端伪造角色/档案、跨账号读写及FHIR隔离、本人改期取消、工作人员登记完成与重放；Chrome浏览器11项全量通过。接收端Completed持久化/重放/乱序验证通过。前端构建、Docker镜像构建、EF模型与迁移一致性检查通过。

升级实验从旧schema迁移至RegisteredAccounts（6个迁移），保留预约ID及六类记录数，再实际导入备份恢复旧schema通过。原有本地数据库升级前已备份到忽略目录artifacts/before-account-upgrade.sql。HTTP/浏览器回归在clinicflow_accounts_tests隔离数据库执行，未向主数据库添加回归账号。测试中首次默认Chromium未安装，改用已安装Chrome；连续测试曾触发真实登录限流，已补429明确提示和遵循Retry-After的浏览器测试辅助逻辑，随后11项全量通过。本次未执行远程CI。

| ID | 实现 | 新验证入口 |
|---|---|---|
| A24 | 注册账号、密码散列、唯一档案绑定 | AccountLifecycleTests注册/校验/并发测试；http-accounts.mjs；accounts.spec.ts |
| A25 | Booker本人范围、资源占用信息最小化、FHIR归属 | AccountScopeHidesOtherPatientsAndRejectsCrossAccountMutation；http-accounts.mjs |
| A26 | Confirmed→Completed、结束时间、终态、原子释放 | Completion*测试；http-accounts.mjs；accounts.spec.ts；FhirTests；receiver.test.mjs |

部署后冒烟：原localhost:5080容器已升级，数据库健康、既有工作人员登录、注册/完成OpenAPI端点、新前端资源均通过检查。独立验证了注册/登录限流返回429、Retry-After和明确提示，未创建额外账号。验证用5088进程已关闭。

## 2026-09-26：个人预约Agent与流式输出

| ID | 实现 | 验证入口 |
|---|---|---|
| A27 | Booker助手、会话和工具的本人归属 | G15/G16、http-accounts.mjs、streaming.spec.ts |
| A28 | Pi Agent Core实际调度模型和工具 | agent-runtime/runtime.test.mjs（3项）；AgentTests；live-agent-stream.mjs |
| A29 | SSE增量输出、中断、确认重放 | KimiModelTests、G17、http-agent.mjs、streaming.spec.ts |

本地后端66项、Pi运行时3项、完整HTTP回归、Chrome浏览器13项通过。浏览器使用真实分块HTTP响应验证完整结果之前已显示文字；模型解析测试覆盖分块UTF-8、工具参数拼接和不完整流拒绝。真实Kimi联调在隔离数据库收到61个文字增量，确认前无预约、确认后仅本人预约、重复确认仅一条记录，测试预约已取消。构建和localhost:5080部署冒烟通过；本次没有远程CI结果。


## 影像扩展验收

| 条目 | 实现与证据 |
|---|---|
| I01 可重复合成数据与标准导入 | imaging/seed.py；18实例、6序列、3检查；STOW-RS |
| I02 查询/多检查关联/去重 | ImagingService、MySQL复合键；HTTP与并发测试 |
| I03 身份缺失、错配、混合实例拒绝 | ImagingTests/DicomWebTests；真实跨患者UID拒绝 |
| I04 权限和CSRF | HTTP测试覆盖匿名、Booker、Admin、TaskOperator及缺失CSRF |
| I05 序列/实例、原文件和实际显示 | HTTP DICM封装检查；Playwright像素请求、画布与截图 |
| I06 解除与审计 | 解除不删除归档，成功审计原子写入；撤销后的读取拒绝 |
| I07 外部故障与恢复 | http-imaging-outage.mjs；503请求标识、本地解除、恢复查询 |
| I08 文档、范围与CI | docs/imaging；CI含真实影像服务与故障实验；实际结果见验证记录 |

[影像实际验证记录](imaging/verification.md)区分已运行结果、环境限制和未执行的远端CI。
<a id="upgrade"></a>

## 数据库升级与恢复检查记录

执行日期：2026-09-24（上海）；精确UTC时间与结果见[evidence/upgrade-drill.txt](evidence/upgrade-drill.txt)。SDK 10.0.401 / runtime 10.0.12 / EF 9.0.20 / MySQL 8.4.8。

已实际执行 `./scripts/upgrade-drill.sh`：

1. 只在固定隔离库`clinicflow_upgrade_tests`重建旧schema，迁移至SyncAttempts（4次迁移）。使用真实SchedulingService创建1预约、4占用、2任务、1审计、1Outbox及1成功幂等记录。
2. 用`mysqldump --single-transaction --no-tablespaces --set-gtid-purged=OFF`生成忽略入库的`artifacts/upgrade-before.sql`。所有业务表使用InnoDB；演练期间无并发DDL。这不是覆盖任意数据库引擎与在线DDL情况的备份承诺。
3. 升级至ResourceScheduleIndex（5次迁移）。新增`(ResourceId,StartUtc,Id)`索引；断言原预约ID与全部六类记录数量不变。
4. 删除的仅是隔离演练库，重新创建并导入备份；核验恢复到旧迁移记录且六类数据保持。备份恢复是实际执行，非空模板。

首次演练失败：生成器将ResourceId单列索引视为可被组合索引替代，先生成DROP INDEX。MySQL拒绝删除外键依赖索引。修复是在模型中显式保留旧索引，使升级只新增索引。重跑完整流程及`dotnet ef migrations has-pending-model-changes`通过。此失败说明生成迁移必须在真实目标数据库审查验证。

应用回退：本次新增索引与旧应用兼容，可回退应用镜像而保留索引。数据库恢复：重新导入备份，必须协调停写、恢复点和外部系统状态；不能把切回旧程序称为数据库恢复。MySQL DDL通常隐式提交，不应假定整个迁移可事务回滚。未来破坏性迁移需单独设计双写/数据回填/兼容窗口和恢复方案。

交付前检查：核验目标库、备份可读性与空间、迁移SQL、应用兼容性、演练结果、凭据不入库；变更后检查迁移记录、预约/占用不变量、审计/Outbox和接口冒烟。本项目startup migration用于单实例演示；生产应使用独立受控迁移作业。

<a id="history"></a>

## 开发里程碑摘要

历史阶段测试数量是对应时点的结果，不是当前总数。更详细的逐阶段叙述保留于 Git 历史中的原 development-progress 文档。

| 阶段 | 交付与证据 |
|---|---|
| M0–M4 | 平台、身份、事务预约、改期/取消、前置任务；见 A01–A12 |
| M5 | Outbox、持久接收、租约及真实停机；见 A13–A17 |
| M6 | FHIR R4 限定只读接口；见 A20 |
| M7 | L1/L2/L3 隔离实验；原始执行计划与计时保留于 evidence |
| M8a / M8b | 容器、升级恢复、全链路与干净启动；早期 e80c473 CI run 35961883622 成功，后续版本不能沿用此结论 |
| 2026-09-26 账号闭环 | 注册与 Completed，59 项后端/11 项浏览器历史通过 |
| 2026-09-26 个人 Agent | Pi 与 SSE，66 项后端/3 项运行时/13 项浏览器历史通过 |
| 2026-09-26 影像 | 80 项后端/14 项浏览器历史通过；详细证据见影像验证记录 |
| 2026-09-26 文档整合 | 当前状态、常见任务导读、学习/运行/面试资料归并；本次复核见本文顶部 |

原先“Completed 留作独立练习”的里程碑描述已失效。阶段提交事实以 `git log` 为准；历史资料不是当前待办列表。

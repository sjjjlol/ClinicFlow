# 需求 → 设计 → 代码 → 验证

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

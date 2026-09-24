# 需求 → 设计 → 代码 → 验证

执行环境：macOS 26.6.2 / Apple Silicon / MySQL 8.4.8；2026-09-23至24。GitHub Actions在Linux执行相同自动化核心。测试方法名是可检索的入口；完整测试命令在README。此表不等于医疗产品认证。

| ID | 设计/代码 | 实际证据与状态 |
|---|---|---|
| A01 | Program、Identity、迁移、Compose | 初始空库迁移/种子/健康检查；HTTP三角色登录；Playwright A01通过。最终干净检出容器路径待M8打包复验 |
| A02 | SchedulingService.Create、SlotClaim | A02_AtomicMultiSlot通过；HTTP/浏览器创建详情通过 |
| A03 | 资源父锁+占用主键 | A03_CompetingIndependentConnections通过；验证一预约/4占用/1幂等记录 |
| A04 | Execute事务 | A04_PartialConflictRollsBackEverything通过；无部分预约/占用/Outbox/幂等残留 |
| A05 | Mutate改期事务 | A05_ConflictAndInjectedFailureRestoreOldClaims通过；after-release注入故障后恢复旧状态/版本/占用/记录 |
| A06 | 删除flush仍未提交→新占用 | A06_OverlapWithOwnSlots通过；浏览器跨资源改期通过 |
| A07 | 资源ID全序锁 | A07_OppositeResourceMovesHaveConsistentOrder通过；两个独立连接、预读屏障、15秒边界 |
| A08 | 预约锁+Version/resource重验 | A08_ConcurrentOldVersionAndChangedPreread、A08_TaskAndCancelUseSameVersionBoundary通过 |
| A09 | 当前任务完成、状态约束 | A09_TasksConfirmationAndReset及HTTP前置失败/角色测试通过 |
| A10 | IdempotencyRecord唯一锁+规范化指纹 | A10_A11_ConcurrentAndLostResponseReplay通过；顺序/并发/异内容 |
| A11 | 成功响应先于版本检查重放 | A11_ReplayPrecedesCurrentVersionAfterLaterChanges通过；HTTP使用同Key重新取原结果 |
| A12 | 取消释放与终态 | A12_CancelReplayReleasesOnceAndRebookingWorks通过；浏览器取消通过 |
| A13 | Outbox与本地事务独立 | A13_OutageDoesNotRollbackLocalBookingAndRecovers；http-integration实际Docker停机/恢复通过 |
| A14 | SQLite持久去重事务 | receiver.test.mjs实际保存后断响应、进程重启、同MessageId重放通过 |
| A15 | 接收端条件版本更新 | receiver.test.mjs取消v3后收到v2仍保持v3通过 |
| A16 | SKIP LOCKED与有期限token租约 | A16_ParallelClaimsAndLeaseRecoveryRejectOldOwner通过；多连接/注入时钟 |
| A17 | Dispatcher及RetrySync | A17_RetryLimitAndIdempotentManualRetryPreserveIdentityHistory、PermanentFailureDoesNotRetryAutomatically通过 |
| A18 | 认证/CSRF/角色策略 | auth/http-scheduling/http-tasks/http-integration覆盖401、各类业务写403、角色分工及CSRF；管理员浏览器只显示同步权限 |
| A19 | UTC、分页、稳定排序、索引 | SlotsValidateBoundaries、A19_AdjacentSlotsDoNotOverlap；HTTP验证UTC Z、分页/筛选/上限 |
| A20 | FhirAdapter | FhirTests三种状态+Patient映射4项通过；HTTP metadata/读取/无效ID/查询/写入边界通过 |
| A21 | Scheduling页面/角色协作 | Playwright完整创建→任务→确认→改期→取消通过；占用冲突和同步队列/attempt页面通过 |
| A22 | ResourceScheduleIndex及UpgradeDrill | 实际旧schema样例→备份→升级保留数据→导入备份恢复旧schema，通过；evidence/upgrade-drill.txt |
| A23 | 锁文件、CI、Dockerfile、提交历史 | M0–M7独立commit/push，远程CI已核验；最终M8与打包/完整CI结果待完成 |

额外证据：L1前后并发覆盖、L2前后副作用计数、L3固定10万数据集计划/计时/同序结果均在evidence目录。`tests/IntegrationTests.cs`的HTTP Handler用于可控错误分类；真实容器停机另由http-integration证明，不能混为同一测试。

未覆盖的生产能力：大规模压测、多节点会话/迁移协调、密钥轮换、备份调度、数据保留、真实医疗协议profile验证与合规认证。它们不在本次spec验收范围。

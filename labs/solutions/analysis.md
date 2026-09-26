# 故障实验解析（完成练习后阅读）

L1：`./scripts/labs.sh L1 --fixed`。UPDATE加上`WHERE version=@readVersion`，更新时递增Version；affected=0表示冲突，需要调用方重新读取并决定。只有WHERE条件而没有递增，后续旧写入仍可能成功。正常预约还需要资源占用事务及锁协议，不能只加单行Version。

L2：`./scripts/labs.sh L2 --fixed`。唯一MessageId记录与副作用同事务；重复insert没有新效果。正常接收端还会比较单调预约版本，防止旧状态覆盖新状态。去重记录保留策略必须与最长重放周期一致；本练习不自动清理。重复HTTP本身无法完全消除，网络超时表示结果未知。

L3：组合索引`(resource_id,start_utc,id)`与等值筛选、时间范围和稳定排序一致，减少扫描和排序。COUNT、不同筛选组合和深offset分页可能需要不同方案；索引也增加写入与空间成本。实验结果见实际证据，计时受缓存和本机负载影响，不包装为生产指标。

<a id="rca"></a>

## RCA：预约旧快照覆盖

日期：2026-09-24；性质：L1隔离实验中的刻意缺陷，不是生产事故。

现象：两个客户端都读取v1，A提交成功后B仍提交成功；最终只看到B的数据，调用方没有任何冲突提示。影响边界是同一对象的并发编辑。实验没有真实患者数据。

复现：`./scripts/labs.sh L1`。证据见`docs/evidence/L1-before.txt`。同步屏障保证两个读都发生在第一次写之前；第二写等待第一写结束，复现不依赖随机线程调度。

根因：UPDATE只检查主键，读取快照与写入之间缺少Version条件。数据库正确执行两次合法UPDATE，问题是应用允许旧业务决策覆盖新状态。

修复：写入附加读取时的Version并原子递增；affected=0映射409 version_conflict。验证见`docs/evidence/L1-after.txt`及正常SchedulingTests.A08。影响分析：新冲突需要UI刷新提示；同Key已成功请求必须先返回幂等结果，否则旧版本重放会误报冲突；改期的多行占用仍需原子事务和锁，Version不能独自代替它们。

预防：跨写路径统一版本边界；受控并发集成测试；审计跟踪对象版本与关联ID；审查所有新增状态变更是否绕过应用服务。正常应用测试与故障实验分别运行。

### English practice defect update

**Title:** Stale appointment edits silently overwrite a committed update.

**Reproduction:** Two isolated clients read version 1. Client A commits first. Client B submits its old snapshot and also receives success.

**Root cause:** The update predicate only checks the appointment identifier.

**Resolution:** Compare the expected version inside the transaction and return an explicit conflict for stale commands. Preserve successful idempotent replay before evaluating the current version.

**Validation:** The deterministic lab rejects the second write after the fix. MySQL integration tests also cover cross-resource rescheduling, rollback, and task/cancellation races. No production performance or clinical safety claim is made.

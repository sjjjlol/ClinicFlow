# 数据库升级与恢复检查记录

执行日期：2026-09-24（上海）；精确UTC时间与结果见[evidence/upgrade-drill.txt](evidence/upgrade-drill.txt)。SDK 10.0.401 / runtime 10.0.12 / EF 9.0.20 / MySQL 8.4.8。

已实际执行 `./scripts/upgrade-drill.sh`：

1. 只在固定隔离库`clinicflow_upgrade_tests`重建旧schema，迁移至SyncAttempts（4次迁移）。使用真实SchedulingService创建1预约、4占用、2任务、1审计、1Outbox及1成功幂等记录。
2. 用`mysqldump --single-transaction --no-tablespaces --set-gtid-purged=OFF`生成忽略入库的`artifacts/upgrade-before.sql`。所有业务表使用InnoDB；演练期间无并发DDL。这不是覆盖任意数据库引擎与在线DDL情况的备份承诺。
3. 升级至ResourceScheduleIndex（5次迁移）。新增`(ResourceId,StartUtc,Id)`索引；断言原预约ID与全部六类记录数量不变。
4. 删除的仅是隔离演练库，重新创建并导入备份；核验恢复到旧迁移记录且六类数据保持。备份恢复是实际执行，非空模板。

首次演练失败：生成器将ResourceId单列索引视为可被组合索引替代，先生成DROP INDEX。MySQL拒绝删除外键依赖索引。修复是在模型中显式保留旧索引，使升级只新增索引。重跑完整流程及`dotnet ef migrations has-pending-model-changes`通过。此失败说明生成迁移必须在真实目标数据库审查验证。

应用回退：本次新增索引与旧应用兼容，可回退应用镜像而保留索引。数据库恢复：重新导入备份，必须协调停写、恢复点和外部系统状态；不能把切回旧程序称为数据库恢复。MySQL DDL通常隐式提交，不应假定整个迁移可事务回滚。未来破坏性迁移需单独设计双写/数据回填/兼容窗口和恢复方案。

交付前检查：核验目标库、备份可读性与空间、迁移SQL、应用兼容性、演练结果、凭据不入库；变更后检查迁移记录、预约/占用不变量、审计/Outbox和接口冒烟。本项目startup migration用于单实例演示；生产应使用独立受控迁移作业。

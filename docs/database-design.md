# OIS 数据库

<a id="ois-data"></a>

|表|用途|
|---|---|
|Patients|共享患者资料，保留虚构种子患者|
|Users|三个工作人员角色及密码散列，不再绑定个人患者账号|
|Idempotency|命令指纹和原始成功响应，保留已有请求重放能力|
|CareCase|患者诊疗档案及当前处方/计划引用|
|Prescription|签署处方和修订链|
|TreatmentPlan|外部计划版本、处方版本、机器、审核和验证结论|
|OisResource|诊疗人员和机器资源、可用性|
|Reservation|单次门诊、治疗、实验和维护占用，由 Scheduling 拥有|
|PhysicalValidation|每个实验预约的签署结果、测量值、附件引用，由 Planning 拥有；ReservationId 为主键及外键|
|TreatmentDelivery|实际使用的计划版本、执行结果|
|OisDraft|个人处方/计划草稿，由 Support 管理|
|OisTask|责任角色待办、已读和处理结果|
|OisAudit|操作者、变更内容、请求关联号及时间|
|OisGate|所有 OIS 写事务先获取的排他行锁|

具体字段和索引以 `backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs` 及模型快照为准。患者及计划关联受约束，签署历史不覆盖；版本校验、半开区间冲突和角色规则由服务端统一执行。

## 历史清理迁移

历史 EF 迁移保留，以兼容已经应用迁移的数据库；它们不是旧业务模块。历史 `RemoveLegacyModules` 迁移删除旧预约、槽占用、前置任务、审计、资源、Outbox、同步尝试及影像关联表，删除非 Scheduler/Clinician/Physicist 账号和 Users.PatientId。

该历史迁移保留全部患者资料、原 OIS 表、三个工作人员角色账号/密码及幂等表。幂等表不按不透明哈希猜测来源删除记录。清理迁移不会自动把旧预约转换成临床数据。

**启动新版前先备份数据库并停止旧版写入。** 应用启动自动执行迁移；该清理会删除旧模块数据。Down 只能重建空表结构，不能恢复删除的数据；需要完整恢复时使用升级前备份。

`./scripts/upgrade-drill.sh` 在隔离库创建上版 OIS 数据 → SQL 备份 → 应用后续迁移（含清理与验证记录分离）→ 验证 OIS 数据、账号、审计、幂等重放及旧表删除 → 恢复备份并验证。不会操作应用数据库。

## 模块化重构：验证记录分离

`SeparatePhysicalValidation` 将 Reservation 的 MeasuredValues、ResultAttachment、ValidationOutcome、ValidationNote、FailureType、SignedBy、SignedUtc 七列搬到 PhysicalValidation。ReservationId 唯一关联原预约，预约上的 PlanId 仍指向该次实验验证的具体计划版本；计划表的验证结论保留为当前状态，历史实验记录分别保留，失败后重测不会覆盖前次结果。

Up 先创建新表，再复制任意验证字段非空或签署时间非空的旧记录，最后移除旧列；未签署且字段全空的预约不会生成空验证记录。Down 先恢复旧列并为无验证记录的预约填空值，再按 ReservationId 写回所有字段，最后删除新表。迁移不改变预约 ID、版本、状态、计划、审计、账号、草稿或幂等记录。

已有实例升级前备份并停止旧版应用写入。MySQL DDL 不能视为整批原子事务，迁移中断时应检查实际表结构和迁移历史，按备份恢复，不要盲目重跑或让旧新版同时写入。新版启动会自动迁移；回退应用代码前必须执行 Down 或恢复备份。这里只说明本次分离迁移的可逆性，历史 RemoveLegacyModules 的删除仍需要备份才能恢复。

EF 映射集中于 `backend/ClinicFlow.Infrastructure/Persistence/OisModel.cs`，实体定义在 `backend/ClinicFlow.Domain/`。同库外键和事务有意保留；三个模块并非三个独立数据库。EfWorkspaceReader 用 ReservationResponse 重组旧 JSON 字段，客户端无需同步迁移数据模型。

验证：`./scripts/test.sh --filter FullyQualifiedName~PhysicalValidationMigrationTests` 使用隔离测试库验证复制、读取和回退再升级；完整升级及备份恢复仍运行 `./scripts/upgrade-drill.sh`。

## 四层重构与集成消息表

EF 模型、设计时工厂和所有迁移位于 ClinicFlow.Infrastructure；运行宿主为 ClinicFlow.Api。历史实体 CLR 名称与迁移 ID 保留；新迁移 AddIntegrationOutbox 仅创建 OutboxMessage，不清空或重建业务表。历史业务记录不生成补发通知。

OutboxMessage 保存 Id（稳定事件 ID）、Type、Version、Payload、OccurredUtc、PublishedUtc、Attempts、NextAttemptUtc、LastError；索引覆盖 PublishedUtc 与 NextAttemptUtc。它与业务、审计及幂等结果一起提交，发送在单独事务内完成。事件生命周期见[事件文档](events.md)。

迁移命令在仓库根执行：

```sh
./scripts/dotnet.sh ef migrations list --project backend/ClinicFlow.Infrastructure
./scripts/dotnet.sh ef migrations script --idempotent --project backend/ClinicFlow.Infrastructure --output migration.sql
```

设计时工厂不是生产连接配置；实际升级使用已配置连接的 API 启动迁移，或经过审核的 SQL。回退 AddIntegrationOutbox 会删除消息表，必须先保全待发与已发记录，并协调外部消费者；不能把普通表回退当成撤回已投递事件。四层迁移本身不改变现有业务表。

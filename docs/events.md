# 领域事件与集成事件

领域事件表达模型内已经发生的业务事实；集成事件是对外的版本化通知。本项目将两者分开定义，不直接把领域对象序列化到消息系统。

| 维度 | 领域事件 | 集成事件 |
| --- | --- | --- |
| 定义 | Domain/Events | Application/Integration |
| 产生与处理 | 聚合行为记录，Application 同步处理 | 领域事件显式映射成独立 V1 契约 |
| 事务 | 当前业务事务内；处理失败回滚 | 与业务数据同时写 Outbox，提交后另行投递 |
| 变更范围 | 项目内部模型 | 对外类型名称、版本、字段需要兼容 |
| 交付语义 | 本次命令内调用 | 至少一次；可能重复、可能乱序 |

## 当前事实及联动

| 领域事件 | 事务内处理 | 集成事件 Type（Version = 1） |
| --- | --- | --- |
| PrescriptionSigned | 物理师待办、受阻预约待办 | clinicflow.prescription-signed |
| CaseTerminated | 终止关联待办 | clinicflow.case-terminated |
| PlanRegistered | 更新档案当前计划引用、审核和受阻待办 | clinicflow.plan-registered |
| PlanValidated | 完成实验占用；失败待办；设备失败时停机 | clinicflow.plan-validated |
| TreatmentFinished | 中断时创建临床待办 | clinicflow.treatment-finished |

只有这些业务事实目前产生事件，不是每个 CRUD 动作都自动发布。审核、开始治疗等仍走显式用例；治疗放行、患者与机器冲突检查仍同步进行，不依赖外部订阅方是否已收到通知。

[DomainEvents](../backend/ClinicFlow.Domain/Events/DomainEvents.cs) 包含事件 ID、发生时间和内部事实数据。[DomainEventHandlers](../backend/ClinicFlow.Application/Events/DomainEventHandlers.cs) 在保存前处理；当前处理器不会再次生成领域事件。以后新增级联事件时，应同步扩展分发循环与防循环测试，不能假设现有单轮遍历覆盖任意链路。

[IntegrationEventMapper](../backend/ClinicFlow.Application/Integration/IntegrationEventMapper.cs) 生成包含 EventId、Type、Version、OccurredUtc、Payload 的信封。Payload 为 camelCase JSON 字符串，传输适配器应明确字符串或对象的线协议，不能依靠领域类型名称自动推导。ContractsV1 只传业务引用及必要状态，不传诊断、备注、操作者等内部字段；引用 ID 仍应按业务数据控制访问。

修改领域事件无需强迫外部消费者升级；新增兼容字段须检查消费者容忍度，删除字段或改变含义应引入新版本并安排过渡。现在没有对外订阅者，也没有宣称已经稳定的公共消息 API。

## Outbox 原子性与重试

EfCommandTransaction 将 OutboxMessage 与领域变更、待办、审计、幂等结果一并提交。失败时不留可投递消息；同幂等键重放直接使用原响应，不重新生成事件。事件 ID 在首次成功命令中确定，投递重试不会改变它。

[OutboxDispatcher](../backend/ClinicFlow.Infrastructure/Messaging/OutboxDispatcher.cs) 以 ReadCommitted 事务批量选取到期未发送记录，使用 MySQL `FOR UPDATE SKIP LOCKED` 避免多个工作者同时领取同一行。它在持有行锁时调用 publisher，随后写入 PublishedUtc 并提交；单批限 1–100 条，传输实现应提供超时与取消支持，避免长时间占锁。

发布失败保存尝试次数、异常类型及下次重试时间，不保存可能含敏感数据的异常消息。退避从 2 秒递增到 256 秒。取消会回滚本批数据库更新。若远端已接收而本地提交失败、进程退出或取消，则消息会再次发送：这不是 exactly-once。消费者必须按 EventId 去重，并让去重记录与本地业务处理原子提交；将来接入消费者时再实现 Inbox。

同一批的多个事件、多个工作者以及失败重试之间没有严格顺序保证。需要顺序的消费者须设计聚合序号、版本检查或重新查询权威状态，当前 V1 不能充当有序状态复制协议。

## 如何启用真实投递

默认启动只持久化 Outbox，没有假 publisher、后台轮询任务或消息中间件依赖。因此 PublishedUtc 为空是当前预期行为，不能解释为“已经发给外部系统”。

接入时在 Infrastructure 实现 IIntegrationEventPublisher；宿主调用 `AddClinicOutboxDelivery<YourPublisher>()`，并安排带独立 DI scope 的后台任务调用 `DispatchBatch`。该扩展只注册服务，不自动创建工作者。真实接入还需要确定目标地址、凭据、协议、确认语义、超时、监控、告警、消费者去重和消息保留期限。

运营可查询待发量、最早 OccurredUtc、Attempts、LastError、NextAttemptUtc。不要为清理积压而直接标记成功；重试前查明失败原因。当前没有自动清理或死信阈值，持续失败会继续重试。已发送记录的归档与删除需要接入方明确保留策略。

AddIntegrationOutbox 为新增表迁移。历史数据不补发事件；回退此迁移会删除表及待发消息，需先保全消息并协调消费者。详见[数据库与升级](database-design.md)。

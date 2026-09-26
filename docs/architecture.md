# 架构与数据模型

ClinicFlow是学习用途的模块化单体。一个ASP.NET Core进程承载API及HostedService；React从同源服务读取数据；MySQL是业务事实来源。独立Node/SQLite服务模拟预约消息接收方；可选Orthanc服务提供影像归档及DICOMweb，Stone提供显示。模块通过明确业务服务协作，不为每个类机械创建接口。

```mermaid
flowchart LR
  UI[React / TypeScript] --> API[认证、CSRF、角色与API]
  API --> Scheduling[SchedulingService]
  API --> FHIR[FHIR只读映射]
  Scheduling --> DB[(MySQL)]
  FHIR --> DB
  Worker[OutboxWorker / Dispatcher] --> DB
  Worker -->|HTTP，事务外| Mock[Node接收端]
  Mock --> External[(SQLite receipts + snapshots)]
```

边界：Identity.cs管理注册账号、个人档案绑定与演示身份；Scheduling目录表达预约及事务；AuditEntry以同一事务追加；Integration目录管理外部投递；Fhir目录映射只读协议。ClinicDb统一映射与迁移是本单体的共享基础设施。外部HTTP用HttpClient、时间用TimeProvider、故障屏障用ITransactionProbe替换；正常运行只注册NoTransactionProbe。

```mermaid
erDiagram
  Patient |o--o| User : binds
  Patient ||--o{ Appointment : identifies
  Resource ||--o{ Appointment : schedules
  Appointment ||--o{ SlotClaim : occupies
  Resource ||--o{ SlotClaim : protects
  Appointment ||--o{ PrerequisiteTask : requires
  Appointment ||--o{ AuditEntry : records
  Appointment ||--o{ OutboxMessage : snapshots
  OutboxMessage ||--o{ SyncAttempt : delivers
```

图中Audit/Outbox关联是逻辑关联，模型中未全部设置数据库外键；预约不提供删除API。SlotClaim与Task有数据库外键，SlotClaim的复合主键是有效占用唯一约束。IdempotencyRecord按主体、操作和调用方键的SHA256主键定位，成功响应在业务事务内持久化，不自动清理。

```mermaid
sequenceDiagram
  participant C as 浏览器
  participant S as SchedulingService
  participant D as MySQL
  C->>S: create + Idempotency-Key
  S->>D: BEGIN READ COMMITTED
  S->>D: 插入/锁幂等记录
  alt 已成功请求
    D-->>S: 原响应
  else 首次请求
    S->>D: 按ID升序锁资源
    S->>D: 检查占用，保存预约/占用/任务/审计/Outbox/响应
  end
  S->>D: COMMIT
  S-->>C: 本地业务结果
```

```mermaid
sequenceDiagram
  participant C as 浏览器
  participant S as SchedulingService
  participant D as MySQL
  C->>S: reschedule + Version + Key
  S->>D: BEGIN，幂等锁，预读旧ResourceId
  S->>D: 升序锁旧/新资源，再锁预约
  S->>S: 重验ResourceId与Version
  S->>D: 检查目标，删除旧占用并flush（未提交）
  S->>D: 新占用、Pending、重置任务、递增版本、审计/Outbox
  alt 任一步失败
    S->>D: ROLLBACK（保留完整旧状态）
  else 成功
    S->>D: COMMIT
  end
```

```mermaid
sequenceDiagram
  participant W as Dispatcher
  participant D as MySQL
  participant E as 外部接收端
  W->>D: 短事务 SKIP LOCKED领取，持久化attempt/lease/token
  W->>E: HTTP完整快照与MessageId（无DB事务）
  E->>E: 同事务去重+仅应用更高版本
  E-->>W: 响应（可能丢失）
  W->>D: 短事务验证token与租约期限，保存结果/退避
```

SQL Server对照：可用UPDLOCK/HOLDLOCK等锁策略及唯一索引，但不能原样复制MySQL FOR UPDATE/SKIP LOCKED；SQL Server rowversion也不等于业务Version。双数据库运行未列入本项目实现。详细取舍见adr/001–005；未来扩大吞吐可将资源父锁细化，但必须重新验证所有改期和锁顺序。


## 数据库字段与存储边界

完整的13张业务表、字段类型、可空性、主外键、索引、迁移顺序，以及AI会话/接收端/影像归档的存储边界统一见[数据库表设计与数据字典](database-design.md)。上方ER图仅展示预约主线，完整关系图以该文档为准。

## 影像集成边界

Imaging 模块通过独立身份映射把预约患者与外部影像患者对应起来，关联表保存 Study UID。影像像素不进入业务 MySQL。关联与审计原子保存；解除只修改本地关系，不删除外部影像，也不改变预约 Version/Outbox。查看器的静态资源和影像请求都经过受限应用代理与身份重验证。完整调用图、失败语义和局限见 [影像设计](imaging/design.md)。

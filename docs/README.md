# OIS 文档导航

学习主线：[ClinicFlow 项目开发顺序详解](project-development-learning.md)。从真实演进历史出发，讲解需求、工程、建模、事务、临床流程、接口、工作台和交付之间的关系，不设置动手任务。

入门学习：[从 Java 到 ASP.NET Core：按项目开发顺序入门](aspnet-core-learning.md)。结合本项目理解工程、C#、配置与 DI、EF Core、业务与 API、安全、测试和部署。

数据库实战：[EF Core：从接入 MySQL 到完成 CRUD](ef-core-learning.md)。包含完整学习工程、关键映射配置、迁移、关联查询、事务、并发与排障。

专项讲解：[预约流程：实体设计与冲突处理](appointment-design-and-conflicts.md)。结合类关系图、状态图、事务时序图与处理流程图，说明患者和资源占用、改期恢复、并发保护与治疗放行。

推荐复盘阅读顺序：

1. [范围与流程](ois-mvp.md)：先理解角色、预约、审核验证和治疗执行。
2. [领域语言](../CONTEXT.md) → [上下文地图](../CONTEXT-MAP.md)：理解术语、所有权和协作。
3. [架构](architecture.md) → [分层与事件决策](adr/0002-layered-events.md)：区分四层与三个业务上下文。
4. [领域事件与集成事件](events.md)：理解事务内联动、Outbox、版本和至少一次投递。
5. [项目开发顺序详解](project-development-learning.md)：串起实现过程；按文中源码入口跟读一个完整用例。
6. [ASP.NET Core 入门](aspnet-core-learning.md) → [EF Core 实战](ef-core-learning.md)：按需补语言、框架与持久化知识。
7. [操作与接口](ois-runbook.md) → [数据库与升级](database-design.md)：核对契约、存储和迁移。
8. [运行手册](runbook.md) → [验收](acceptance.md)：启动项目、运行测试并复核证据。
9. [模块化单体决策](adr/0001-modular-monolith.md)：回顾为何保留同步事务；并发演进见[时间格占用决策](adr/0003-slot-occupancy.md)。

接手维护可先看[开发交接](../开发交接.md)；需要快速查找端点时看[API 索引](api.md)。产品范围、接口契约、数据库迁移和验收证据分别以上述对应文档为准，学习文档中的独立示例不代表新增产品功能。

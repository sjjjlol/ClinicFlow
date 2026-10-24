# OIS 文档导航

学习主线：[ClinicFlow 项目开发顺序详解](project-development-learning.md)。从真实演进历史出发，讲解需求、工程、建模、事务、临床流程、接口、工作台和交付之间的关系，不设置动手任务。

入门学习：[从 Java 到 ASP.NET Core：按项目开发顺序入门](aspnet-core-learning.md)。结合本项目理解工程、C#、配置与 DI、EF Core、业务与 API、安全、测试和部署。

数据库实战：[EF Core：从接入 MySQL 到完成 CRUD](ef-core-learning.md)。包含完整学习工程、关键映射配置、迁移、关联查询、事务、并发与排障。

1. [范围与流程](ois-mvp.md)：三个角色、单次预约、审核验证和执行。
2. [运行手册](runbook.md)：Docker、本机开发、测试、升级。
3. [操作与接口](ois-runbook.md)：命令字段、状态和错误。
4. [架构](architecture.md)：模块边界与事务。
5. [数据库](database-design.md)：当前表结构、历史清理与验证记录分离迁移。
6. [验收](acceptance.md)：覆盖范围和验证命令。
7. [领域语言](../CONTEXT.md)：统一术语。
8. [上下文地图](../CONTEXT-MAP.md)：三个业务模块的数据所有权和协作。
9. [模块化单体决策](adr/0001-modular-monolith.md)：为何保留同步事务与全局写锁。

接手维护可先看[开发交接](../开发交接.md)；需要快速查找端点时看[API 索引](api.md)。产品范围、接口契约、数据库迁移和验收证据分别以上述对应文档为准，学习文档中的独立示例不代表新增产品功能。

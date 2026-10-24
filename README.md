# ClinicFlow OIS

单院区放疗协作演示系统：预约人员、诊疗人员（医生与治疗师合并）、物理师各有工作台。

覆盖单次门诊/治疗预约、日周资源日历、诊疗与处方、计划版本、审核、机器实验排期、物理验证、失败退回、治疗执行、站内待办和审计。不使用 AI Agent，不提供患者自助预约、疗程批量排期、真实剂量计算或设备控制。

## 启动

```sh
./scripts/start.sh
```

打开 http://localhost:5080。账号为 `scheduler`、`clinician`、`physicist`；初次初始化的密码取自本地 `.env` 的 `DEMO_PASSWORD`，至少 8 个字符；已有账号不会随该变量修改而自动重置。需要 Docker Compose、Bash、curl、OpenSSL。

## 文档

- [项目开发顺序详解（业务流程、源码链路与设计原因）](docs/project-development-learning.md)
- [ASP.NET Core 入门（面向 Java 开发者，按项目开发顺序）](docs/aspnet-core-learning.md)
- [EF Core 实战（配置、映射、迁移与完整 CRUD 示例）](docs/ef-core-learning.md)
- [产品范围](docs/ois-mvp.md)
- [运行与验证](docs/runbook.md)
- [操作与 API](docs/ois-runbook.md)
- [架构](docs/architecture.md) · [数据库与升级](docs/database-design.md)
- [验收范围](docs/acceptance.md) · [文档导航](docs/README.md)

架构采用三个业务模块的单体：诊疗管理、计划与验证、排程与执行，共享事务保证治疗条件检查与执行一致。见[上下文地图](CONTEXT-MAP.md)和[架构说明](docs/architecture.md)。

技术栈：ASP.NET Core / EF Core / MySQL，React / TypeScript / Vite。

升级已有数据库前先备份并停止旧版写入：本次 `SeparatePhysicalValidation` 迁移将历史验证字段搬到独立表；更早版本还会执行删除旧模块数据和非 OIS 账号的 `RemoveLegacyModules` 迁移。两种迁移的回退能力不同，见[升级说明](docs/database-design.md)。

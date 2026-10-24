# ClinicFlow 上下文地图

当前三个候选限界上下文以模块化单体实现。共享领域术语见 [CONTEXT.md](CONTEXT.md)，实现与一致性见[架构](docs/architecture.md)。

## 上下文

- [诊疗管理](backend/ClinicFlow.Domain/Clinical/CONTEXT.md)：诊疗档案、处方版本及当前方案引用，提供患者资料查询。
- [计划与验证](backend/ClinicFlow.Domain/Planning/CONTEXT.md)：治疗计划、审核、验证记录和放行条件。
- [排程与执行](backend/ClinicFlow.Domain/Scheduling/CONTEXT.md)：患者预约、机器占用与单次治疗执行。

## 关系

- Clinical → Planning：提供当前处方、档案状态及结构化信息是否完整；Planning 通过 Clinical 更新当前计划引用。
- Planning → Scheduling：提供计划与放行条件；Scheduling 提供实验时段、机器条件以及完成占用的命令。
- Clinical ↔ Scheduling：修订或终止前检查正在治疗的执行，变更后创建受阻预约待办；Scheduling 查询患者与档案摘要。
- Support：保存三个业务模块产生的待办和个人草稿；失败待办是否可解决由 Planning 判断。

这些关系目前是同一命令事务内的同步协作，没有独立服务、跨进程消息或最终一致性读模型。三个角色工作台可以调用多个上下文，不按角色划分数据所有权。

技术层采用 Domain → 被 Application 引用、Infrastructure 实现应用端口、API 装配的四层结构。领域事件在本地事务内处理；集成事件经 Outbox 等待可选外部投递，当前没有实际跨进程订阅。见[事件设计](docs/events.md)。

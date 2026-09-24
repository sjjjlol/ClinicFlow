# 用户独立任务：预约完成 Completed

初版没有实现Completed。该需求由你自行设计、编码、测试和提交；这里不提供完整答案。

先回答：哪些状态允许完成？哪个角色有权限？完成时占用立即释放还是保留到原结束时间？能否撤销？列表、资源查询、审计、Outbox与FHIR如何表达？与取消在语义上有什么不同？已有客户端或外部服务遇到新状态怎么办？

代码入口：Scheduling/Models.cs的状态字段；SchedulingService.Mutate的状态变更；Endpoints.cs的权限；Scheduling.tsx的操作按钮；Dispatcher与模拟接收端快照契约；FhirAdapter状态映射；SchedulingTests与Playwright流程。

验收骨架（由你补期望，不给实现）：合法前置状态成功；非法前置状态失败；旧版本冲突；权限绕过失败；同Key重放一致；多时段占用符合你记录的规则；业务/审计/Outbox原子提交；外部乱序仍不倒退；页面状态可解释。给新规则一个需求ID，记录ADR和独立commit，最后接受代码追问。

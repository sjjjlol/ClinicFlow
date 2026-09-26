# Completed：已实现的业务闭环

此功能原为独立练习，现根据继续开发请求实现。规则与账号权限见[账号及预约闭环](accounts-and-lifecycle.md)。

Scheduler仅可将已到结束时间的Confirmed预约登记为Completed。完成释放SlotClaims，保留预约、任务、审计和Outbox；状态不可撤销。重复相同幂等键返回原成功结果，不重复产生副作用。FHIR映射为fulfilled，模拟接收端支持Completed并继续按版本防止乱序回退。

代码入口：SchedulingService.Mutate的complete分支、Endpoints的schedule策略、Scheduling.tsx的登记完成按钮、FhirAdapter映射、mock-external/server.mjs的状态校验。

验证入口：AccountLifecycleTests（合法前置状态、结束时间边界、旧版本冲突、重放、终态限制、失败回滚及并发）；http-accounts.mjs（真实角色权限与完整闭环）；accounts.spec.ts（浏览器角色协作与终态按钮）；receiver.test.mjs（Completed持久化、重放与乱序）。

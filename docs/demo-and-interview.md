# 演示与面试准备

## 三分钟演示

0:00–0:30：说明这是AI协助搭建的学习项目，虚构数据、模块化单体、无临床计算。以Scheduler登录，创建四个连续15分钟时段的预约，展示Pending与外部同步是两种状态。

0:30–1:10：再次选择同一资源时间，展示可操作的409冲突；打开详情，尝试未完成任务就确认，展示服务端前置规则。切换TaskOperator完成两项任务，再由Scheduler确认。

1:10–1:50：改期到部分重叠时段或另一资源，说明事务回滚和任务重置。取消后展示占用释放与审计历史。切Admin看同步队列和投递记录。

1:50–2:30：展示受控并发测试结果或L1两次写入时间线。讲清唯一约束、资源父锁、业务Version与请求Key各解决什么问题。

2:30–3:00：展示已提交的升级恢复记录与FHIR范围。说明下一步是自己实现Completed，并明确哪些内容已经亲自修改、哪些仍是阅读理解。

## 90-second project introduction（练习稿）

ClinicFlow is a learning project I use to practice C# and ASP.NET Core with a realistic scheduling workflow. The first version was built with AI assistance. I distinguish the parts I have reviewed from the changes I have implemented myself.

The application uses React, a modular ASP.NET Core service, and MySQL. A booking occupies multiple fifteen-minute slots. Resource locks and a unique slot constraint prevent double booking. A business version rejects stale edits, while an idempotency key lets a client safely repeat a request whose response was lost.

The booking transaction also writes an audit entry and an Outbox snapshot. A background worker claims messages with a bounded lease and calls a separate mock receiver. The receiver persists message identities and only applies newer appointment versions. This is at-least-once delivery with deduplication, not an exactly-once guarantee.

I validate the important failures with real MySQL tests, browser workflows, and isolated exercises. The project also includes a small FHIR R4 read adapter and a tested database upgrade and restore procedure. It is not deployed clinical software or an Elekta product.

## 代码追问

| 问题 | 应能指出的代码与理由 |
|---|---|
| 先查空闲再插入为何不够？ | CheckFree外面的资源锁与SlotClaim复合主键；两个请求会同时读到空闲 |
| 为什么锁后再查版本/资源？ | Mutate预读可能过时；A08受控屏障测试 |
| 改期部分重叠如何处理？ | 旧占用删除与SaveChanges仍在事务内；A05故障恢复与A06 |
| 请求Key能替代Version吗？ | Key防同命令重复效果，Version拒绝不同旧决策；A11优先重放 |
| 为什么不用数据库事务包HTTP？ | 远端效果不可回滚，长锁降低吞吐；Outbox提交后发 |
| worker崩溃会丢消息吗？ | 持久租约超时可重领；过期token不能回写；仍可能重复投递 |
| 老取消前的消息晚到怎么办？ | receiver WHERE excluded.version > snapshots.version |
| async会为每个请求建线程吗？ | IO等待释放线程；本项目没有用Task.Run包装HTTP/SQL |
| 你的测试证明什么、不证明什么？ | 真实MySQL不变量与受控失败；不是生产负载、法规认证或全量FHIR兼容 |
| 索引改善数字来自哪里？ | docs/evidence/L3-plan.txt的固定规模/参数/计划；不能称企业收益 |

## English PR description example（练习）

**Prevent stale edits from overwriting rescheduled appointments**

A client could act on an appointment snapshot read before another request moved it to a different resource. The mutation now locks resources in a stable order, locks the appointment, and revalidates the resource and business version before changing any claims. Stale requests receive a conflict that the UI can explain.

Validation includes independent MySQL connections, a controlled pre-read barrier, self-overlapping moves, and a fault injected after releasing the original claims. Rollback preserves the original booking, audit, and Outbox state.

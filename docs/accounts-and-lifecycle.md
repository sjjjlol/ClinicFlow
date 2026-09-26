# 注册账号与预约闭环

登录页点击“没有账号？注册预约账号”，输入账号、演示姓名、密码与确认密码。账号为3–40位英文字母、数字、下划线或短横线，首位为字母或数字；服务端统一去除首尾空格并转小写。密码8–128字符，姓名1–80字符。注册成功直接进入“我的预约”，每个账号只绑定自己的一个档案。密码经ASP.NET PasswordHasher散列后存储。

若注册成功但响应中断，再次注册会提示账号已存在，此时使用刚才的账号密码登录。账号没有邮箱/手机号验证、找回密码、SSO或亲友代约功能；仍是本地学习演示，请使用虚构姓名。

## 业务流程

1. Booker为自己创建未来预约，状态为Pending，立即保留资源时段。
2. TaskOperator完成资料核对与预约信息复核。
3. Scheduler确认，状态为Confirmed。
4. 预约到达结束时间后，Scheduler在详情点击“登记完成”并确认，状态为Completed。
5. Completed为终态，不可取消、改期、确认或再次完成；释放资源占用，保留历史记录。

Booker在预约开始前可改期或取消。改期后返回Pending并重置前置核对，需要工作人员重新确认；取消为Cancelled终态。开始后需联系工作人员处理。Scheduler保留历史补录及纠错能力。系统不会自动把超时预约标为已完成。

## 权限

| 操作 | Booker | Scheduler | TaskOperator | Admin |
|---|---|---|---|---|
| 查看预约、详情、Patient/FHIR | 仅本人 | 全部 | 全部 | 全部 |
| 创建预约 | 本人、未来时间 | 全部 | 无 | 无 |
| 改期/取消 | 本人且开始前 | 全部非终态 | 无 | 无 |
| 完成前置核对 | 无 | 无 | Pending预约 | 无 |
| 确认/登记完成 | 无 | 有 | 无 | 无 |
| 同步队列/重试 | 无 | 无 | 无 | 有 |

预约归属按Patient而不是创建账号判断：工作人员代Booker创建的预约也归该Booker。读取他人预约、FHIR或修改他人预约返回404；为其他档案创建预约返回403。资源占用查询共享，但仅返回资源和时间，不暴露预约ID或患者ID。AI助手现已开放给Booker，固定本人档案；Scheduler仍使用演示档案。两者均支持Pi Agent Core驱动的流式文字、查询进度与候选确认。

## 升级及验证

RegisteredAccounts迁移只给Users增加可空PatientId、唯一索引和外键。原有三种角色、Patient、预约和密码保持不变。部署顺序为更新模拟接收端支持Completed，再更新应用；启动时应用迁移。不要用down -v升级，否则会删除数据。建议升级前保留数据库备份。

验证命令（实际执行日期及结果见[验收记录](acceptance.md)）：`./scripts/test.sh`，`./scripts/http-tests.sh`（包括http-accounts.mjs），`node --test mock-external/receiver.test.mjs`，`./scripts/upgrade-drill.sh`，`npm --prefix frontend run test:e2e`。

HTTP和浏览器测试会创建虚构账号与预约，应在测试数据库运行；后端测试自动使用独立的clinicflow_tests数据库。注册每IP每分钟5次、登录30次，连续多轮测试时需等待限流窗口结束。

## Completed 实现与代码入口

Scheduler仅可将已到结束时间的Confirmed预约登记为Completed。完成释放SlotClaims，保留预约、任务、审计和Outbox；状态不可撤销。重复相同幂等键返回原成功结果，不重复产生副作用。FHIR映射为fulfilled，模拟接收端支持Completed并继续按版本防止乱序回退。

代码入口：SchedulingService.Mutate的complete分支、Endpoints的schedule策略、Scheduling.tsx的登记完成按钮、FhirAdapter映射、mock-external/server.mjs的状态校验。

验证入口：AccountLifecycleTests（合法前置状态、结束时间边界、旧版本冲突、重放、终态限制、失败回滚及并发）；http-accounts.mjs（真实角色权限与完整闭环）；accounts.spec.ts（浏览器角色协作与终态按钮）；receiver.test.mjs（Completed持久化、重放与乱序）。

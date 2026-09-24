# Java开发者的代码阅读路线

从一次创建预约开始，先运行再读，不必从所有实体逐行读起。

1. `frontend/src/Scheduling.tsx`：submit把上海本地时间转成带Z的UTC，保留重试用Idempotency-Key；`api.ts`负责Cookie同源请求与CSRF。观察409资源/版本/幂等错误的不同提示。浏览器按钮权限只是体验，服务端仍校验。
2. `backend/Program.cs`：看DI注册和中间件顺序。认证建立ClaimsPrincipal，授权检查角色，随后校验CSRF，再进入端点。DbContext是Scoped，不能让后台单例长期持有它。和Spring Security过滤器链可以对照，但ASP.NET endpoint metadata与运行管道的顺序要单独理解。
3. `backend/Scheduling/Endpoints.cs`：参数绑定、CancellationToken、端点策略。返回对象交给JSON序列化，BusinessException由统一错误映射处理。可空`int?`表示可能不存在；`!`是编译器空分析断言，不是运行时判空。读出业务输入record与数据库可变class的区别。
4. `SchedulingService.Create/Execute`：找到事务的开始、flush和commit。C#的`await using`调用异步Dispose，即使异常也释放事务/连接。相比@Transactional，本项目显式编排事务；中途SaveChanges不等于提交事务。异常离开作用域会回滚。
5. `ClinicDb.OnModelCreating`与Migrations：LINQ查询被翻译为SQL，AsNoTracking避免读操作加入变更跟踪；状态变更实体被跟踪后SaveChanges生成UPDATE。EF跟踪不防止业务并发，数据库行锁和Version检查各有职责。一个DbContext不可并发执行两个await查询。
6. `SchedulingService.Mutate`：预读不是锁；资源按序锁、再锁预约、再验证资源与版本。部分重叠改期为什么先flush删除再插入？故障测试after-release验证该中间状态没有泄漏为成功。
7. `Integration/Dispatcher.cs`：HostedService是生命周期管理的异步循环，每次CreateAsyncScope取得Scoped依赖。async/await释放等待中的线程，不表示另开线程，也不需要为IO套Task.Run。CancellationToken沿调用链传递；应用停止时留下租约恢复，不猜测HTTP是否已生效。
8. `mock-external/server.mjs`：外部不是同一事务的一部分。理解唯一receipt与版本条件更新为何必须一起提交，再运行响应丢失和乱序测试。

## 语言与运行时重点

- `record Booking/Mutation`使用值语义；`with`生成修改后的副本，用于UTC规范化。持久实体是可变class，不要机械将JPA模型全翻成record。
- 集合表达式`[1,2]`、LINQ Select/Where、lambda和Java Stream相似，但IQueryable表达式树由provider翻译；ToListAsync是执行边界。客户端集合和数据库查询不是同一种求值环境。
- nullable引用分析是编译期工具；外部JSON、数据库和反序列化仍需要运行时校验。DateTimeOffset用于入站偏移，持久化UTC DateTime，再由UtcDateTimeConverter明确输出Z。
- `Task<T>`是异步操作结果；`TaskCompletionSource`在测试中作为受控屏障。不要用Thread.Sleep制造“看起来同时”，也不要把本项目async测试当压测。
- DI注册Singleton/Scoped/Transient与Spring的生命周期概念有相似性，但线程安全和请求作用域并不自动保证。TimeProvider singleton可以替换；DbContext scoped必须随工作单元释放。
- 配置从环境变量读取，`ConnectionStrings__Clinic`映射嵌套键。日志只包含关联ID和错误类别，不记录密码、Cookie或集成密钥。
- 测试组合：xUnit纯规则与真实MySQL事务、Node HTTP契约、Playwright完整业务流程。InMemory provider不用于证明锁和回滚。

## 七天安排（每天3–4小时）

| 天 | 任务 | 自己提交的学习证据 |
|---|---|---|
| D1 | 一键启动、使用页面、读Program与实体 | 画创建请求调用链，解释三个C#特性 |
| D2 | 读身份/端点/LINQ/迁移 | 修改一个查询条件，解释生成SQL和权限 |
| D3 | 创建/改期/幂等与L1 | 画双客户端锁时间线，说明冲突与回滚 |
| D4 | HostedService/Outbox与L2 | 演示断网和响应丢失，解释结果未知 |
| D5 | 测试/RCA/L3 | 写一份带执行计划证据的故障分析 |
| D6 | 用户独立Completed需求 | 自己的功能提交与失败路径测试 |
| D7 | 升级演练/FHIR/英文说明 | 3分钟演示、90秒介绍与代码追问 |

五天版本可合并D1/D2与D6/D7，不能跳过亲手修改。遇到问题先用`docs/acceptance.md`找对应测试，再回到业务不变量。

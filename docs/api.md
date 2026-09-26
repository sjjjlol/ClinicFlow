# API契约与错误处理

登录后GET `/api/openapi/v1.json`读取随代码生成的OpenAPI文档。主业务端点使用JSON，时间入站DateTimeOffset、保存及输出UTC，页面显示Asia/Shanghai。预约写操作返回200及预约快照；注册返回201并建立登录态；logout返回204。FID/路径大小写以生成文档为准。

| 方法与路径 | 权限/用途 |
|---|---|
| GET /api/health | 匿名，数据库连通状态 |
| GET /api/auth/csrf | 匿名或登录态，当前身份的CSRF令牌 |
| POST /api/auth/register | 匿名；username/password/displayName；每IP每分钟5次；角色固定Booker |
| POST /api/auth/login | 注册账号或预置用户名与密码；每IP每分钟30次 |
| POST /api/auth/logout；GET /api/auth/me | 登录态 |
| GET /api/patients；GET /api/resources | 全部登录角色；Booker仅能读取自己的档案，资源目录共享 |
| GET /api/resources/{id}/slots?start=...&end=... | 只返回resourceId/slotStartUtc，不含预约或患者ID；最多7天；查询结果不保证提交时仍空闲 |
| GET /api/appointments | page、size、status、resourceId；Booker仅列本人预约；页码夹限1–100000、页大小1–100；StartUtc降序、Id升序 |
| GET /api/appointments/{id} | 预约、任务、审计、同步记录；Booker仅本人，访问他人返回404 |
| POST /api/appointments | Scheduler或Booker；patientId/resourceId/startUtc/endUtc；Booker只可使用绑定档案及未来时间 |
| POST /api/appointments/{id}/reschedule | Scheduler或本人Booker；version/resourceId/startUtc/endUtc；Booker只可在开始前改为未来时间 |
| POST /api/appointments/{id}/cancel | Scheduler或本人Booker；version；Booker只可在开始前取消 |
| POST /api/appointments/{id}/confirm | Scheduler；version；须完成任务 |
| POST /api/appointments/{id}/complete | Scheduler；version；仅Confirmed且已到结束时间，完成后释放占用，成为终态 |
| POST /api/appointments/{id}/complete-task | TaskOperator；version/taskId |
| GET /api/sync；GET /api/sync/{id}/attempts | Admin；最近100条消息/某消息所有尝试 |
| POST /api/sync/{id}/retry | Admin；空JSON对象；只重试Failed消息 |

所有POST携带`X-CSRF-TOKEN`，注册/登录/退出后刷新令牌。预约及同步重试写操作还必须携带1–128字符`Idempotency-Key`（影像关联采用数据库唯一约束，不提供幂等响应回放）。同主体、同操作、同Key、同规范化内容返回原成功快照，包含原版本；不保证它仍是最新状态，UI随后重新读取详情。不同内容复用Key返回409。网络响应丢失时重用原Key；用户修改内容应生成新Key。初版React在当前表单/操作期间保留Key；刷新整个页面会丢失未完成表单上下文，不能声称支持跨浏览器崩溃恢复草稿。

| HTTP / code | 含义与用户动作 |
|---|---|
| 400 invalid_time / invalid_key / csrf / invalid_request | 修正输入或刷新安全令牌 |
| 401 | 登录失效，重新登录 |
| 403 | 角色无权操作，切换正确演示角色 |
| 404 patient_missing/resource_missing/appointment_missing/task_missing/message_missing | 重新读取对象；部分只读404无自定义body |
| 400 invalid_username/invalid_password/invalid_name | 修正注册资料 |
| 403 patient_forbidden | 只能为自己的档案预约 |
| 409 username_taken | 换账号或登录；注册响应中断时先尝试登录 |
| 409 appointment_started/appointment_not_ended | 自助调整须在开始前，登记完成须在结束后 |
| 429 | 请求过于频繁，稍后重试 |
| 409 slot_conflict | 换资源或时间 |
| 409 version_conflict | 刷新详情，用新快照重新决定 |
| 409 idempotency_conflict | 不要更改既有Key的含义，生成新操作 |
| 409 invalid_state/prerequisites_incomplete/task_completed | 按当前状态完成前置动作 |
| 409 concurrent_conflict | 事务失败，可用原Key安全重试；没有无限自动重试 |
| 409 sync_not_failed | 队列已改变，刷新后检查 |
| 500 internal_error | 记录X-Correlation-ID；结果未知时保留原Key再查/重试 |

认证授权由服务端保证。普通角色不能直接修改/删除审计，项目没有这种端点。审计同事务追加不等于防篡改合规存储。FHIR只读范围与不同错误语义见[fhir-r4.md](fhir-r4.md)。

## 预约Agent

`/api/agent/config`、`/messages`、`/{sessionId}/confirm`允许Scheduler和Booker。Booker强制绑定本人档案，客户端和模型均不能覆盖。新增对应的`/messages/stream`与`/{sessionId}/confirm/stream`，POST请求仍需CSRF，返回text/event-stream（progress/session/message_start/delta/trace/result/error）。只有result代表本轮完整结果；流中error携带业务code/status，提前断流不得当作成功。模拟冲突接口保持仅Scheduler且需开发环境开关。


## 影像业务 API

以下路径以 `/api/imaging/{appointmentId}` 为前缀。仅 Scheduler/TaskOperator；所有响应 `Cache-Control: no-store`；POST 仍需 CSRF。路径中的 Study UID 是标准影像标识，appointmentId 是内部预约标识。

| 方法与后缀 | 行为 |
|---|---|
| GET `/` | 本地身份映射、关联列表和最近30条影像审计；不依赖影像服务在线 |
| GET `/search` | 当前患者的候选检查；最多100项，返回items/truncated；缺少身份映射409 |
| POST `/links` | body `{ "studyInstanceUid": "..." }`；成功204，重复或错配409 |
| POST `/links/{uid}/remove` | body `{}`；解除关系并审计，成功204，不存在404 |
| GET `/studies/{uid}/metadata` | 已关联检查的序列/实例DTO；重验证患者身份 |
| GET `/studies/{uid}/series/{series}/instances/{instance}/file` | 解包后原始DICOM文件下载 |
| GET `/studies/{uid}/viewer/index.html?study={uid}` | 受控Stone入口；插件内部资源不列入业务OpenAPI |
| GET `/studies/{uid}/dicom-web/{resource}` | 受限GET代理，仅当前已关联Study，用于查看器 |

影像服务不可用503，格式异常502，跨检查代理请求403，关联不存在404。业务错误包含code/message/correlationId。见 [错误码和标准范围](imaging/design.md)。这是应用业务 API 与受限代理，不能当作完整公开 DICOMweb 服务端。

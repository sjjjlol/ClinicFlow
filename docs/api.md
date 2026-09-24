# API契约与错误处理

登录后GET `/api/openapi/v1.json`读取随代码生成的OpenAPI文档。主业务端点使用JSON，时间入站DateTimeOffset、保存及输出UTC，页面显示Asia/Shanghai。自定义写操作返回200及预约快照；logout返回204。FID/路径大小写以生成文档为准。

| 方法与路径 | 权限/用途 |
|---|---|
| GET /api/health | 匿名，数据库连通状态 |
| GET /api/auth/csrf | 匿名或登录态，当前身份的CSRF令牌 |
| POST /api/auth/login | 预置用户名与密码；每IP每分钟30次 |
| POST /api/auth/logout；GET /api/auth/me | 登录态 |
| GET /api/patients；GET /api/resources | 全部登录角色 |
| GET /api/resources/{id}/slots?start=...&end=... | 查询已占用15分钟时段，最多7天；查询结果不保证提交时仍空闲 |
| GET /api/appointments | page、size、status、resourceId；页码夹限1–100000、页大小1–100；StartUtc降序、Id升序 |
| GET /api/appointments/{id} | 预约、任务、审计、同步记录 |
| POST /api/appointments | Scheduler；patientId/resourceId/startUtc/endUtc |
| POST /api/appointments/{id}/reschedule | Scheduler；version/resourceId/startUtc/endUtc |
| POST /api/appointments/{id}/cancel | Scheduler；version |
| POST /api/appointments/{id}/confirm | Scheduler；version；须完成任务 |
| POST /api/appointments/{id}/complete-task | TaskOperator；version/taskId |
| GET /api/sync；GET /api/sync/{id}/attempts | Admin；最近100条消息/某消息所有尝试 |
| POST /api/sync/{id}/retry | Admin；空JSON对象；只重试Failed消息 |

所有POST携带`X-CSRF-TOKEN`，登录/退出后刷新令牌。业务写操作还必须携带1–128字符`Idempotency-Key`。同主体、同操作、同Key、同规范化内容返回原成功快照，包含原版本；不保证它仍是最新状态，UI随后重新读取详情。不同内容复用Key返回409。网络响应丢失时重用原Key；用户修改内容应生成新Key。初版React在当前表单/操作期间保留Key；刷新整个页面会丢失未完成表单上下文，不能声称支持跨浏览器崩溃恢复草稿。

| HTTP / code | 含义与用户动作 |
|---|---|
| 400 invalid_time / invalid_key / csrf / invalid_request | 修正输入或刷新安全令牌 |
| 401 | 登录失效，重新登录 |
| 403 | 角色无权操作，切换正确演示角色 |
| 404 patient_missing/resource_missing/appointment_missing/task_missing/message_missing | 重新读取对象；部分只读404无自定义body |
| 409 slot_conflict | 换资源或时间 |
| 409 version_conflict | 刷新详情，用新快照重新决定 |
| 409 idempotency_conflict | 不要更改既有Key的含义，生成新操作 |
| 409 invalid_state/prerequisites_incomplete/task_completed | 按当前状态完成前置动作 |
| 409 concurrent_conflict | 事务失败，可用原Key安全重试；没有无限自动重试 |
| 409 sync_not_failed | 队列已改变，刷新后检查 |
| 500 internal_error | 记录X-Correlation-ID；结果未知时保留原Key再查/重试 |

认证授权由服务端保证。普通角色不能直接修改/删除审计，项目没有这种端点。审计同事务追加不等于防篡改合规存储。FHIR只读范围与不同错误语义见[fhir-r4.md](fhir-r4.md)。

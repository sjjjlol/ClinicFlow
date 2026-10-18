# OIS API

业务命令完整契约见 [OIS 操作与接口](ois-runbook.md)。

|接口|用途|
|---|---|
|GET /api/health|数据库可连接性|
|GET /api/auth/csrf|签发 CSRF 请求令牌|
|POST /api/auth/login|工作人员登录，受限流保护|
|POST /api/auth/logout|退出当前会话|
|GET /api/auth/me|当前工作人员身份|
|GET /api/ois/workspace|OIS 一致性快照|
|POST /api/ois/cases、plans/{id}/review 等资源路径|专用请求类型及明确业务动作，完整列表见操作手册|
|PUT /api/ois/machines/{id}/availability|机器可用性|
|PUT /api/ois/cases/{id}/prescription-draft、plan-draft|本人草稿保存|
|GET /api/openapi/v1.json|已登录工作人员查看 OpenAPI|

Cookie 使用 HttpOnly 和 SameSite=Strict。写请求带 X-CSRF-TOKEN，登录/退出后刷新令牌；业务命令额外要求 Idempotency-Key。角色由会话确定，不信任客户端传入操作者。旧角色 Cookie 被拒绝，个人注册和旧业务 API 已移除。

OpenAPI 列出每个资源操作的专用请求体、路径参数、CSRF 和幂等头。旧 `/api/ois/commands/*` 已删除，返回 404 `not_found`。新旧请求通过存储兼容层保留历史幂等重放能力，前端与后端需同步升级。

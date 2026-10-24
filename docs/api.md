# OIS API

业务命令完整契约见 [OIS 操作与接口](ois-runbook.md)。

|接口|用途|
|---|---|
|GET /api/health|数据库可连接性；检查响应 status 是否为 ready，不能只看 HTTP 200|
|GET /api/auth/csrf|签发 CSRF 请求令牌|
|POST /api/auth/login|工作人员登录，受限流保护|
|POST /api/auth/logout|退出当前会话|
|GET /api/auth/me|当前工作人员身份|
|GET /api/ois/workspace|OIS 一致性快照|
|POST /api/ois/cases、POST /api/ois/plans/{id}/review 等资源路径|专用请求类型及明确业务动作，完整列表见操作手册|
|PUT /api/ois/machines/{id}/availability|机器可用性|
|PUT /api/ois/cases/{id}/prescription-draft、PUT /api/ois/cases/{id}/plan-draft|本人草稿保存|
|GET /api/openapi/v1.json|已登录工作人员查看 OpenAPI|

Cookie 使用 HttpOnly 和 SameSite=Strict。写请求带 X-CSRF-TOKEN，登录/退出后刷新令牌；业务命令额外要求 Idempotency-Key。角色由会话确定，不信任客户端传入操作者。旧角色 Cookie 被拒绝，个人注册和旧业务 API 已移除。

OpenAPI 列出每个资源操作的专用请求体、路径参数、CSRF 和幂等头。旧 `/api/ois/commands/*` 已删除，返回 404 `not_found`。历史请求通过存储兼容层保留幂等重放能力。仍使用旧命令路径的客户端需要升级；本次模块化重构保持现有资源路径、请求字段和工作台 JSON 不变。

后端分别由 Clinical、Planning、Scheduling 处理诊疗、计划验证、排程执行，Support 管理草稿与待办；这些是同一个应用内部的模块。接口路由按资源组织，不要求与模块名一一对应，例如实验结果提交位于 reservations 路径，业务归 Planning。

工作台中的验证字段由 ReservationResponse 将独立验证记录与预约合并输出。具体字段、版本含义、幂等重试和读取限制见[操作手册](ois-runbook.md)，数据所有权见[上下文地图](../CONTEXT-MAP.md)。

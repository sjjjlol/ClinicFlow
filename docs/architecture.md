# OIS 架构

浏览器 → 同源 ASP.NET Core API → OisService → OisWorkflow → MySQL。

- `frontend/src/OisApp.tsx`：三个角色工作台、表单预览确认、待办、历史和执行。
- `frontend/src/OisCalendar.tsx`：日/周日历、患者预约及机器占用。
- `backend/Identity.cs`：仅工作人员登录/退出，Cookie、CSRF 和登录限流；不提供注册。
- `backend/Ois/Workflow.cs`：确定性领域规则、角色约束、冲突检测、计划放行、状态转换。
- `backend/Ois/Service.cs`：一致性快照、写入事务、幂等响应和审计。
- `backend/ClinicDb.cs` 与 `backend/Ois/Models.cs`：当前持久化模型。

所有写命令先锁定 OisGate 单行，再读取状态并执行规则；领域变更、待办、审计和幂等结果同事务提交。读取快照用 RepeatableRead。客户端传目标版本，开始治疗还传已核对的计划 id，防止版本被静默替换。

部署只包含应用和 MySQL。前端由 ASP.NET 静态托管；开发时 Vite 代理 API。没有外部同步、影像服务器或模型服务运行依赖。

这是小规模演示设计：全量快照和单行串行写入不适用于生产规模；附件只是外部编号/引用，不上传、不解析。

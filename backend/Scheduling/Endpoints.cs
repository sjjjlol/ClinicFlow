using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Scheduling;

// ===== Minimal API 端点注册（对照 Spring MVC @RestController，但不用类/注解，全是显式代码）=====
public static class Endpoints
{
    // 扩展方法：this WebApplication app 让调用方写 app.MapScheduling()，像给 WebApplication 加了成员。
    public static void MapScheduling(this WebApplication app)
    {
        // MapGroup：路由分组 + 统一元数据（这里全组要求登录，≈ 类级 @PreAuthorize("isAuthenticated()")）。
        var group = app.MapGroup("/api").RequireAuthorization();
        group
            .MapPost(
                "/appointments",
                // 端点委托的参数绑定规则：
                //   Booking input        ← JSON body（record 反序列化，≈ @RequestBody）
                //   SchedulingService    ← DI 容器（Scoped）
                //   HttpContext ctx      ← 当前请求上下文（≈ HttpServletRequest/Response 合体）
                //   CancellationToken ct ← 客户端断开/超时信号（自动传入）
                async (
                    Booking input,
                    SchedulingService service,
                    HttpContext ctx,
                    CancellationToken ct
                ) =>
                {
                    var scope = ctx.User.PatientScope();
                    if (scope is not null && input.PatientId != scope)
                        throw new BusinessException(
                            "patient_forbidden",
                            "只能为自己的档案预约",
                            403
                        );
                    return await service.Create(
                        input,
                        // ctx.User ≈ SecurityContextHolder 里的 Authentication；
                        // FindFirstValue 是扩展方法，取指定 claim；! 空断言（已通过认证，必有值）。
                        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                        ctx.Request.Headers["Idempotency-Key"].ToString(),
                        ctx.TraceIdentifier,
                        ct,
                        selfService: scope is not null
                    );
                }
            )
            // 端点级授权策略：引用 Program.cs 里注册的命名策略（≈ 方法级 @PreAuthorize）。
            .RequireAuthorization("booking");
        // 用循环注册 5 个同构端点：C# lambda 闭包特性——
        foreach (
            var operation in new[]
            {
                "reschedule",
                "cancel",
                "confirm",
                "complete-task",
                "complete",
            }
        )
        {
            // foreach 迭代变量每轮是新实例，但显式拷贝一份更清晰（老式 for 循环闭包陷阱的防御写法）。
            var action = operation;
            group
                .MapPost(
                    "/appointments/{id}/" + action, // {id} 路由占位（≈ @PathVariable）
                    async (
                        string id,
                        Mutation input,
                        SchedulingService service,
                        HttpContext ctx,
                        CancellationToken ct
                    ) =>
                        await service.Mutate(
                            id,
                            action,
                            input,
                            ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                            ctx.Request.Headers["Idempotency-Key"].ToString(),
                            ctx.TraceIdentifier,
                            ct,
                            patientScope: ctx.User.PatientScope()
                        )
                )
                // 三元表达式 ?: 与 Java 相同；complete-task 走 tasks，创建/改期/取消允许 booking，确认/完成仅 schedule。
                .RequireAuthorization(
                    action == "complete-task" ? "tasks"
                    : action is "cancel" or "reschedule" ? "booking"
                    : "schedule"
                );
        }
        group.MapGet(
            "/appointments",
            // int? page / string? status：可空参数 ← query string（≈ @RequestParam(required = false)）
            async (
                ClinicDb db,
                int? page,
                int? size,
                string? status,
                int? resourceId,
                HttpContext ctx,
                CancellationToken ct
            ) =>
            {
                // ?? null 合并：query 未传时取默认值；Math.Clamp 截断到合法区间。
                var p = Math.Clamp(page ?? 1, 1, 100000);
                var s = Math.Clamp(size ?? 20, 1, 100);
                // AsQueryable()：拿到 IQueryable<T> 后可以分步拼接条件（延迟执行，Java Stream 做不到）。
                var q = db.Appointments.AsNoTracking().VisibleTo(ctx.User);
                if (status is not null) // is not null：模式匹配判空（≈ status != null，但可组合更复杂模式）
                    q = q.Where(x => x.Status == status); // Where ≈ stream.filter，但翻译成 SQL WHERE
                if (resourceId is not null)
                    q = q.Where(x => x.ResourceId == resourceId);
                return Results.Ok( // Results.Ok/NotFound ≈ ResponseEntity.ok()/notFound()
                    new
                    {
                        page = p,
                        size = s,
                        total = await q.CountAsync(ct), // CountAsync：翻译成 SELECT COUNT(*)
                        items = await q.OrderByDescending(x => x.StartUtc)
                            .ThenBy(x => x.Id)
                            .Skip((p - 1) * s) // Skip/Take ≈ SQL OFFSET/LIMIT
                            .Take(s)
                            .ToListAsync(ct),
                    }
                );
            }
        );
        group.MapGet(
            "/appointments/{id}",
            async (string id, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
            {
                // SingleOrDefaultAsync：0 或 1 条返回实体/null，多于 1 条抛异常（≈ JPA getSingleResult 的宽容版）。
                var a = await db
                    .Appointments.AsNoTracking()
                    .VisibleTo(ctx.User)
                    .SingleOrDefaultAsync(x => x.Id == id, ct);
                // 条件运算符 ? : 配合模式匹配：a is null → 404。
                return a is null
                    ? Results.NotFound()
                    : Results.Ok(
                        new
                        {
                            appointment = a,
                            tasks = await db
                                .Tasks.AsNoTracking()
                                .Where(x => x.AppointmentId == id)
                                .OrderBy(x => x.Id)
                                .ToListAsync(ct),
                            audit = await db
                                .Audits.AsNoTracking()
                                .Where(x => x.AppointmentId == id)
                                .OrderByDescending(x => x.Id)
                                .ToListAsync(ct),
                            sync = await db
                                .Outbox.AsNoTracking()
                                .Where(x => x.AppointmentId == id)
                                .OrderByDescending(x => x.Version)
                                .Select(x => new
                                {
                                    x.Id,
                                    x.Version,
                                    x.Status,
                                    x.Attempts,
                                    x.LastError,
                                })
                                .ToListAsync(ct),
                        }
                    );
            }
        );
        group.MapGet(
            // {id:int} 路由约束：非整数直接 404，不进委托（≈ Spring 的路径变量类型转换）。
            "/resources/{id:int}/slots",
            // DateTimeOffset start/end ← query string 自动绑定（ISO 8601 带偏移）。
            async (
                int id,
                DateTimeOffset start,
                DateTimeOffset end,
                ClinicDb db,
                CancellationToken ct
            ) =>
            {
                if (end <= start || end - start > TimeSpan.FromDays(7))
                    throw new BusinessException("invalid_range", "查询范围须在7天内", 400);
                // UtcDateTime：取 DateTimeOffset 的 UTC 部分（DateTime 类型），与库中存储一致。
                var from = start.UtcDateTime;
                var to = end.UtcDateTime;
                return await db
                    .SlotClaims.AsNoTracking()
                    // && 短路条件直接翻译成 SQL AND。
                    .Where(x => x.ResourceId == id && x.SlotStartUtc >= from && x.SlotStartUtc < to)
                    .OrderBy(x => x.SlotStartUtc)
                    .Select(x => new { x.ResourceId, x.SlotStartUtc })
                    .ToListAsync(ct);
            }
        );
    }
}

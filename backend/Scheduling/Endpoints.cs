using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace ClinicFlow.Scheduling;
public static class Endpoints
{
    public static void MapScheduling(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();
        group.MapPost("/appointments", async (Booking input, SchedulingService service, HttpContext ctx, CancellationToken ct) =>
            await service.Create(input, ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!, ctx.Request.Headers["Idempotency-Key"].ToString(), ctx.TraceIdentifier, ct)).RequireAuthorization("schedule");
        foreach (var operation in new[] { "reschedule", "cancel", "confirm", "complete-task" })
        {
            var action = operation;
            group.MapPost("/appointments/{id}/" + action, async (string id, Mutation input, SchedulingService service, HttpContext ctx, CancellationToken ct) =>
                await service.Mutate(id, action, input, ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!, ctx.Request.Headers["Idempotency-Key"].ToString(), ctx.TraceIdentifier, ct)).RequireAuthorization(action == "complete-task" ? "tasks" : "schedule");
        }
        group.MapGet("/appointments", async (ClinicDb db, int? page, int? size, string? status, int? resourceId, CancellationToken ct) =>
        {
            var p = Math.Max(1, page ?? 1); var s = Math.Clamp(size ?? 20, 1, 100);
            var q = db.Appointments.AsNoTracking().AsQueryable();
            if (status is not null) q = q.Where(x => x.Status == status);
            if (resourceId is not null) q = q.Where(x => x.ResourceId == resourceId);
            return Results.Ok(new { page = p, size = s, total = await q.CountAsync(ct), items = await q.OrderByDescending(x => x.StartUtc).ThenBy(x => x.Id).Skip((p-1)*s).Take(s).ToListAsync(ct) });
        });
        group.MapGet("/appointments/{id}", async (string id, ClinicDb db, CancellationToken ct) =>
        {
            var a = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return a is null ? Results.NotFound() : Results.Ok(new { appointment = a, tasks = await db.Tasks.AsNoTracking().Where(x=>x.AppointmentId == id).OrderBy(x=>x.Id).ToListAsync(ct), audit = await db.Audits.AsNoTracking().Where(x=>x.AppointmentId == id).OrderByDescending(x=>x.Id).ToListAsync(ct), sync = await db.Outbox.AsNoTracking().Where(x=>x.AppointmentId == id).OrderByDescending(x=>x.Version).Select(x=>new { x.Id, x.Version, x.Status, x.Attempts, x.LastError }).ToListAsync(ct) });
        });
        group.MapGet("/resources/{id:int}/slots", async (int id, DateTimeOffset start, DateTimeOffset end, ClinicDb db, CancellationToken ct) =>
        {
            if (end <= start || end-start > TimeSpan.FromDays(7)) throw new BusinessException("invalid_range", "查询范围须在7天内", 400);
            var from = start.UtcDateTime; var to = end.UtcDateTime;
            return await db.SlotClaims.AsNoTracking().Where(x => x.ResourceId == id && x.SlotStartUtc >= from && x.SlotStartUtc < to).OrderBy(x=>x.SlotStartUtc).ToListAsync(ct);
        });
    }
}

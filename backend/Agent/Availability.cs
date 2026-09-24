using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Agent;

public record SearchRequest(
    int? PatientId,
    string? FromDate,
    string? ToDate,
    int? DurationMinutes,
    int? ResourceId,
    int? EarliestMinute,
    int? LatestMinute
);

public record Candidate(
    string Id,
    Booking Booking,
    string PatientName,
    string ResourceName,
    string Reason
);

// All scheduling arithmetic stays outside the model. Dates/windows are clinic-local; bookings are UTC.
public class Availability(ClinicDb db, TimeProvider clock)
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public static (DateOnly From, DateOnly To, int Duration, int Earliest, int Latest) Validate(
        SearchRequest q
    )
    {
        if (
            q.PatientId is null
            || q.DurationMinutes is null
            || q.FromDate is null
            || q.ToDate is null
        )
            throw new BusinessException(
                "missing_constraints",
                "请明确患者、预约时长和日期范围",
                400
            );
        if (
            !DateOnly.TryParseExact(q.FromDate, "yyyy-MM-dd", out var from)
            || !DateOnly.TryParseExact(q.ToDate, "yyyy-MM-dd", out var to)
            || to < from
            || to.DayNumber - from.DayNumber > 30
        )
            throw new BusinessException(
                "invalid_range",
                "日期范围须为 yyyy-MM-dd，且最多31天",
                400
            );
        var duration = q.DurationMinutes.Value;
        var earliest = q.EarliestMinute ?? 540;
        var latest = q.LatestMinute ?? 1020;
        if (
            duration < 15
            || duration > 240
            || duration % 15 != 0
            || earliest < 540
            || latest > 1020
            || earliest >= latest
            || earliest % 15 != 0
            || latest % 15 != 0
        )
            throw new BusinessException(
                "invalid_window",
                "工作时间为09:00–17:00，时长15–240分钟，时间须对齐15分钟",
                400
            );
        return (from, to, duration, earliest, latest);
    }

    public async Task<List<Candidate>> Search(SearchRequest q, CancellationToken ct)
    {
        var (from, to, duration, earliest, latest) = Validate(q);
        // This feature intentionally only exposes the two seeded, fictional patients.
        var patient =
            await db
                .Patients.AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == q.PatientId
                        && (x.Identifier == "DEMO-001" || x.Identifier == "DEMO-002"),
                    ct
                )
            ?? throw new BusinessException("patient_missing", "仅支持演示患者", 404);
        var resources = await db
            .Resources.AsNoTracking()
            .Where(x => q.ResourceId == null || x.Id == q.ResourceId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        if (resources.Count == 0)
            throw new BusinessException("resource_missing", "资源不存在", 404);
        var start = Utc(from, 0);
        var end = Utc(to.AddDays(1), 0);
        var ids = resources.Select(x => x.Id).ToArray();
        var claims = await db
            .SlotClaims.AsNoTracking()
            .Where(x =>
                ids.Contains(x.ResourceId) && x.SlotStartUtc >= start && x.SlotStartUtc < end
            )
            .Select(x => new { x.ResourceId, x.SlotStartUtc })
            .ToListAsync(ct);
        var occupied = claims.Select(x => (x.ResourceId, x.SlotStartUtc)).ToHashSet();
        var candidates = new List<Candidate>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            for (var minute = earliest; minute + duration <= latest; minute += 15)
            {
                var slot = Utc(day, minute);
                if (slot <= clock.GetUtcNow().UtcDateTime)
                    continue;
                foreach (var resource in resources)
                {
                    if (
                        Rules
                            .Slots(slot, slot.AddMinutes(duration))
                            .Any(t => occupied.Contains((resource.Id, t)))
                    )
                        continue;
                    candidates.Add(
                        new(
                            Guid.NewGuid().ToString(),
                            new(
                                patient.Id,
                                resource.Id,
                                new DateTimeOffset(slot),
                                new DateTimeOffset(slot.AddMinutes(duration))
                            ),
                            patient.Name,
                            resource.Name,
                            $"符合日期与每日时间范围，连续{duration}分钟；按开始时间排序。"
                        )
                    );
                    if (candidates.Count == 3)
                        return candidates;
                }
            }
        }
        return candidates;
    }

    static DateTime Utc(DateOnly day, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(minute), Zone);
}

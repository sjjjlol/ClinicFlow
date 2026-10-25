using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois.Compatibility;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    internal static class CommandLocks
    {
        // Resolve with untracked reads, acquire in a total order, then resolve again before loading state.
        // A concurrently rescheduled target may have changed resource while we were waiting.
        internal static async Task Acquire(ClinicDb db, CommandEnvelope envelope, CancellationToken ct)
        {
            var command = JsonSerializer.Deserialize<LegacyCommandRecord>(envelope.CommandJson, CommandHistory.Json)!;
            var keys = await Resolve(db, command, ct);
            foreach (var key in keys)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO OisCommandLock (Id) VALUES ({key}) ON DUPLICATE KEY UPDATE Id = Id", ct);
            if (!keys.SequenceEqual(await Resolve(db, command, ct)))
                throw new BusinessException("concurrent_conflict", "预约关联已变化，请刷新并使用原请求标识重试");
        }

        private static async Task<string[]> Resolve(ClinicDb db, LegacyCommandRecord command, CancellationToken ct)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            void Entity(string? id) { if (!string.IsNullOrEmpty(id) && id.Length <= 36) pending.Enqueue(id); }
            void Resource(string id) { if (id != "" && id.Length <= 36) keys.Add("resource:" + id); }
            void Patient(int id) { if (id > 0) keys.Add("patient:" + id.ToString(CultureInfo.InvariantCulture)); }
            Entity(command.Id); Entity(command.CaseId); Entity(command.PlanId);
            Resource(command.ResourceId); Patient(command.PatientId);
            while (pending.TryDequeue(out var id))
            {
                if (!visited.Add(id)) continue;
                keys.Add("entity:" + id);
                var care = await db.Set<CareCase>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (care != null) Patient(care.PatientId);
                var booking = await db.Set<Reservation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (booking != null)
                {
                    Resource(booking.ResourceId);
                    if (booking.PatientId is int patient) Patient(patient);
                    Entity(booking.CaseId); Entity(booking.PlanId);
                }
                var plan = await db.Set<TreatmentPlan>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (plan != null) { Entity(plan.CaseId); Resource(plan.MachineId); }
                var delivery = await db.Set<TreatmentDelivery>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (delivery != null) { Entity(delivery.ReservationId); Entity(delivery.PlanId); Resource(delivery.MachineId); }
                var task = await db.Set<OisTask>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                if (task != null) Entity(task.TargetId);
                if (await db.Set<OisResource>().AsNoTracking().AnyAsync(x => x.Id == id, ct)) Resource(id);
            }
            return keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
    }
}

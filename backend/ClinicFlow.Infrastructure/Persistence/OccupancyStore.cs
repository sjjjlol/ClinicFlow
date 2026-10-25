using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    internal static class OccupancyStore
    {
        internal static IEnumerable<ReservationOccupancy> Slots(Reservation reservation)
        {
            // Preserve the existing rule: only cancellation releases the reserved interval.
            if (reservation.Status == "Cancelled") yield break;
            var step = TimeSpan.FromMinutes(15);
            if (reservation.StartUtc.Ticks % step.Ticks != 0 || reservation.EndUtc.Ticks % step.Ticks != 0
                || reservation.EndUtc <= reservation.StartUtc || reservation.EndUtc - reservation.StartUtc > TimeSpan.FromHours(4))
                throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", BusinessErrorKind.InvalidInput);
            for (var time = reservation.StartUtc; time < reservation.EndUtc; time += step)
            {
                yield return new ReservationOccupancy { SubjectType = "Resource", SubjectId = reservation.ResourceId, SlotStartUtc = time, ReservationId = reservation.Id };
                if (reservation.PatientId is int patient)
                    yield return new ReservationOccupancy { SubjectType = "Patient", SubjectId = patient.ToString(CultureInfo.InvariantCulture), SlotStartUtc = time, ReservationId = reservation.Id };
            }
        }

        internal static async Task Synchronize(ClinicDb db, OisState state, CancellationToken ct)
        {
            db.ChangeTracker.DetectChanges();
            var reservations = db.ChangeTracker.Entries<Reservation>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).Select(e => e.Entity).ToList();
            var ids = reservations.Select(r => r.Id).ToArray();
            var existing = await db.Set<ReservationOccupancy>().Where(x => ids.Contains(x.ReservationId)).ToListAsync(ct);
            var wanted = reservations.SelectMany(Slots).ToDictionary(x => (x.SubjectType, x.SubjectId, x.SlotStartUtc));
            var additions = new List<ReservationOccupancy>();
            foreach (var claim in existing)
            {
                var key = (claim.SubjectType, claim.SubjectId, claim.SlotStartUtc);
                if (wanted.TryGetValue(key, out var next) && next.ReservationId == claim.ReservationId)
                    wanted.Remove(key);
                else db.Remove(claim);
            }
            additions.AddRange(wanted.Values);

            var deliveries = db.ChangeTracker.Entries<TreatmentDelivery>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).Select(e => e.Entity).ToList();
            var deliveryIds = deliveries.Select(d => d.Id).ToArray();
            var active = await db.Set<ActiveExecution>().Where(x => deliveryIds.Contains(x.DeliveryId)).ToListAsync(ct);
            var executionClaims = new List<ActiveExecution>();
            foreach (var delivery in deliveries.Where(d => d.Outcome == "InProgress"))
            {
                executionClaims.Add(new ActiveExecution { SubjectType = "Resource", SubjectId = delivery.MachineId, DeliveryId = delivery.Id });
                var booking = state.Reservations.Single(r => r.Id == delivery.ReservationId);
                if (booking.PatientId is int patient)
                    executionClaims.Add(new ActiveExecution { SubjectType = "Patient", SubjectId = patient.ToString(CultureInfo.InvariantCulture), DeliveryId = delivery.Id });
            }
            foreach (var claim in active)
            {
                var match = executionClaims.Find(x => x.SubjectType == claim.SubjectType && x.SubjectId == claim.SubjectId && x.DeliveryId == claim.DeliveryId);
                if (match != null) executionClaims.Remove(match);
                else db.Remove(claim);
            }

            // Delete obsolete claims before inserting replacements; both saves share the command transaction.
            // A later uniqueness failure rolls back these deletions and every earlier business write.
            await db.SaveChangesAsync(ct);
            db.AddRange(additions.OrderBy(x => x.SubjectType, StringComparer.Ordinal).ThenBy(x => x.SubjectId, StringComparer.Ordinal).ThenBy(x => x.SlotStartUtc));
            db.AddRange(executionClaims.OrderBy(x => x.SubjectType, StringComparer.Ordinal).ThenBy(x => x.SubjectId, StringComparer.Ordinal));
        }
    }
}

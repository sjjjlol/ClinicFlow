using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    public sealed class WorkspaceQuery
    {
        private readonly ClinicDb db;
        public WorkspaceQuery(ClinicDb db) => this.db = db;
        public async Task<WorkspaceResponse> Snapshot(string actor, CancellationToken ct)
        {
            // Repeatable read keeps all tables and derived release status in one consistent view.
            await using var tx = await db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                ct
            );
            var w = await OisStateStore.Load(db, ct);
            var audits = await db.Set<OisAudit>()
                .Where(x => x.Action != "save-draft" || x.Actor == actor)
                .OrderBy(x => x.Id).ToListAsync(ct);
            var session = new OisSession(w);
            var validations = w.Validations.ToDictionary(x => x.ReservationId);
            var reservations = w
                .Reservations.Select(r => new WorkspaceReservation
                {
                    Reservation = new ReservationResponse(r, validations.GetValueOrDefault(r.Id)),
                    Blockers = r.Status == "Booked" ? session.Scheduling.Blockers(r.Id) : new List<string>(),
                })
                .ToList();
            await tx.CommitAsync(ct);
            return new WorkspaceResponse
            {
                Drafts = w.Drafts.Where(x => x.Actor == actor).ToList(),
                Cases = w.Cases, Prescriptions = w.Prescriptions, Plans = w.Plans,
                Resources = w.Resources, Reservations = reservations,
                Deliveries = w.Deliveries, Tasks = w.Tasks, Patients = w.Patients, Audits = audits,
            };
        }
    }
}

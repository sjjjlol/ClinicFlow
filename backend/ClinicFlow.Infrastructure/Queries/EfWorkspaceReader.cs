using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    public sealed class EfWorkspaceReader : IWorkspaceReader
    {
        private readonly ClinicDb db;
        public EfWorkspaceReader(ClinicDb db) => this.db = db;
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
                    Reservation = ReservationProjection.Project(r, validations.GetValueOrDefault(r.Id)),
                    Blockers = r.Status == "Booked" ? session.Scheduling.Blockers(r.Id) : new List<string>(),
                })
                .ToList();
            await tx.CommitAsync(ct);
            return new WorkspaceResponse
            {
                Drafts = w.Drafts.Where(x => x.Actor == actor).Select(WorkspaceProjection.Project).ToList(),
                Cases = w.Cases.Select(WorkspaceProjection.Project).ToList(), Prescriptions = w.Prescriptions.Select(WorkspaceProjection.Project).ToList(), Plans = w.Plans.Select(WorkspaceProjection.Project).ToList(),
                Resources = w.Resources.Select(WorkspaceProjection.Project).ToList(), Reservations = reservations,
                Deliveries = w.Deliveries.Select(WorkspaceProjection.Project).ToList(), Tasks = w.Tasks.Select(WorkspaceProjection.Project).ToList(), Patients = w.Patients.Select(WorkspaceProjection.Project).ToList(), Audits = audits.Select(WorkspaceProjection.Project).ToList(),
            };
        }
    }
}

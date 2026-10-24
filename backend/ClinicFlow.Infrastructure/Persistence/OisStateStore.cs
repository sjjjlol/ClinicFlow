using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    internal static class OisStateStore
    {
        internal static async Task<OisState> Load(ClinicDb db, CancellationToken ct) =>
            new OisState()
            {
                Drafts = await db.Set<OisDraft>().ToListAsync(ct),
                Cases = await db.Set<CareCase>().ToListAsync(ct),
                Prescriptions = await db.Set<Prescription>().ToListAsync(ct),
                Validations = await db.Set<PhysicalValidation>().ToListAsync(ct),
                Plans = await db.Set<TreatmentPlan>().ToListAsync(ct),
                Resources = await db.Set<OisResource>().ToListAsync(ct),
                Reservations = await db.Set<Reservation>().ToListAsync(ct),
                Deliveries = await db.Set<TreatmentDelivery>().ToListAsync(ct),
                Tasks = await db.Set<OisTask>().ToListAsync(ct),
                Patients = await db.Patients.AsNoTracking().ToListAsync(ct),
            };

    }
}

using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using ClinicFlow.Ois.Operations;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class PrescriptionsController : OisCommandController
    {
        public PrescriptionsController(CommandExecutor executor) : base(executor) { }

        [HttpPost("cases/{id}/prescriptions")]
        [Idempotent]
        public Task<IActionResult> SignPrescription(string id, [FromBody] SignPrescriptionRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new PrescriptionsOperations(w).SignPrescription(c, actor, role, now), ct);
        }

        [HttpPut("cases/{id}/prescription-draft")]
        [Idempotent]
        public Task<IActionResult> SavePrescriptionDraft(string id, [FromBody] SavePrescriptionDraftRequest input, CancellationToken ct)
        {
            input.CaseId = id;
            return Run(input, (w, c, actor, role, now) => new PrescriptionsOperations(w).SavePrescriptionDraft(c, actor, role, now), ct);
        }
    }
}

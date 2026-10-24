using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class TreatmentDeliveriesController : OisCommandController
    {
        public TreatmentDeliveriesController(CommandExecutor executor) : base(executor) { }

        [HttpPost("reservations/{id}/start-treatment")]
        [Idempotent]
        public Task<IActionResult> StartTreatment(string id, [FromBody] StartTreatmentRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Scheduling.StartTreatment(c, actor, role, now), ct);
        }

        [HttpPost("treatment-deliveries/{id}/finish")]
        [Idempotent]
        public Task<IActionResult> FinishTreatment(string id, [FromBody] FinishTreatmentRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Scheduling.FinishTreatment(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/not-executed")]
        [Idempotent]
        public Task<IActionResult> RecordNotExecuted(string id, [FromBody] RecordNotExecutedRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Scheduling.RecordNotExecuted(c, actor, role, now), ct);
        }

        [HttpPost("treatment-deliveries/{id}/corrections")]
        [Idempotent]
        public Task<IActionResult> CorrectTreatmentDelivery(string id, [FromBody] CorrectTreatmentDeliveryRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Scheduling.CorrectTreatmentDelivery(c, actor, role, now), ct);
        }
    }
}

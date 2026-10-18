using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using ClinicFlow.Ois.Operations;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class ReservationsController : OisCommandController
    {
        public ReservationsController(CommandExecutor executor) : base(executor) { }

        [HttpPost("reservations/{id}/reschedule")]
        [Idempotent]
        public Task<IActionResult> RescheduleReservation(string id, [FromBody] RescheduleReservationRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).RescheduleReservation(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/restore")]
        [Idempotent]
        public Task<IActionResult> RestoreReservation(string id, [FromBody] RestoreReservationRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).RestoreReservation(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/cancel")]
        [Idempotent]
        public Task<IActionResult> CancelReservation(string id, [FromBody] CancelReservationRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).CancelReservation(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/no-show")]
        [Idempotent]
        public Task<IActionResult> RecordNoShow(string id, [FromBody] RecordNoShowRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).RecordNoShow(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/experiment-results")]
        [Idempotent]
        public Task<IActionResult> SubmitExperimentResult(string id, [FromBody] SubmitExperimentResultRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ExperimentsOperations(w).SubmitExperimentResult(c, actor, role, now), ct);
        }

        [HttpPost("reservations/{id}/complete-consultation")]
        [Idempotent]
        public Task<IActionResult> CompleteConsultation(string id, [FromBody] CompleteConsultationRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input, (w, c, actor, role, now) => new ConsultationsOperations(w).CompleteConsultation(c, actor, role, now), ct);
        }

        [HttpPost("reservations/consultation")]
        [Idempotent]
        public Task<IActionResult> BookConsultation([FromBody] BookConsultationRequest input, CancellationToken ct)
        {
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).BookConsultation(c, actor, role, now), ct);
        }

        [HttpPost("reservations/treatment")]
        [Idempotent]
        public Task<IActionResult> BookTreatment([FromBody] BookTreatmentRequest input, CancellationToken ct)
        {
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).BookTreatment(c, actor, role, now), ct);
        }

        [HttpPost("reservations/experiment")]
        [Idempotent]
        public Task<IActionResult> BookExperiment([FromBody] BookExperimentRequest input, CancellationToken ct)
        {
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).BookExperiment(c, actor, role, now), ct);
        }

        [HttpPost("reservations/maintenance")]
        [Idempotent]
        public Task<IActionResult> BookMaintenance([FromBody] BookMaintenanceRequest input, CancellationToken ct)
        {
            return Run(input, (w, c, actor, role, now) => new ReservationsOperations(w).BookMaintenance(c, actor, role, now), ct);
        }
    }
}

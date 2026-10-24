using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClinicFlow.Controllers
{
    public sealed class MachinesController : OisCommandController
    {
        public MachinesController(CommandExecutor executor) : base(executor) { }

        [HttpPut("machines/{id}/availability")]
        [Idempotent]
        public Task<IActionResult> SetMachineAvailability(string id, [FromBody] SetMachineAvailabilityRequest input, CancellationToken ct)
        {
            input.Id = id;
            return Run(input.ToCommand(), (w, c, actor, role, now) => w.Scheduling.SetMachineAvailability(c, actor, role, now), ct);
        }
    }
}

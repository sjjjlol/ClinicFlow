using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class MachinesOperations
    {
        private readonly OisState state;
        public MachinesOperations(OisState state) => this.state = state;

        public string SetMachineAvailability(SetMachineAvailabilityRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            var machine = state.Current(state.Resources, request.Id, request.Version);
            state.Need(machine.Kind == "Machine", "invalid_resource", "仅可设置机器可用性");
            state.Text(request.Reason);
            if (request.Available)
            {
                machine.Available = true;
                machine.Reason = request.Reason;
                machine.Version++;
            }
            else
                state.StopMachine(machine.Id, request.Reason, now);
            return machine.Id;
        }

    }
}

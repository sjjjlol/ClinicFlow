using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule
    {

        public string SetMachineAvailability(SetMachineAvailabilityRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", 403);
            var machine = Current(resources, request.Id, request.Version);
            Need(machine.Kind == "Machine", "invalid_resource", "仅可设置机器可用性");
            Text(request.Reason);
            if (request.Available)
            {
                machine.Available = true;
                machine.Reason = request.Reason;
                machine.Version++;
            }
            else
                StopMachine(machine.Id, request.Reason, now);
            return machine.Id;
        }

    }
}

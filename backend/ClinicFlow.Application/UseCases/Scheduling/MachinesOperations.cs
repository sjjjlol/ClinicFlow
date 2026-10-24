using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Scheduling
{
    public sealed partial class SchedulingModule
    {

        public string SetMachineAvailability(SetMachineAvailabilityCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Physicist", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var machine = Current(resources, request.Id, request.Version);
            Need(machine.Kind == "Machine", "invalid_resource", "仅可设置机器可用性");
            Text(request.Reason);
            if (request.Available)
            {
                machine.SetAvailability(true, request.Reason);
            }
            else
                StopMachine(machine.Id, request.Reason, now);
            return machine.Id;
        }

    }
}

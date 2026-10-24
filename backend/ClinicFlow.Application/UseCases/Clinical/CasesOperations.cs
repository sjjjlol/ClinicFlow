using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Clinical
{
    public sealed partial class ClinicalModule
    {

        public string CreateCase(CreateCaseCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            Need(patients.Any(x => x.Id == request.PatientId), "patient_missing", "患者不存在", BusinessErrorKind.NotFound);
            var care = CareCase.Open(request.PatientId, request.Text);
            cases.Add(care);
            return care.Id;
        }

        public string TerminateCase(TerminateCaseCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var ended = Current(cases, request.Id, request.Version);
            scheduling().NoActiveTreatment(ended.Id);
            Need(!ended.Terminated, "terminated", "诊疗档案已终止");
            ended.Terminate(request.Reason, actor, now);
            return ended.Id;
        }

        public string AddCaseNote(AddCaseNoteCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            Current(cases, request.Id, request.Version);
            Text(request.Text);
            return request.Id;
        }

    }
}

using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Clinical
{
    public sealed partial class ClinicalModule
    {

        public string CreateCase(CreateCaseRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            Need(patients.Any(x => x.Id == request.PatientId), "patient_missing", "患者不存在", 404);
            var care = new CareCase { PatientId = request.PatientId, Title = Text(request.Text) };
            cases.Add(care);
            return care.Id;
        }

        public string TerminateCase(TerminateCaseRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var ended = Current(cases, request.Id, request.Version);
            scheduling().NoActiveTreatment(ended.Id);
            Need(!ended.Terminated, "terminated", "诊疗档案已终止");
            ended.Terminate(request.Reason);
            scheduling().BlockedTasks(ended.Id, "诊疗方案已终止", now);
            support().AddTask(ended.Id, "Physicist", "诊疗方案已终止：" + request.Reason, now);
            return ended.Id;
        }

        public string AddCaseNote(AddCaseNoteRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            Current(cases, request.Id, request.Version);
            Text(request.Text);
            return request.Id;
        }

    }
}

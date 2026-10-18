using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class CasesOperations
    {
        private readonly OisState state;
        public CasesOperations(OisState state) => this.state = state;

        public string CreateCase(CreateCaseRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            state.Need(state.Patients.Any(x => x.Id == request.PatientId), "patient_missing", "患者不存在", 404);
            var care = new CareCase { PatientId = request.PatientId, Title = state.Text(request.Text) };
            state.Cases.Add(care);
            return care.Id;
        }

        public string TerminateCase(TerminateCaseRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var ended = state.Current(state.Cases, request.Id, request.Version);
            state.NoActiveTreatment(ended.Id);
            state.Need(!ended.Terminated, "terminated", "诊疗档案已终止");
            ended.TerminationReason = state.Text(request.Reason);
            ended.Terminated = true;
            ended.Version++;
            state.BlockedTasks(ended.Id, "诊疗方案已终止", now);
            state.AddTask(ended.Id, "Physicist", "诊疗方案已终止：" + request.Reason, now);
            return ended.Id;
        }

        public string AddCaseNote(AddCaseNoteRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            state.Current(state.Cases, request.Id, request.Version);
            state.Text(request.Text);
            return request.Id;
        }

    }
}

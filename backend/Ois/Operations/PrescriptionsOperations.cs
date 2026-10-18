using System;
using System.Linq;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Operations
{
    public sealed class PrescriptionsOperations
    {
        private readonly OisState state;
        public PrescriptionsOperations(OisState state) => this.state = state;

        public string SignPrescription(SignPrescriptionRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var care = state.Current(state.Cases, request.Id, request.Version);
            state.NoActiveTreatment(care.Id);
            state.Need(!care.Terminated, "terminated", "诊疗档案已终止");
            ValidateClinical(request);
            var rx = new Prescription
            {
                CaseId = care.Id,
                TreatmentSite = request.TreatmentSite,
                TotalDoseGy = request.TotalDoseGy,
                FractionDoseGy = request.FractionDoseGy,
                Fractions = request.Fractions,
                TreatmentMaterials = request.TreatmentMaterials,
                Diagnosis = state.Text(request.Diagnosis),
                Content = state.Text(request.Text),
                Reason = care.CurrentPrescriptionId == null ? request.Reason : state.Text(request.Reason),
                SupersedesId = care.CurrentPrescriptionId,
                SignedBy = actor,
                SignedUtc = now,
            };
            state.Prescriptions.Add(rx);
            care.CurrentPrescriptionId = rx.Id;
            care.CurrentPlanId = null;
            care.Version++;
            state.AddTask(care.Id, "Physicist", "处方已签署，请登记匹配的治疗计划", now);
            state.BlockedTasks(care.Id, "处方更新，待计划审核与验证", now);
            state.Drafts.RemoveAll(x => x.CaseId == request.Id && x.Actor == actor && x.Operation == "prescribe");
            return rx.Id;
        }

        public string SavePrescriptionDraft(SavePrescriptionDraftRequest request, string actor, string role, DateTime now)
        {
            state.Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            return new DraftOperations(state).Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

        private void ValidateClinical(SignPrescriptionRequest request)
        {
            state.Text(request.TreatmentSite);
            state.Text(request.TreatmentMaterials);
            state.Need(request.TotalDoseGy > 0 && request.FractionDoseGy > 0 && request.Fractions > 0, "invalid_dose", "剂量和分次数必须大于零", 400);
        }
    }
}

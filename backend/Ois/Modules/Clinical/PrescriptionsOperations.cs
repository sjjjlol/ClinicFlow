using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Ois.Requests;

namespace ClinicFlow.Ois.Modules.Clinical
{
    public sealed partial class ClinicalModule
    {

        public string SignPrescription(SignPrescriptionRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            var care = Current(cases, request.Id, request.Version);
            scheduling().NoActiveTreatment(care.Id);
            Need(!care.Terminated, "terminated", "诊疗档案已终止");
            ValidateClinical(request);
            var rx = new Prescription
            {
                CaseId = care.Id,
                TreatmentSite = request.TreatmentSite,
                TotalDoseGy = request.TotalDoseGy,
                FractionDoseGy = request.FractionDoseGy,
                Fractions = request.Fractions,
                TreatmentMaterials = request.TreatmentMaterials,
                Diagnosis = Text(request.Diagnosis),
                Content = Text(request.Text),
                Reason = care.CurrentPrescriptionId == null ? request.Reason : Text(request.Reason),
                SupersedesId = care.CurrentPrescriptionId,
                SignedBy = actor,
                SignedUtc = now,
            };
            prescriptions.Add(rx);
            care.SignPrescription(rx.Id);
            support().AddTask(care.Id, "Physicist", "处方已签署，请登记匹配的治疗计划", now);
            scheduling().BlockedTasks(care.Id, "处方更新，待计划审核与验证", now);
            support().RemoveDraft(request.Id, actor, "prescribe");
            return rx.Id;
        }

        public string SavePrescriptionDraft(SavePrescriptionDraftRequest request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", 403);
            return support().Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

        private void ValidateClinical(SignPrescriptionRequest request)
        {
            Text(request.TreatmentSite);
            Text(request.TreatmentMaterials);
            Need(request.TotalDoseGy > 0 && request.FractionDoseGy > 0 && request.Fractions > 0, "invalid_dose", "剂量和分次数必须大于零", 400);
        }
    }
}

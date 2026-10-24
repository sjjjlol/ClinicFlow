using System;
using System.Linq;
using static ClinicFlow.Ois.DomainRules;
using ClinicFlow.Application.Commands;

namespace ClinicFlow.Ois.Modules.Clinical
{
    public sealed partial class ClinicalModule
    {

        public string SignPrescription(SignPrescriptionCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            var care = Current(cases, request.Id, request.Version);
            scheduling().NoActiveTreatment(care.Id);
            Need(!care.Terminated, "terminated", "诊疗档案已终止");
            var rx = Prescription.Sign(care.Id, care.CurrentPrescriptionId, request.TreatmentSite,
                request.TotalDoseGy, request.FractionDoseGy, request.Fractions, request.TreatmentMaterials,
                request.Diagnosis, request.Text, request.Reason, actor, now);
            prescriptions.Add(rx);
            care.SignPrescription(rx.Id, actor, now);
            support().RemoveDraft(request.Id, actor, "prescribe");
            return rx.Id;
        }

        public string SavePrescriptionDraft(SavePrescriptionDraftCommand request, string actor, string role, DateTime now)
        {
            Need(role == "Clinician", "forbidden", "当前角色没有此操作权限", BusinessErrorKind.Forbidden);
            return support().Save(request.CaseId, request.Version, request.DraftVersion, request.DraftJson, request.Kind, actor);
        }

    }
}

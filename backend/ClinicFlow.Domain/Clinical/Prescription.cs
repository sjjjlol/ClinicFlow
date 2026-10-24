using System;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois
{
    public class Prescription : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string CaseId { get; internal set; } = "";
        public string? SupersedesId { get; internal set; }
        public string TreatmentSite { get; internal set; } = "";
        public decimal TotalDoseGy { get; internal set; }
        public decimal FractionDoseGy { get; internal set; }
        public int Fractions { get; internal set; }
        public string TreatmentMaterials { get; internal set; } = "";
        public string Diagnosis { get; internal set; } = "";
        public string Content { get; internal set; } = "";
        public string Reason { get; internal set; } = "";
        public string SignedBy { get; internal set; } = "";
        public DateTime SignedUtc { get; internal set; }
        public bool HasClinicalDetails() =>
            !string.IsNullOrWhiteSpace(TreatmentSite) && !string.IsNullOrWhiteSpace(TreatmentMaterials)
            && TotalDoseGy > 0 && FractionDoseGy > 0 && Fractions > 0;
        public static Prescription Sign(string caseId, string? supersedesId, string treatmentSite,
            decimal totalDoseGy, decimal fractionDoseGy, int fractions, string treatmentMaterials,
            string diagnosis, string content, string reason, string actor, DateTime now)
        {
            Text(treatmentSite); Text(treatmentMaterials);
            Need(totalDoseGy > 0 && fractionDoseGy > 0 && fractions > 0, "invalid_dose", "剂量和分次数必须大于零", BusinessErrorKind.InvalidInput);
            return new Prescription
            {
                CaseId = caseId, SupersedesId = supersedesId, TreatmentSite = treatmentSite,
                TotalDoseGy = totalDoseGy, FractionDoseGy = fractionDoseGy, Fractions = fractions,
                TreatmentMaterials = treatmentMaterials, Diagnosis = Text(diagnosis), Content = Text(content),
                Reason = supersedesId == null ? reason : Text(reason), SignedBy = actor, SignedUtc = now,
            };
        }
    }
}

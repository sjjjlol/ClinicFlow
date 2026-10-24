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
        internal bool HasClinicalDetails() =>
            !string.IsNullOrWhiteSpace(TreatmentSite) && !string.IsNullOrWhiteSpace(TreatmentMaterials)
            && TotalDoseGy > 0 && FractionDoseGy > 0 && Fractions > 0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class PrescriptionResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
        public string? SupersedesId { get; set; }
        public string TreatmentSite { get; set; } = "";
        public decimal TotalDoseGy { get; set; }
        public decimal FractionDoseGy { get; set; }
        public int Fractions { get; set; }
        public string TreatmentMaterials { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Content { get; set; } = "";
        public string Reason { get; set; } = "";
        public string SignedBy { get; set; } = "";
        public DateTime SignedUtc { get; set; }
    }
}

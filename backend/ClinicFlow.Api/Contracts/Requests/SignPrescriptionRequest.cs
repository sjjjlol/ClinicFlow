using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    public sealed class SignPrescriptionRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Diagnosis { get; set; } = "";
        public string Text { get; set; } = "";
        public string Reason { get; set; } = "";
        public string TreatmentSite { get; set; } = "";
        public decimal TotalDoseGy { get; set; }
        public decimal FractionDoseGy { get; set; }
        public int Fractions { get; set; }
        public string TreatmentMaterials { get; set; } = "";
        public ClinicFlow.Application.Commands.SignPrescriptionCommand ToCommand() =>
            new ClinicFlow.Application.Commands.SignPrescriptionCommand
            {
                Id = Id,
                Version = Version,
                Diagnosis = Diagnosis,
                Text = Text,
                Reason = Reason,
                TreatmentSite = TreatmentSite,
                TotalDoseGy = TotalDoseGy,
                FractionDoseGy = FractionDoseGy,
                Fractions = Fractions,
                TreatmentMaterials = TreatmentMaterials,
            };
    }
}

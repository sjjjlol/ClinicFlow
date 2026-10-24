
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("prescribe")]
    public sealed class SignPrescriptionCommand
    {
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
    }
}

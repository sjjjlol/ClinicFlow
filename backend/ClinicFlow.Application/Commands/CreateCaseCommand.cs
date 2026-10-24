namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("case")]
    public sealed class CreateCaseCommand
    {
        public int PatientId { get; set; }
        public string Text { get; set; } = "";
    }
}

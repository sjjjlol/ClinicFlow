namespace ClinicFlow.Ois.Requests
{
    public sealed class CreateCaseRequest
    {
        public int PatientId { get; set; }
        public string Text { get; set; } = "";
        public ClinicFlow.Application.Commands.CreateCaseCommand ToCommand() =>
            new ClinicFlow.Application.Commands.CreateCaseCommand
            {
                PatientId = PatientId,
                Text = Text,
            };
    }
}

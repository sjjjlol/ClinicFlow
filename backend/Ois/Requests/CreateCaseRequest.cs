namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("case")]
    public sealed class CreateCaseRequest
    {
        public int PatientId { get; set; }
        public string Text { get; set; } = "";
    }
}

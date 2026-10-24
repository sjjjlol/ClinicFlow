
namespace ClinicFlow.Application.Commands
{
    [ClinicFlow.Ois.BusinessOperation("validate")]
    public sealed class SubmitExperimentResultCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Outcome { get; set; } = "";
        public string Text { get; set; } = "";
        public string FailureType { get; set; } = "";
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
    }
}

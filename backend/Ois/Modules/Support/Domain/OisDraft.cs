using System;

namespace ClinicFlow.Ois
{
    public class OisDraft : IOisEntity
    {
        public string Id { get; internal set; } = Guid.NewGuid().ToString();
        public int Version { get; internal set; } = 1;
        public string CaseId { get; internal set; } = "";
        public string Actor { get; internal set; } = "";
        public string Operation { get; internal set; } = "";
        public string Payload { get; internal set; } = "";
        public int CaseVersion { get; internal set; }
    }
}

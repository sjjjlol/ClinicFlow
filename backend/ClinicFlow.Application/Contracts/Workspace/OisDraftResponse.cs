using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class OisDraftResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string CaseId { get; set; } = "";
        public string Actor { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Payload { get; set; } = "";
        public int CaseVersion { get; set; }
    }
}

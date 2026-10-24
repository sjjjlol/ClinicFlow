using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public sealed class OisResourceResponse
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool Available { get; set; }
        public string Reason { get; set; } = "";
    }
}

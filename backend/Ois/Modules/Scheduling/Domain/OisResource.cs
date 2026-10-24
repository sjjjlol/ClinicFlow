using System;

namespace ClinicFlow.Ois
{
    public class OisResource : IOisEntity
    {
        public string Id { get; internal set; } = "";
        public int Version { get; internal set; } = 1;
        public string Name { get; internal set; } = "";
        public string Kind { get; internal set; } = "Machine";
        public bool Available { get; internal set; } = true;
        public string Reason { get; internal set; } = "";
    }
}

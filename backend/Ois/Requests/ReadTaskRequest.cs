using System.Text.Json.Serialization;

namespace ClinicFlow.Ois.Requests
{
    [BusinessOperation("read-task")]
    public sealed class ReadTaskRequest
    {
        [JsonIgnore]
        public string Id { get; set; } = "";
        public int Version { get; set; }
    }
}

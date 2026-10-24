using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClinicFlow.Ois
{
    public interface ICommandTransaction
    {
        Task<object> Run(CommandEnvelope command, Func<OisSession, string> apply, CancellationToken ct);
    }
    public sealed class CommandEnvelope
    {
        public DateTime OccurredUtc { get; set; }
        public string Id { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Actor { get; set; } = "";
        public string Role { get; set; } = "";
        public string Correlation { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string CaseId { get; set; } = "";
        public string CommandJson { get; set; } = "";
    }

}

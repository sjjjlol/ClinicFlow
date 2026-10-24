using System;
using static ClinicFlow.Ois.DomainRules;
namespace ClinicFlow.Ois
{
    public sealed class VerificationProtocol
    {
        public string Method { get; }
        public string Equipment { get; }
        public string Conditions { get; }
        public string Expected { get; }
        public string Standard { get; }
        public string StandardVersion { get; }
        public string Criteria { get; }
        public string Deviation { get; }
        public VerificationProtocol(string method, string equipment, string conditions, string expected,
            string standard, string standardVersion, string criteria, string deviation)
        {
            Text(method); Text(equipment); Text(conditions); Text(expected);
            Text(standard); Text(standardVersion); Text(criteria); Text(deviation);
            Method = method; Equipment = equipment; Conditions = conditions; Expected = expected;
            Standard = standard; StandardVersion = standardVersion; Criteria = criteria; Deviation = deviation;
        }
    }
}

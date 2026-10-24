using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClinicFlow.Ois.Compatibility
{
    // Frozen storage adapter: preserves historical audit JSON and idempotency fingerprints.
    // Domain handlers and HTTP actions never receive LegacyCommandRecord.
    internal static class CommandHistory
    {
        internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { Converters = { new UtcDateTimeConverter() } };

        internal static LegacyCommandRecord Normalize<T>(T request) where T : class
        {
            var record = new LegacyCommandRecord();
            foreach (var property in typeof(T).GetProperties())
            {
                var target = typeof(LegacyCommandRecord).GetProperty(property.Name)
                    ?? throw new InvalidOperationException($"Request field {property.Name} needs an explicit history mapping");
                target.SetValue(record, property.GetValue(request));
            }
            return record;
        }

        internal static string Operation<T>() => typeof(T).GetCustomAttribute<BusinessOperationAttribute>()?.Name
            ?? throw new InvalidOperationException("Request has no business operation metadata");

        internal static string Fingerprint(string op, LegacyCommandRecord command)
        {
            var legacyNames = new HashSet<string> { "id", "version", "patientId", "caseId", "planId", "resourceId", "kind", "startUtc", "endUtc", "text", "diagnosis", "reason", "attachment", "outcome", "failureType", "identityChecked", "available" };
            var fingerprintCommand = JsonSerializer.SerializeToElement(command, Json).EnumerateObject()
                .Where(p => legacyNames.Contains(p.Name) || !(p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "" || p.Value.ValueKind == JsonValueKind.Number && p.Value.GetDecimal() == 0))
                .ToDictionary(p => p.Name, p => p.Value);
            return Hash(JsonSerializer.Serialize(new { op, command = fingerprintCommand }, Json));
        }

        internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

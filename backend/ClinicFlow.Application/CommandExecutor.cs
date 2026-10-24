using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois.Compatibility;

namespace ClinicFlow.Ois
{
    public sealed class CommandExecutor
    {
        private readonly ICommandTransaction transaction;
        private readonly TimeProvider clock;
        public CommandExecutor(ICommandTransaction transaction, TimeProvider clock)
        { this.transaction = transaction; this.clock = clock; }
        private static readonly JsonSerializerOptions Json = CommandHistory.Json;
        public Task<object> Execute<T>(T request, string actor, string role, string key, string correlation,
            CancellationToken ct, Func<OisSession, T, string, string, DateTime, string> apply) where T : class
        {
            var op = CommandHistory.Operation<T>();
            var command = CommandHistory.Normalize(request);
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
                throw new BusinessException("invalid_key", "请提供1至128字符的请求标识", BusinessErrorKind.InvalidInput);
            foreach (var property in typeof(T).GetProperties())
            {
                if (property.PropertyType != typeof(string))
                    continue;

                string? value = property.GetValue(request) as string;
                int maxLength = property.Name == "DraftJson" ? 64000 : 4000;
                if (value == null || value.Length > maxLength)
                    throw new BusinessException("invalid_input", "文本不能为空值或超过4000字符", BusinessErrorKind.InvalidInput);
            }
            var id = CommandHistory.Hash(JsonSerializer.Serialize(new[] { "ois", actor, role, key }));

            var envelope = new CommandEnvelope
            {
                Id = id, Fingerprint = CommandHistory.Fingerprint(op, command), Operation = op,
                Actor = actor, Role = role, Correlation = correlation, TargetId = command.Id,
                CaseId = command.CaseId, CommandJson = JsonSerializer.Serialize(command, Json),
            };
            return transaction.Run(envelope, session =>
            {
                envelope.OccurredUtc = clock.GetUtcNow().UtcDateTime;
                var target = apply(session, request, actor, role, envelope.OccurredUtc);
                session.HandleDomainEvents();
                return target;
            }, ct);
        }
    }
}

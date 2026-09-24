using System.Text.Json.Nodes;
using ClinicFlow.Scheduling;

namespace ClinicFlow.Agent;

public record ToolTrace(string Tool, string Arguments, string Outcome, long DurationMs);

public record AgentReply(
    string SessionId,
    string Message,
    List<Candidate> Candidates,
    List<ToolTrace> Trace,
    string Status,
    Appointment? Appointment = null
);

public record AgentMessage(string? SessionId, string Message, int? SelectedPatientId);

public record ConfirmCandidate(string CandidateId);

public class AgentSession(string owner, DateTimeOffset expires)
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Owner { get; } = owner;
    public DateTimeOffset Expires { get; } = expires;
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public JsonArray History { get; set; } = [];
    public List<Candidate> Candidates { get; set; } = [];
    public SearchRequest? Constraints { get; set; }
    public int? RequiredResourceId { get; set; }
    public int Turns { get; set; }
    public string? ConfirmingId { get; set; }
    public Appointment? Appointment { get; set; }
    public Dictionary<string, AgentReply> ConfirmReplies { get; } = [];
}

// Deliberately process-local for the single-instance demo. Expired/restarted sessions fail closed.
public class AgentSessions(TimeProvider clock)
{
    readonly Dictionary<string, AgentSession> sessions = [];
    readonly object sync = new();

    public AgentSession Get(string? id, string owner)
    {
        lock (sync)
        {
            foreach (
                var key in sessions
                    .Where(x => x.Value.Expires <= clock.GetUtcNow())
                    .Select(x => x.Key)
                    .ToArray()
            )
                sessions.Remove(key);
            if (id is not null)
            {
                if (!sessions.TryGetValue(id, out var existing) || existing.Owner != owner)
                    throw new BusinessException(
                        "session_expired",
                        "会话已失效，请先查看预约列表确认上次结果，再开启新会话",
                        404
                    );
                return existing;
            }
            if (sessions.Count >= 128)
                throw new BusinessException("agent_busy", "助手会话已满，请稍后重试", 429);
            var session = new AgentSession(owner, clock.GetUtcNow().AddMinutes(30));
            sessions.Add(session.Id, session);
            return session;
        }
    }
}

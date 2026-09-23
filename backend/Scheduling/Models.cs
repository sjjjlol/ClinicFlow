namespace ClinicFlow.Scheduling;
public class Appointment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int PatientId { get; set; }
    public int ResourceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public int Version { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
public class SlotClaim
{
    public int ResourceId { get; set; }
    public DateTime SlotStartUtc { get; set; }
    public string AppointmentId { get; set; } = "";
}
public class PrerequisiteTask
{
    public int Id { get; set; }
    public string AppointmentId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Completed { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
public class IdempotencyRecord
{
    public string Id { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string? Response { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public class AuditEntry
{
    public long Id { get; set; }
    public string AppointmentId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Actor { get; set; } = "";
    public int Version { get; set; }
    public string Summary { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
public class OutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AppointmentId { get; set; } = "";
    public int Version { get; set; }
    public string Payload { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public string? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = "";
}
public record Booking(int PatientId, int ResourceId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public record Mutation(int Version, int? ResourceId = null, DateTimeOffset? StartUtc = null, DateTimeOffset? EndUtc = null, int? TaskId = null);
public class BusinessException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public static class Rules
{
    public static DateTime[] Slots(DateTime start, DateTime end)
    {
        if (start.Ticks % TimeSpan.FromMinutes(15).Ticks != 0 || end.Ticks % TimeSpan.FromMinutes(15).Ticks != 0 || end <= start || end - start > TimeSpan.FromHours(4))
            throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", 400);
        return Enumerable.Range(0, (int)(end - start).TotalMinutes / 15).Select(i => start.AddMinutes(i * 15)).ToArray();
    }
}

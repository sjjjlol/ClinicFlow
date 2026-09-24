using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicFlow;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions options
    ) => reader.GetDateTime().ToUniversalTime();

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

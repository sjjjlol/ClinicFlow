using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicFlow;

// ===== 自定义 JSON 转换器（≈ Jackson 的 JsonSerializer/JsonDeserializer 自定义 Module）=====
// JsonConverter<DateTime>：泛型基类，只接管 DateTime 类型的读写。
// 目的：读入一律转 UTC；写出一律带 "Z"（库中 DateTime 的 Kind 未指定，序列化时补上）。
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    // ref Utf8JsonReader reader：ref 参数 ≈ 传引用（值类型的读取器要原地推进，性能考虑）。
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

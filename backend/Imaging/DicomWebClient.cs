using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClinicFlow.Scheduling;

namespace ClinicFlow.Imaging;

// Only this adapter knows DICOM tag numbers and the external HTTP representation.
public class DicomWebClient(HttpClient http, ILogger<DicomWebClient> logger)
{
    public const string Source = "orthanc-demo";

    public static void ValidateUid(string uid)
    {
        if (
            string.IsNullOrEmpty(uid)
            || uid.Length > 64
            || !Regex.IsMatch(uid, @"^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*))+\z")
        )
            throw new BusinessException("invalid_uid", "影像 UID 格式不正确", 400);
    }

    public static string Value(JsonElement item, string tag)
    {
        if (
            !item.TryGetProperty(tag, out var attribute)
            || !attribute.TryGetProperty("Value", out var values)
            || values.ValueKind != JsonValueKind.Array
            || values.GetArrayLength() == 0
        )
            return "";
        return string.Join("\\", values.EnumerateArray().Select(v => v.ToString()));
    }

    public static bool Matches(JsonElement item, ImagingIdentity identity) =>
        Value(item, "00100020") == identity.ExternalPatientId
        && Value(item, "00100021") == identity.Issuer;

    public async Task<JsonElement[]> Json(string path, CancellationToken ct)
    {
        var (bytes, _) = await Get(path, "application/dicom+json", ct);
        try
        {
            var items =
                System.Text.Json.JsonSerializer.Deserialize<JsonElement[]>(bytes)
                ?? throw new JsonException();
            if (items.Any(x => x.ValueKind != JsonValueKind.Object))
                throw new JsonException();
            return items;
        }
        catch (JsonException)
        {
            throw new BusinessException(
                "imaging_invalid_response",
                "影像服务返回了无效元数据",
                502
            );
        }
    }

    public async Task<(StudySummary[] Items, bool Truncated)> Search(
        ImagingIdentity identity,
        CancellationToken ct
    )
    {
        var items = await Json(
            "dicom-web/studies?PatientID="
                + Uri.EscapeDataString(identity.ExternalPatientId)
                + "&includefield=00100021&includefield=00081030&limit=101",
            ct
        );
        // QIDO is a search, not an authorization decision: verify returned identifiers too.
        var matched = items
            .Take(100)
            .Where(x => Matches(x, identity))
            .Select(x => new StudySummary(
                Value(x, "0020000D"),
                Value(x, "00081030"),
                Value(x, "00080020"),
                Value(x, "00080061")
            ))
            .ToArray();
        return (matched, items.Length > 100);
    }

    public async Task<JsonElement[]> Verify(
        string uid,
        ImagingIdentity identity,
        CancellationToken ct
    )
    {
        ValidateUid(uid);
        var items = await Json($"dicom-web/studies/{uid}/metadata", ct);
        if (items.Length == 0)
            throw new BusinessException("imaging_not_found", "影像检查不存在", 404);
        // Check every instance, not merely the representative QIDO study header.
        if (items.Any(x => !Matches(x, identity) || Value(x, "0020000D") != uid))
            throw new BusinessException(
                "imaging_identity_mismatch",
                "影像患者标识或来源与当前患者不匹配，已阻止访问",
                409
            );
        return items;
    }

    public async Task<(byte[] Bytes, string ContentType)> Get(
        string path,
        string accept,
        CancellationToken ct
    )
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("Accept", accept);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new BusinessException("imaging_not_found", "影像资源不存在", 404);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Imaging upstream status {Status}", (int)response.StatusCode);
                throw new BusinessException(
                    "imaging_unavailable",
                    "影像服务暂不可用，请稍后重试",
                    503
                );
            }
            // Bound memory and total body time as well as connection/header time.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = await body.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (output.Length + count > 64 * 1024 * 1024)
                    throw new BusinessException(
                        "imaging_too_large",
                        "影像响应超过本演示的 64 MiB 上限",
                        502
                    );
                output.Write(buffer, 0, count);
            }
            return (
                output.ToArray(),
                response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream"
            );
        }
        catch (Exception ex)
            when (ex is HttpRequestException or IOException
                || ex is OperationCanceledException && !ct.IsCancellationRequested
            )
        {
            // Don't log credentials, URL query strings or patient metadata.
            logger.LogWarning("Imaging upstream failure {Kind}", ex.GetType().Name);
            throw new BusinessException("imaging_unavailable", "影像服务暂不可用，请稍后重试", 503);
        }
    }
}

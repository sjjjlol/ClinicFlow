using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClinicFlow.Scheduling;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Imaging;

public static class ImagingEndpoints
{
    public static void AddImaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ImagingService>();
        services
            .AddHttpClient<DicomWebClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["Imaging:BaseUrl"] ?? "http://127.0.0.1:8042/"
                );
                client.Timeout = TimeSpan.FromSeconds(10);
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes("clinicflow:" + configuration["Imaging:Password"])
                );
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    credentials
                );
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            );
    }

    public static void MapImaging(this WebApplication app)
    {
        var group = app.MapGroup("/api/imaging/{appointmentId}").RequireAuthorization("imaging");
        group.AddEndpointFilter(
            async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
                try
                {
                    return await next(context);
                }
                catch (BusinessException ex)
                {
                    context
                        .HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ClinicFlow.Imaging")
                        .LogWarning(
                            "Imaging request rejected {Code} {Status} {CorrelationId}",
                            ex.Code,
                            ex.Status,
                            context.HttpContext.TraceIdentifier
                        );
                    throw;
                }
            }
        );
        group.MapGet(
            "/",
            async (string appointmentId, ClinicDb db, CancellationToken ct) =>
            {
                var appointment =
                    await db
                        .Appointments.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
                    ?? throw new BusinessException("not_found", "预约不存在", 404);
                return Results.Ok(
                    new
                    {
                        identity = await db.Set<ImagingIdentity>()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(x => x.PatientId == appointment.PatientId, ct),
                        links = await db.Set<ImagingLink>()
                            .AsNoTracking()
                            .Where(x => x.AppointmentId == appointmentId)
                            .OrderBy(x => x.LinkedUtc)
                            .ToListAsync(ct),
                        audit = await db.Set<ImagingAudit>()
                            .AsNoTracking()
                            .Where(x => x.AppointmentId == appointmentId)
                            .OrderByDescending(x => x.Id)
                            .Take(30)
                            .ToListAsync(ct),
                    }
                );
            }
        );
        group.MapGet(
            "/search",
            async (
                string appointmentId,
                ImagingService service,
                DicomWebClient dicom,
                CancellationToken ct
            ) =>
            {
                var result = await dicom.Search(await service.Identity(appointmentId, ct), ct);
                return Results.Ok(new { items = result.Items, truncated = result.Truncated });
            }
        );
        group.MapPost(
            "/links",
            async (
                string appointmentId,
                LinkStudy input,
                ImagingService service,
                HttpContext ctx,
                CancellationToken ct
            ) =>
            {
                await service.Link(
                    appointmentId,
                    input.StudyInstanceUid,
                    ctx.User.Identity!.Name!,
                    ctx.TraceIdentifier,
                    ct
                );
                return Results.NoContent();
            }
        );
        group.MapPost(
            "/links/{uid}/remove",
            async (
                string appointmentId,
                string uid,
                ImagingService service,
                HttpContext ctx,
                CancellationToken ct
            ) =>
            {
                await service.Remove(
                    appointmentId,
                    uid,
                    ctx.User.Identity!.Name!,
                    ctx.TraceIdentifier,
                    ct
                );
                return Results.NoContent();
            }
        );
        group.MapGet(
            "/studies/{uid}/metadata",
            async (
                string appointmentId,
                string uid,
                ImagingService service,
                CancellationToken ct
            ) =>
            {
                var metadata = await service.LinkedMetadata(appointmentId, uid, ct);
                return Results.Ok(
                    metadata.Select(x => new
                    {
                        seriesInstanceUid = DicomWebClient.Value(x, "0020000E"),
                        sopInstanceUid = DicomWebClient.Value(x, "00080018"),
                        seriesDescription = DicomWebClient.Value(x, "0008103E"),
                        modality = DicomWebClient.Value(x, "00080060"),
                        instanceNumber = DicomWebClient.Value(x, "00200013"),
                    })
                );
            }
        );
        group.MapGet(
            "/studies/{uid}/series/{series}/instances/{instance}/file",
            async (
                string appointmentId,
                string uid,
                string series,
                string instance,
                ImagingService service,
                DicomWebClient dicom,
                CancellationToken ct
            ) =>
            {
                DicomWebClient.ValidateUid(series);
                DicomWebClient.ValidateUid(instance);
                var metadata = await service.LinkedMetadata(appointmentId, uid, ct);
                if (
                    !metadata.Any(x =>
                        DicomWebClient.Value(x, "0020000E") == series
                        && DicomWebClient.Value(x, "00080018") == instance
                    )
                )
                    throw new BusinessException("imaging_not_found", "影像实例不存在", 404);
                var (bytes, type) = await dicom.Get(
                    $"dicom-web/studies/{uid}/series/{series}/instances/{instance}",
                    "multipart/related; type=\"application/dicom\"; transfer-syntax=*",
                    ct
                );
                var media = Microsoft.Net.Http.Headers.MediaTypeHeaderValue.Parse(type);
                var boundary = Microsoft
                    .Net.Http.Headers.HeaderUtilities.RemoveQuotes(media.Boundary)
                    .Value;
                if (string.IsNullOrEmpty(boundary))
                    throw new BusinessException(
                        "imaging_invalid_response",
                        "影像服务未返回 DICOM multipart 数据",
                        502
                    );
                var reader = new MultipartReader(boundary, new MemoryStream(bytes));
                var section =
                    await reader.ReadNextSectionAsync(ct)
                    ?? throw new BusinessException("imaging_invalid_response", "影像文件为空", 502);
                using var file = new MemoryStream();
                await section.Body.CopyToAsync(file, ct);
                return Results.File(file.ToArray(), "application/dicom", instance + ".dcm");
            }
        );
        MapViewer(group);
    }

    private static void MapViewer(RouteGroupBuilder group)
    {
        // Separate static plugin resources from the study-scoped DICOMweb gateway.
        group
            .MapGet(
                "/studies/{uid}/viewer/{**asset}",
                async (
                    string appointmentId,
                    string uid,
                    string? asset,
                    ImagingService service,
                    DicomWebClient dicom,
                    CancellationToken ct
                ) =>
                {
                    await service.LinkedMetadata(appointmentId, uid, ct);
                    asset ??= "index.html";
                    if (!Regex.IsMatch(asset, @"^[a-zA-Z0-9_./-]+$") || asset.Contains(".."))
                        throw new BusinessException("not_found", "资源不存在", 404);
                    if (asset == "configuration.json")
                        return Results.Json(
                            new
                            {
                                StoneWebViewer = new
                                {
                                    DicomWebRoot = "../dicom-web",
                                    OrthancApiRoot = "",
                                    ShowInfoPanelAtStartup = "Never",
                                    DicomWebHttpHeaders = new { },
                                    DownloadStudyEnabled = false,
                                    PrintEnabled = false,
                                    ShowNotForDiagnosticUsageDisclaimer = true,
                                    ShowUserPreferencesButton = true,
                                    CombinedToolEnabled = true,
                                    CombinedToolBehaviour = new
                                    {
                                        LeftMouseButton = "Windowing",
                                        MiddleMouseButton = "Pan",
                                        RightMouseButton = "Zoom",
                                    },
                                    AnnotationsColor = new[] { 64, 130, 173 },
                                    HighlightedAnnotationsColor = new[] { 64, 173, 121 },
                                },
                            },
                            new JsonSerializerOptions { PropertyNamingPolicy = null }
                        );
                    var (bytes, type) = await dicom.Get("stone-webviewer/" + asset, "*/*", ct);
                    return Results.Bytes(bytes, type);
                }
            )
            .ExcludeFromDescription();
        group
            .MapGet(
                "/studies/{uid}/dicom-web/{**resource}",
                async (
                    string appointmentId,
                    string uid,
                    string? resource,
                    ImagingService service,
                    DicomWebClient dicom,
                    HttpContext ctx,
                    CancellationToken ct
                ) =>
                {
                    await service.LinkedMetadata(appointmentId, uid, ct);
                    resource ??= "";
                    // Never forward arbitrary upstream paths, methods, credentials or filters.
                    var pattern =
                        @"^studies/"
                        + Regex.Escape(uid)
                        + @"(?:/metadata|/series(?:/[0-9.]+(?:/metadata|/rendered|/instances(?:/[0-9.]+(?:/metadata|/frames/[0-9,]+(?:/rendered)?)?)?)?)?)?/?$";
                    string upstream;
                    if (resource.TrimEnd('/') is "studies" or "series" or "instances")
                    {
                        if (
                            (
                                ctx.Request.Query["StudyInstanceUID"].ToString() != uid
                                && ctx.Request.Query["0020000D"].ToString() != uid
                            )
                            || (
                                ctx.Request.Query.ContainsKey("StudyInstanceUID")
                                && ctx.Request.Query["StudyInstanceUID"] != uid
                            )
                            || (
                                ctx.Request.Query.ContainsKey("0020000D")
                                && ctx.Request.Query["0020000D"] != uid
                            )
                        )
                            throw new BusinessException(
                                "imaging_scope",
                                "查看器仅可访问当前关联的检查",
                                403
                            );
                        upstream =
                            resource.TrimEnd('/') == "studies"
                                ? "dicom-web/studies?StudyInstanceUID="
                                    + uid
                                    + "&includefield=00081030"
                                : "dicom-web/series?StudyInstanceUID="
                                    + uid
                                    + "&includefield=0008103E&includefield=00200011";
                        if (resource.TrimEnd('/') == "instances")
                        {
                            var seriesUid = ctx.Request.Query["0020000E"].ToString();
                            DicomWebClient.ValidateUid(seriesUid);
                            upstream =
                                $"dicom-web/studies/{uid}/series/{seriesUid}/instances?includefield=00080016";
                        }
                    }
                    else
                    {
                        if (!Regex.IsMatch(resource, pattern))
                            throw new BusinessException(
                                "imaging_scope",
                                "查看器请求超出当前检查范围",
                                403
                            );
                        upstream = "dicom-web/" + resource;
                        // Fixed projection; caller filters must not change the authorization scope.
                        if (resource.EndsWith("/series") || resource.EndsWith("/instances"))
                            upstream += "?includefield=all";
                    }
                    var accept = ctx.Request.Headers.Accept.ToString();
                    var (bytes, type) = await dicom.Get(
                        upstream,
                        string.IsNullOrEmpty(accept) ? "application/dicom+json" : accept,
                        ct
                    );
                    if (type.Contains("json"))
                    {
                        // Do not leak internal addresses in RetrieveURL / BulkDataURI.
                        var node = System.Text.Json.Nodes.JsonNode.Parse(bytes);
                        RewriteUrls(node, $"/api/imaging/{appointmentId}/studies/{uid}/dicom-web/");
                        bytes = JsonSerializer.SerializeToUtf8Bytes(node);
                    }
                    return Results.Bytes(bytes, type);
                }
            )
            .ExcludeFromDescription();
    }

    private static void RewriteUrls(System.Text.Json.Nodes.JsonNode? node, string root)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
            foreach (var pair in obj.ToArray())
            {
                if (
                    pair.Value is System.Text.Json.Nodes.JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && text.Contains("/dicom-web/")
                )
                    obj[pair.Key] = root + text.Split("/dicom-web/", 2)[1];
                else
                    RewriteUrls(pair.Value, root);
            }
        else if (node is System.Text.Json.Nodes.JsonArray array)
            for (var i = 0; i < array.Count; i++)
            {
                if (
                    array[i] is System.Text.Json.Nodes.JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && text.Contains("/dicom-web/")
                )
                    array[i] = root + text.Split("/dicom-web/", 2)[1];
                else
                    RewriteUrls(array[i], root);
            }
    }
}

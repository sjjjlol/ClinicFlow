using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Fhir;

// ===== FHIR R4 只读适配器：把领域模型映射为 FHIR 资源 JSON（演示"显式边界"的 API 设计）=====
public static class FhirAdapter
{
    // 返回 object + 匿名类型：按需拼出 FHIR 资源形状，直接 JSON 序列化（无需建一套 FHIR 模型类）。
    public static object PatientResource(Patient p) =>
        new
        {
            resourceType = "Patient",
            id = p.Id.ToString(),
            active = true,
            identifier = new[] // new[] { ... }：匿名对象数组
            {
                new { system = "https://clinicflow.example/patient", value = p.Identifier },
            },
            name = new[] { new { text = p.Name } },
        };

    public static object AppointmentResource(Appointment a, string resourceName) =>
        new
        {
            resourceType = "Appointment",
            id = a.Id,
            meta = new
            {
                versionId = a.Version.ToString(),
                // DateTime.SpecifyKind：只改 Kind 标记不改值——库中存的是 UTC 值但 Kind 未指定，
                // 序列化前补上 Utc 标记，System.Text.Json 才会输出带 "Z" 的格式。
                lastUpdated = DateTime.SpecifyKind(a.UpdatedUtc, DateTimeKind.Utc),
            },
            // switch 表达式：领域状态 → FHIR 状态码；_ 兜底抛异常（编译器要求穷尽或提供默认）。
            status = a.Status switch
            {
                "Pending" => "pending",
                "Confirmed" => "booked",
                "Cancelled" => "cancelled",
                "Completed" => "fulfilled",
                _ => throw new ArgumentException("Unsupported domain status"),
            },
            start = DateTime.SpecifyKind(a.StartUtc, DateTimeKind.Utc),
            end = DateTime.SpecifyKind(a.EndUtc, DateTimeKind.Utc),
            minutesDuration = (int)(a.EndUtc - a.StartUtc).TotalMinutes, // (int) 强制截断转换
            description = "Fictional scheduling exercise",
            participant = new object[] // object[]：元素形状不同的异构数组
            {
                new
                {
                    actor = new { reference = "Patient/" + a.PatientId },
                    // 嵌套三元表达式排版：一行一个分支，读作 if/else if/else。
                    status = a.Status is "Confirmed" or "Completed" ? "accepted"
                    : a.Status == "Cancelled" ? "declined"
                    : "needs-action",
                },
                new
                {
                    actor = new
                    {
                        type = "Location",
                        identifier = new
                        {
                            system = "https://clinicflow.example/resource",
                            value = a.ResourceId.ToString(),
                        },
                        display = resourceName,
                    },
                    status = a.Status is "Confirmed" or "Completed" ? "accepted"
                    : a.Status == "Cancelled" ? "declined"
                    : "needs-action",
                },
            },
        };

    // IResult：端点返回结果的抽象（≈ ResponseEntity）；Results.Json 指定 contentType 与序列化选项。
    static IResult Fhir(object value, int status = 200) =>
        Results.Json(value, SchedulingService.Json, "application/fhir+json", statusCode: status);

    // FHIR 错误响应：OperationOutcome 资源。
    static IResult Outcome(int status, string code, string diagnostics) =>
        Fhir(
            new
            {
                resourceType = "OperationOutcome",
                issue = new[]
                {
                    new
                    {
                        severity = "error",
                        code,
                        diagnostics,
                    },
                },
            },
            status
        );

    // Validate：显式边界——只支持按 id 读；返回 IResult?（可空），null 表示校验通过。
    static IResult? Validate(HttpContext ctx)
    {
        if (ctx.Request.Query.Count > 0)
            return Outcome(
                400,
                "not-supported",
                "This exercise supports read by id only; query parameters are not supported."
            );
        // ctx.Request.Headers.Accept ≈ request.getHeader("Accept")；ToString() 把多值拼成逗号串。
        var accept = ctx.Request.Headers.Accept.ToString();
        if (accept.Length > 0 && !accept.Contains("json") && !accept.Contains("*/*"))
            return Outcome(406, "not-supported", "Only application/fhir+json is supported.");
        return null;
    }

    public static void MapFhir(this WebApplication app)
    {
        var group = app.MapGroup("/fhir/r4").RequireAuthorization();
        // CapabilityStatement：声明本服务只支持 Patient/Appointment 的 read。
        group.MapGet(
            "/metadata",
            () =>
                Fhir(
                    new
                    {
                        resourceType = "CapabilityStatement",
                        status = "active",
                        date = "2026-09-24",
                        kind = "instance",
                        fhirVersion = "4.0.1",
                        format = new[] { "json" },
                        implementation = new
                        {
                            description = "ClinicFlow limited educational read adapter",
                        },
                        rest = new[]
                        {
                            new
                            {
                                mode = "server",
                                resource = new[]
                                {
                                    new
                                    {
                                        type = "Patient",
                                        interaction = new[] { new { code = "read" } },
                                    },
                                    new
                                    {
                                        type = "Appointment",
                                        interaction = new[] { new { code = "read" } },
                                    },
                                },
                            },
                        },
                    }
                )
        );
        group.MapGet(
            "/Patient/{id}",
            async (string id, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
            {
                var invalid = Validate(ctx);
                if (invalid is not null)
                    return invalid;
                // int.TryParse(s, out var number)：out 参数内联声明（≈ 返回 bool + 输出解析值，不抛异常）。
                if (!int.TryParse(id, out var number) || number < 1)
                    return Outcome(400, "invalid", "Patient id must be a positive catalog id.");
                var p = await db
                    .Patients.AsNoTracking()
                    .VisibleTo(ctx.User)
                    .SingleOrDefaultAsync(x => x.Id == number, ct);
                return p is null
                    ? Outcome(404, "not-found", "Patient not found.")
                    : Fhir(PatientResource(p));
            }
        );
        group.MapGet(
            "/Appointment/{id}",
            async (string id, ClinicDb db, HttpContext ctx, CancellationToken ct) =>
            {
                var invalid = Validate(ctx);
                if (invalid is not null)
                    return invalid;
                // Guid.TryParseExact(id, "D", out _)：out _ 丢弃输出（只关心是否解析成功）。
                if (!Guid.TryParseExact(id, "D", out _))
                    return Outcome(
                        400,
                        "invalid",
                        "Appointment id must use the UUID representation."
                    );
                var a = await db
                    .Appointments.AsNoTracking()
                    .VisibleTo(ctx.User)
                    .SingleOrDefaultAsync(x => x.Id == id, ct);
                if (a is null)
                    return Outcome(404, "not-found", "Appointment not found.");
                // FHIR 版本化读取：ETag/Last-Modified 头（≈ HTTP 缓存条件请求）。
                ctx.Response.Headers.ETag = $"W/\"{a.Version}\"";
                ctx.Response.Headers.LastModified = DateTime
                    .SpecifyKind(a.UpdatedUtc, DateTimeKind.Utc)
                    .ToString("R"); // "R" = RFC1123 格式（HTTP 日期）
                var resource = await db
                    .Resources.AsNoTracking()
                    .SingleAsync(x => x.Id == a.ResourceId, ct); // SingleAsync：必须恰好 1 条，否则抛异常
                return Fhir(AppointmentResource(a, resource.Name));
            }
        );
        // 兜底：/fhir/r4 下其他一切路径/方法 → 统一 not-supported（显式边界的收尾）。
        group.MapMethods(
            "/{**path}",
            ["GET", "POST", "PUT", "PATCH", "DELETE"],
            (HttpContext ctx) =>
                Outcome(
                    ctx.Request.Method == "GET" ? 400 : 405,
                    "not-supported",
                    "Only metadata and Patient/Appointment read by id are supported."
                )
        );
    }
}

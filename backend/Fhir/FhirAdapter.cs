using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Fhir;

public static class FhirAdapter
{
    public static object PatientResource(Patient p) =>
        new
        {
            resourceType = "Patient",
            id = p.Id.ToString(),
            active = true,
            identifier = new[]
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
                lastUpdated = DateTime.SpecifyKind(a.UpdatedUtc, DateTimeKind.Utc),
            },
            status = a.Status switch
            {
                "Pending" => "pending",
                "Confirmed" => "booked",
                "Cancelled" => "cancelled",
                _ => throw new ArgumentException("Unsupported domain status"),
            },
            start = DateTime.SpecifyKind(a.StartUtc, DateTimeKind.Utc),
            end = DateTime.SpecifyKind(a.EndUtc, DateTimeKind.Utc),
            minutesDuration = (int)(a.EndUtc - a.StartUtc).TotalMinutes,
            description = "Fictional scheduling exercise",
            participant = new object[]
            {
                new
                {
                    actor = new { reference = "Patient/" + a.PatientId },
                    status = a.Status == "Confirmed" ? "accepted"
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
                    status = a.Status == "Confirmed" ? "accepted"
                    : a.Status == "Cancelled" ? "declined"
                    : "needs-action",
                },
            },
        };

    static IResult Fhir(object value, int status = 200) =>
        Results.Json(value, SchedulingService.Json, "application/fhir+json", statusCode: status);

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

    static IResult? Validate(HttpContext ctx)
    {
        if (ctx.Request.Query.Count > 0)
            return Outcome(
                400,
                "not-supported",
                "This exercise supports read by id only; query parameters are not supported."
            );
        var accept = ctx.Request.Headers.Accept.ToString();
        if (accept.Length > 0 && !accept.Contains("json") && !accept.Contains("*/*"))
            return Outcome(406, "not-supported", "Only application/fhir+json is supported.");
        return null;
    }

    public static void MapFhir(this WebApplication app)
    {
        var group = app.MapGroup("/fhir/r4").RequireAuthorization();
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
                if (!int.TryParse(id, out var number) || number < 1)
                    return Outcome(400, "invalid", "Patient id must be a positive catalog id.");
                var p = await db
                    .Patients.AsNoTracking()
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
                if (!Guid.TryParseExact(id, "D", out _))
                    return Outcome(
                        400,
                        "invalid",
                        "Appointment id must use the UUID representation."
                    );
                var a = await db
                    .Appointments.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == id, ct);
                if (a is null)
                    return Outcome(404, "not-found", "Appointment not found.");
                ctx.Response.Headers.ETag = $"W/\"{a.Version}\"";
                ctx.Response.Headers.LastModified = DateTime
                    .SpecifyKind(a.UpdatedUtc, DateTimeKind.Utc)
                    .ToString("R");
                var resource = await db
                    .Resources.AsNoTracking()
                    .SingleAsync(x => x.Id == a.ResourceId, ct);
                return Fhir(AppointmentResource(a, resource.Name));
            }
        );
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

using System.Net;
using System.Text;
using System.Text.Json;
using ClinicFlow.Imaging;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClinicFlow.Tests;

public class ImagingTests : IAsyncLifetime
{
    private readonly SchedulingTests fixture = new();

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => fixture.DisposeAsync();

    private ClinicFlow.ClinicDb Db() => fixture.Db();

    private Task<Appointment> Create() => fixture.Create();

    public const string Uid = "2.25.123";

    public static string Metadata(
        string patient = "CF-IMG-001",
        string issuer = "ClinicFlowDemo",
        string uid = Uid
    ) =>
        JsonSerializer.Serialize(
            new[]
            {
                new Dictionary<string, object>
                {
                    ["00100020"] = new { vr = "LO", Value = new[] { patient } },
                    ["00100021"] = new { vr = "LO", Value = new[] { issuer } },
                    ["0020000D"] = new { vr = "UI", Value = new[] { uid } },
                    ["00081030"] = new { vr = "LO", Value = new[] { "Synthetic" } },
                },
            }
        );

    public static DicomWebClient Client(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(
            new HttpClient(
                new StubHandler(
                    (_, _) =>
                        Task.FromResult(
                            new HttpResponseMessage(code)
                            {
                                Content = new StringContent(
                                    body,
                                    Encoding.UTF8,
                                    "application/dicom+json"
                                ),
                            }
                        )
                )
            )
            {
                BaseAddress = new Uri("http://example.invalid/"),
            },
            NullLogger<DicomWebClient>.Instance
        );

    [Fact]
    public async Task Imaging_LinkDuplicateUnlinkKeepsAuditAndAppointmentVersion()
    {
        var a = await Create();
        await using (var db = Db())
        {
            await new ImagingService(db, Client(Metadata())).Link(
                a.Id,
                Uid,
                "scheduler",
                "trace-link",
                default
            );
        }
        await using (var db = Db())
        {
            var error = await Assert.ThrowsAsync<BusinessException>(() =>
                new ImagingService(db, Client(Metadata())).Link(
                    a.Id,
                    Uid,
                    "scheduler",
                    "duplicate",
                    default
                )
            );
            Assert.Equal("imaging_duplicate", error.Code);
        }
        await using (var db = Db())
        {
            Assert.Equal(1, await db.Set<ImagingLink>().CountAsync());
            Assert.Equal(1, await db.Set<ImagingAudit>().CountAsync());
            await new ImagingService(db, Client("", HttpStatusCode.ServiceUnavailable)).Remove(
                a.Id,
                Uid,
                "taskoperator",
                "trace-remove",
                default
            );
            Assert.Empty(await db.Set<ImagingLink>().ToListAsync());
            Assert.Equal(
                new[] { "Linked", "Unlinked" },
                await db.Set<ImagingAudit>().OrderBy(x => x.Id).Select(x => x.Action).ToArrayAsync()
            );
            Assert.Equal(a.Version, (await db.Appointments.SingleAsync()).Version);
        }
    }

    [Theory]
    [InlineData("CF-IMG-002", "ClinicFlowDemo")]
    [InlineData("CF-IMG-001", "OtherHospital")]
    [InlineData("CF-IMG-001", "")]
    public async Task Imaging_RejectsWrongPatientOrIssuerWithoutWriting(
        string patient,
        string issuer
    )
    {
        var a = await Create();
        await using var db = Db();
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new ImagingService(db, Client(Metadata(patient, issuer))).Link(
                a.Id,
                Uid,
                "scheduler",
                "mismatch",
                default
            )
        );
        Assert.Equal("imaging_identity_mismatch", error.Code);
        Assert.Empty(await db.Set<ImagingLink>().ToListAsync());
        Assert.Empty(await db.Set<ImagingAudit>().ToListAsync());
    }

    [Fact]
    public async Task Imaging_MissingMappingAndUnlinkedStudyAreRejected()
    {
        var a = await Create();
        await using var db = Db();
        var service = new ImagingService(db, Client(Metadata()));
        Assert.Equal(
            "imaging_not_linked",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    service.LinkedMetadata(a.Id, Uid, default)
                )
            ).Code
        );
        await db.Set<ImagingIdentity>().Where(x => x.PatientId == 1).ExecuteDeleteAsync();
        Assert.Equal(
            "imaging_identity_missing",
            (
                await Assert.ThrowsAsync<BusinessException>(() => service.Identity(a.Id, default))
            ).Code
        );
    }

    [Fact]
    public async Task Imaging_ConcurrentLinksHaveOneWinnerAndOneAudit()
    {
        var a = await Create();
        async Task<string> Link()
        {
            await using var db = Db();
            try
            {
                await new ImagingService(db, Client(Metadata())).Link(
                    a.Id,
                    Uid,
                    "scheduler",
                    "race",
                    default
                );
                return "ok";
            }
            catch (BusinessException ex)
            {
                return ex.Code;
            }
        }
        Assert.Equal(
            new[] { "imaging_duplicate", "ok" },
            (await Task.WhenAll(Link(), Link())).Order().ToArray()
        );
        await using var check = Db();
        Assert.Equal(1, await check.Set<ImagingAudit>().CountAsync());
    }
}

public class DicomWebTests
{
    [Theory]
    [InlineData("../system")]
    [InlineData("1.02.3")]
    [InlineData("")]
    [InlineData("1.2?patient=other")]
    public void InvalidUidsAreRejected(string uid) =>
        Assert.Throws<BusinessException>(() => DicomWebClient.ValidateUid(uid));

    [Fact]
    public async Task ChecksEveryInstanceAndRevalidatesAfterAssociation()
    {
        var mixed =
            ImagingTests.Metadata().TrimEnd(']')
            + ","
            + ImagingTests.Metadata("CF-IMG-002").TrimStart('[');
        var identity = new ImagingIdentity
        {
            ExternalPatientId = "CF-IMG-001",
            Issuer = "ClinicFlowDemo",
        };
        Assert.Equal(
            "imaging_identity_mismatch",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    ImagingTests.Client(mixed).Verify(ImagingTests.Uid, identity, default)
                )
            ).Code
        );
    }

    [Fact]
    public async Task UnavailableAndMalformedResponsesAreDistinct()
    {
        Assert.Equal(
            "imaging_unavailable",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    ImagingTests
                        .Client("", HttpStatusCode.Unauthorized)
                        .Json("dicom-web/studies", default)
                )
            ).Code
        );
        Assert.Equal(
            "imaging_invalid_response",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    ImagingTests.Client("not json").Json("dicom-web/studies", default)
                )
            ).Code
        );
        var client = new DicomWebClient(
            new HttpClient(
                new StubHandler((_, _) => throw new HttpRequestException("connection refused"))
            )
            {
                BaseAddress = new Uri("http://example.invalid"),
            },
            NullLogger<DicomWebClient>.Instance
        );
        Assert.Equal(
            "imaging_unavailable",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    client.Json("dicom-web/studies", default)
                )
            ).Code
        );
    }
}

internal sealed class StubHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action
) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => action(request, cancellationToken);
}

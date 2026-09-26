using System.Security.Claims;
using System.Text.Json;
using ClinicFlow.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClinicFlow.Tests;

public class AccountLifecycleTests : IAsyncLifetime
{
    readonly SchedulingTests fixture = new();

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    const string Password = "12345678";

    sealed class Clock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    async Task<DemoUser> Register(string name)
    {
        await using var db = fixture.Db();
        return await Identity.Register(new(name, Password, "测试预约者"), db, default);
    }

    async Task<Appointment> Confirmed()
    {
        var a = await fixture.Create();
        await using var db = fixture.Db();
        foreach (var task in await db.Tasks.Where(x => x.AppointmentId == a.Id).ToListAsync())
            a = await fixture.Mutate(a, "complete-task", new(a.Version, TaskId: task.Id));
        return await fixture.Mutate(a, "confirm");
    }

    async Task<Appointment> Complete(
        Appointment a,
        string key = "complete",
        ITransactionProbe? probe = null
    )
    {
        await using var db = fixture.Db();
        return await new SchedulingService(
            db,
            probe ?? new NoTransactionProbe(),
            new Clock(a.EndUtc)
        ).Mutate(a.Id, "complete", new(a.Version), "scheduler", key, "test", default);
    }

    [Fact]
    public async Task RegistrationIsNormalizedHashedAndBoundToOnePatient()
    {
        var user = await Register("  Alice_Test  ");
        Assert.Equal("alice_test", user.Id);
        Assert.Equal("Booker", user.Role);
        Assert.NotNull(user.PatientId);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<DemoUser>().VerifyHashedPassword(user, user.PasswordHash, Password)
        );
        await using var db = fixture.Db();
        Assert.Equal("测试预约者", (await db.Patients.FindAsync(user.PatientId))!.Name);
    }

    [Fact]
    public async Task ConcurrentDuplicateRegistrationDoesNotLeaveOrphanPatient()
    {
        async Task<string> Attempt()
        {
            try
            {
                await Register("same_user");
                return "created";
            }
            catch (BusinessException ex)
            {
                return ex.Code;
            }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x == "created");
        Assert.Single(results, x => x == "username_taken");
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(3, await db.Patients.CountAsync()); // two seed patients plus the winning registration
    }

    [Theory]
    [InlineData("ab", Password, "姓名", "invalid_username")]
    [InlineData("a@b.com", Password, "姓名", "invalid_username")]
    [InlineData("valid_user", "1234567", "姓名", "invalid_password")]
    [InlineData("valid_user", Password, "  ", "invalid_name")]
    public async Task InvalidRegistrationsWriteNothing(
        string username,
        string password,
        string displayName,
        string code
    )
    {
        await using var db = fixture.Db();
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            Identity.Register(new(username, password, displayName), db, default)
        );
        Assert.Equal(code, error.Code);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(2, await db.Patients.CountAsync());
    }

    [Fact]
    public async Task AccountScopeHidesOtherPatientsAndRejectsCrossAccountMutation()
    {
        var alice = await Register("alice");
        var bob = await Register("bob");
        var a = await fixture.Create(
            SchedulingTests.Book() with
            {
                PatientId = alice.PatientId!.Value,
            }
        );
        await fixture.Create(
            SchedulingTests.Book(hour: 11) with
            {
                PatientId = bob.PatientId!.Value,
            }
        );
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new(ClaimTypes.Role, "Booker"), new("patient_id", alice.PatientId.ToString()!)],
                "test"
            )
        );
        await using var db = fixture.Db();
        Assert.Equal(a.Id, (await db.Appointments.VisibleTo(principal).SingleAsync()).Id);
        Assert.Equal(alice.PatientId, (await db.Patients.VisibleTo(principal).SingleAsync()).Id);
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new SchedulingService(db, new NoTransactionProbe()).Mutate(
                a.Id,
                "cancel",
                new(a.Version),
                bob.Id,
                "forged",
                "test",
                default,
                bob.PatientId
            )
        );
        Assert.Equal("appointment_missing", error.Code);
        Assert.Equal(
            "Pending",
            (await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == a.Id)).Status
        );
    }

    [Fact]
    public async Task CompletionIsAtomicReplayableAndTerminal()
    {
        var a = await Confirmed();
        var completed = await Complete(a);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(a.Version + 1, completed.Version);
        Assert.Equal(completed.Version, (await Complete(a)).Version);
        await using var db = fixture.Db();
        Assert.Equal(0, await db.SlotClaims.CountAsync());
        Assert.Equal(1, await db.Audits.CountAsync(x => x.Action == "Completed"));
        var outbox = await db.Outbox.SingleAsync(x => x.Version == completed.Version);
        Assert.Equal(
            "Completed",
            JsonDocument.Parse(outbox.Payload).RootElement.GetProperty("status").GetString()
        );
        foreach (
            var action in new[] { "complete", "cancel", "reschedule", "confirm", "complete-task" }
        )
            Assert.Equal(
                "invalid_state",
                (
                    await Assert.ThrowsAsync<BusinessException>(() =>
                        fixture.Mutate(completed, action)
                    )
                ).Code
            );
        Assert.Equal(
            "version_conflict",
            (await Assert.ThrowsAsync<BusinessException>(() => Complete(a, "stale"))).Code
        );
    }

    [Fact]
    public async Task CompletionRequiresConfirmedAndElapsedEndTime()
    {
        var pending = await fixture.Create();
        Assert.Equal(
            "invalid_state",
            (await Assert.ThrowsAsync<BusinessException>(() => Complete(pending))).Code
        );
        await fixture.Mutate(pending, "cancel");
        var confirmed = await Confirmed();
        await using var db = fixture.Db();
        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            new SchedulingService(
                db,
                new NoTransactionProbe(),
                new Clock(confirmed.EndUtc.AddTicks(-1))
            ).Mutate(
                confirmed.Id,
                "complete",
                new(confirmed.Version),
                "scheduler",
                "early",
                "test",
                default
            )
        );
        Assert.Equal("appointment_not_ended", error.Code);
        Assert.Equal(4, await db.SlotClaims.CountAsync());
        Assert.Equal(0, await db.Audits.CountAsync(x => x.Action == "Completed"));
    }

    sealed class FailCompletion : ITransactionProbe
    {
        public Task Reach(string point, CancellationToken ct) =>
            point == "before-save"
                ? throw new InvalidOperationException("Injected completion failure")
                : Task.CompletedTask;
    }

    [Fact]
    public async Task CompletionFailureRollsBackClaimsAuditAndOutbox()
    {
        var a = await Confirmed();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Complete(a, probe: new FailCompletion())
        );
        await using var db = fixture.Db();
        Assert.Equal("Confirmed", (await db.Appointments.SingleAsync()).Status);
        Assert.Equal(4, await db.SlotClaims.CountAsync());
        Assert.Equal(a.Version, await db.Audits.CountAsync());
        Assert.Equal(a.Version, await db.Outbox.CountAsync());
        Assert.Equal(a.Version, await db.Idempotency.CountAsync());
        Assert.Equal("Completed", (await Complete(a)).Status);
    }

    [Fact]
    public async Task ConcurrentCompletionCommitsOnlyOneTransition()
    {
        var a = await Confirmed();
        var gate = new SchedulingTests.PointGate("after-preread");
        var first = Complete(a, "first", gate);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Complete(a, "second");
        gate.Release.SetResult();
        Assert.Equal(
            "version_conflict",
            (await Assert.ThrowsAsync<BusinessException>(() => first)).Code
        );
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Audits.CountAsync(x => x.Action == "Completed"));
        Assert.Equal(1, await db.Outbox.CountAsync(x => x.Version == a.Version + 1));
    }

    [Fact]
    public async Task SelfServiceCannotBookInPastOrChangeAnAppointmentThatStarted()
    {
        var user = await Register("alice");
        var booking = SchedulingTests.Book() with { PatientId = user.PatientId!.Value };
        var a = await fixture.Create(booking);
        await using var db = fixture.Db();
        var service = new SchedulingService(db, new NoTransactionProbe(), new Clock(a.StartUtc));
        Assert.Equal(
            "appointment_started",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    service.Create(booking, user.Id, "past", "test", default, selfService: true)
                )
            ).Code
        );
        db.ChangeTracker.Clear();
        Assert.Equal(
            "appointment_started",
            (
                await Assert.ThrowsAsync<BusinessException>(() =>
                    service.Mutate(
                        a.Id,
                        "cancel",
                        new(a.Version),
                        user.Id,
                        "late-cancel",
                        "test",
                        default,
                        user.PatientId
                    )
                )
            ).Code
        );
        Assert.Equal("Pending", (await db.Appointments.AsNoTracking().SingleAsync()).Status);
    }
}

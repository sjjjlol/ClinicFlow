using ClinicFlow;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace ClinicFlow.Tests;
public class SchedulingTests : IAsyncLifetime
{
    readonly string connection = Environment.GetEnvironmentVariable("TEST_DB") ?? throw new Exception("TEST_DB must name an isolated *_tests database");
    public ClinicDb Db() => new(new DbContextOptionsBuilder<ClinicDb>().UseMySql(connection, new MySqlServerVersion(new Version(8,4,8))).Options);
    public async Task InitializeAsync()
    {
        if (!new MySqlConnectionStringBuilder(connection).Database.EndsWith("_tests")) throw new Exception("Refusing non-test database");
        await using var db = Db(); await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    public static Booking Book(int resource = 1, int hour = 9, int minute = 0, int duration = 60) => new(1, resource, new DateTimeOffset(2030,1,1,hour,minute,0,TimeSpan.Zero), new DateTimeOffset(2030,1,1,hour,minute,0,TimeSpan.Zero).AddMinutes(duration));
    public async Task<Appointment> Create(Booking? b = null, string? key = null, ITransactionProbe? probe = null)
    {
        await using var db = Db(); return await new SchedulingService(db, probe ?? new NoTransactionProbe()).Create(b ?? Book(), "scheduler", key ?? Guid.NewGuid().ToString(), "test", default);
    }
    [Fact] public void SlotsValidateBoundaries() { Assert.Equal(4, Rules.Slots(Book().StartUtc.UtcDateTime, Book().EndUtc.UtcDateTime).Length); Assert.Throws<BusinessException>(()=>Rules.Slots(Book().StartUtc.UtcDateTime.AddMinutes(1),Book().EndUtc.UtcDateTime)); }
    [Fact] public async Task A02_AtomicMultiSlot()
    {
        var a = await Create(); await using var db = Db();
        Assert.Equal(4, await db.SlotClaims.CountAsync()); Assert.Equal(1,await db.Audits.CountAsync()); Assert.Equal(1,await db.Outbox.CountAsync()); Assert.Equal(2,await db.Tasks.CountAsync()); Assert.Equal(a.Id,(await db.Appointments.SingleAsync()).Id);
    }
    [Fact] public async Task A03_CompetingIndependentConnections()
    {
        var gate = new GateProbe(); var first = Create(probe:gate); await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Create(); gate.Release.SetResult(); await first; var ex = await Assert.ThrowsAsync<BusinessException>(()=>second); Assert.Equal("slot_conflict",ex.Code);
        await using var db=Db(); Assert.Equal(1,await db.Appointments.CountAsync());Assert.Equal(4,await db.SlotClaims.CountAsync());Assert.Equal(1,await db.Idempotency.CountAsync());
    }
    [Fact] public async Task A04_PartialConflictRollsBackEverything()
    {
        await Create(Book(hour:10,duration:15)); await Assert.ThrowsAsync<BusinessException>(()=>Create(Book(hour:9,minute:30,duration:45)));
        await using var db=Db(); Assert.Equal(1,await db.Appointments.CountAsync());Assert.Equal(1,await db.SlotClaims.CountAsync());Assert.Equal(1,await db.Idempotency.CountAsync());Assert.Equal(1,await db.Outbox.CountAsync());Assert.Equal(1,await db.Audits.CountAsync());
    }
    [Fact] public async Task A10_A11_ConcurrentAndLostResponseReplay()
    {
        var gate=new GateProbe(); var first=Create(key:"same",probe:gate);await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var second=Create(key:"same");gate.Release.SetResult();
        var a=await first;Assert.Equal(a.Id,(await second).Id);Assert.Equal(a.Id,(await Create(key:"same")).Id);
        Assert.Equal("idempotency_conflict",(await Assert.ThrowsAsync<BusinessException>(()=>Create(Book(hour:11),"same"))).Code);
        await using var db=Db();Assert.Equal(1,await db.Appointments.CountAsync());Assert.Equal(1,await db.Audits.CountAsync());
    }
    [Fact] public async Task A19_AdjacentSlotsDoNotOverlap() {await Create();await Create(Book(hour:10));await using var db=Db();Assert.Equal(8,await db.SlotClaims.CountAsync());}
    public async Task<Appointment> Mutate(Appointment a, string action, Mutation? mutation = null, string? key = null, ITransactionProbe? probe = null)
    {
        await using var db = Db(); return await new SchedulingService(db, probe ?? new NoTransactionProbe()).Mutate(a.Id, action, mutation ?? new Mutation(a.Version), "scheduler", key ?? Guid.NewGuid().ToString(), "test", default);
    }
    [Fact] public async Task A05_ConflictAndInjectedFailureRestoreOldClaims()
    {
        var a = await Create(); await Create(Book(hour:11));
        var conflict = new Mutation(a.Version, 1, Book(hour:11).StartUtc, Book(hour:11).EndUtc);
        await Assert.ThrowsAsync<BusinessException>(()=>Mutate(a,"reschedule",conflict));
        var next = new Mutation(a.Version, 2, Book(hour:12).StartUtc,Book(hour:12).EndUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Mutate(a,"reschedule",next,probe:new FailProbe()));
        await using var db=Db();var saved=await db.Appointments.FindAsync(a.Id);Assert.Equal(a.StartUtc,saved!.StartUtc);Assert.Equal(a.Version,saved.Version);Assert.Equal(a.Status,saved.Status);Assert.Equal(4,await db.SlotClaims.CountAsync(x=>x.AppointmentId==a.Id));Assert.Equal(0,await db.SlotClaims.CountAsync(x=>x.ResourceId==2));Assert.Equal(2,await db.Audits.CountAsync());Assert.Equal(2,await db.Outbox.CountAsync());Assert.Equal(2,await db.Idempotency.CountAsync());
    }
    [Fact] public async Task A06_OverlapWithOwnSlots()
    {
        var a=await Create();var b=Book(hour:9,minute:30);var moved=await Mutate(a,"reschedule",new(a.Version,1,b.StartUtc,b.EndUtc));
        await using var db=Db();Assert.Equal(2,moved.Version);Assert.Equal("Pending",moved.Status);var claims=await db.SlotClaims.OrderBy(x=>x.SlotStartUtc).ToListAsync();Assert.Equal(4,claims.Count);Assert.Equal(b.StartUtc.UtcDateTime,claims.First().SlotStartUtc);
    }
    [Fact] public async Task A07_OppositeResourceMovesHaveConsistentOrder()
    {
        var a=await Create();var b=await Create(Book(resource:2,hour:11));
        var results=await Task.WhenAll(Mutate(a,"reschedule",new(a.Version,2,Book(hour:9).StartUtc,Book(hour:9).EndUtc)),Mutate(b,"reschedule",new(b.Version,1,Book(hour:11).StartUtc,Book(hour:11).EndUtc))).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.All(results,x=>Assert.Equal(2,x.Version));await using var db=Db();Assert.Equal(8,await db.SlotClaims.CountAsync());
    }
    [Fact] public async Task A08_ConcurrentOldVersionAndChangedPreread()
    {
        var a=await Create();var gate=new PointGate("after-preread");var stale=Mutate(a,"cancel",probe:gate);await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var moved=await Mutate(a,"reschedule",new(a.Version,2,Book(hour:11).StartUtc,Book(hour:11).EndUtc));gate.Release.SetResult();
        Assert.Equal("version_conflict",(await Assert.ThrowsAsync<BusinessException>(()=>stale)).Code);
        await using var db=Db();Assert.Equal(moved.ResourceId,(await db.Appointments.FindAsync(a.Id))!.ResourceId);Assert.Equal(4,await db.SlotClaims.CountAsync(x=>x.ResourceId==2));
    }
    [Fact] public async Task A12_CancelReplayReleasesOnceAndRebookingWorks()
    {
        var a=await Create();var cancelled=await Mutate(a,"cancel",key:"cancel");Assert.Equal(cancelled.Version,(await Mutate(a,"cancel",key:"cancel")).Version);
        await Assert.ThrowsAsync<BusinessException>(()=>Mutate(cancelled,"cancel"));await Create();await using var db=Db();Assert.Equal(4,await db.SlotClaims.CountAsync());Assert.Equal(3,await db.Audits.CountAsync());
    }
    public class FailProbe : ITransactionProbe { public Task Reach(string point,CancellationToken ct) { if(point=="after-release")throw new InvalidOperationException("Injected rollback");return Task.CompletedTask;} }
    public class PointGate(string target) : ITransactionProbe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Reach(string point,CancellationToken ct){if(point!=target)return;Entered.TrySetResult();await Release.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);}
    }
    public class GateProbe : ITransactionProbe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Reach(string point,CancellationToken ct){Entered.TrySetResult();await Release.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);}
    }
}

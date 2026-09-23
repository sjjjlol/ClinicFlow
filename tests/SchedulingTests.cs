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
    public class GateProbe : ITransactionProbe
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Reach(string point,CancellationToken ct){Entered.TrySetResult();await Release.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);}
    }
}

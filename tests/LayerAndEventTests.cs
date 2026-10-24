using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Events;
using ClinicFlow.Application.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class LayerTests
    {
        [Fact]
        public void Domain_and_application_dependencies_point_inward()
        {
            var domain = typeof(CareCase).Assembly;
            var application = typeof(CommandExecutor).Assembly;
            Assert.Equal("ClinicFlow.Domain", domain.GetName().Name);
            Assert.Equal("ClinicFlow.Application", application.GetName().Name);
            Assert.DoesNotContain(domain.GetReferencedAssemblies(), x => (x.Name ?? "").StartsWith("Microsoft.AspNetCore") || (x.Name ?? "").StartsWith("Microsoft.EntityFrameworkCore") || (x.Name ?? "").StartsWith("ClinicFlow."));
            Assert.DoesNotContain(application.GetReferencedAssemblies(), x => (x.Name ?? "").StartsWith("Microsoft.AspNetCore") || (x.Name ?? "").StartsWith("Microsoft.EntityFrameworkCore") || x.Name == "ClinicFlow.Api" || x.Name == "ClinicFlow.Infrastructure");
            foreach (var type in application.GetTypes().Where(x => x.Name.EndsWith("Response")))
                foreach (var property in type.GetProperties())
                {
                    Assert.NotEqual(domain, property.PropertyType.Assembly);
                    Assert.DoesNotContain(property.PropertyType.GetGenericArguments(), x => x.Assembly == domain);
                }
        }
        [Fact]
        public void Domain_behavior_records_fact_without_creating_external_messages()
        {
            var care = CareCase.Open(1, "case");
            care.SignPrescription("rx", "doctor", DateTime.UtcNow);
            var fact = Assert.IsType<PrescriptionSigned>(Assert.Single(care.DequeueDomainEvents()));
            Assert.Equal(care.Id, fact.CaseId);
            Assert.Equal("rx", fact.PrescriptionId);
            Assert.Empty(care.DequeueDomainEvents());
        }
    }
    public sealed class OutboxTests : IAsyncLifetime
    {
        private readonly DatabaseFixture fixture = new DatabaseFixture();
        public Task InitializeAsync() => fixture.InitializeAsync();
        public Task DisposeAsync() => Task.CompletedTask;
        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }
        private sealed class Publisher : IIntegrationEventPublisher
        {
            public bool Fail = true;
            public System.Collections.Generic.List<string> Ids = new System.Collections.Generic.List<string>();
            public Task Publish(IntegrationEvent message, CancellationToken ct)
            {
                Ids.Add(message.EventId);
                if (Fail) throw new InvalidOperationException("simulated lost acknowledgement");
                return Task.CompletedTask;
            }
        }
        [Fact]
        public async Task Failed_delivery_retries_same_event_and_committed_delivery_is_not_repeated()
        {
            var clock = new Clock();
            await using (var db = fixture.Db())
            {
                db.Add(new OutboxMessage { Id = Guid.NewGuid().ToString(), Type = "clinicflow.plan-registered", Version = 1, Payload = "{}", OccurredUtc = clock.Now.UtcDateTime, NextAttemptUtc = clock.Now.UtcDateTime });
                await db.SaveChangesAsync();
            }
            var publisher = new Publisher();
            await using (var db = fixture.Db())
                Assert.Equal(0, await new OutboxDispatcher(db, publisher, clock).DispatchBatch(10, default));
            await using (var db = fixture.Db())
            {
                var row = await db.Set<OutboxMessage>().SingleAsync();
                Assert.Equal(1, row.Attempts);
                Assert.Null(row.PublishedUtc);
                Assert.Equal("InvalidOperationException", row.LastError);
                Assert.True(row.NextAttemptUtc > clock.Now.UtcDateTime);
                Assert.Equal(0, await new OutboxDispatcher(db, publisher, clock).DispatchBatch(10, default));
            }
            clock.Now = clock.Now.AddMinutes(10);
            publisher.Fail = false;
            await using (var db = fixture.Db())
                Assert.Equal(1, await new OutboxDispatcher(db, publisher, clock).DispatchBatch(10, default));
            await using (var db = fixture.Db())
            {
                Assert.Equal(0, await new OutboxDispatcher(db, publisher, clock).DispatchBatch(10, default));
                var row = await db.Set<OutboxMessage>().SingleAsync();
                Assert.Equal(2, row.Attempts);
                Assert.NotNull(row.PublishedUtc);
                Assert.Null(row.LastError);
            }
            Assert.Equal(2, publisher.Ids.Count);
            Assert.Equal(publisher.Ids[0], publisher.Ids[1]);
        }
    }
}

using Clinic.Contracts.Events;
using Clinic.Application.Interfaces;
using Clinic.Infrastructure.Sqlserver.Inbox;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clinic.Infrastructure.UnitTests;

public sealed class InboxStoreTests
{
    [Fact]
    public async Task New_event_is_registered_as_processing()
    {
        await using var db = CreateDbContext();
        var store = new InboxStore(db);
        var envelope = CreateEnvelope();

        var decision = await store.TryBeginProcessingAsync(
            envelope,
            "queue-service.appointment-checked-in",
            CancellationToken.None);

        await db.SaveChangesAsync();
        var inbox = await db.InboxMessages.SingleAsync();

        Assert.Equal(InboxDecision.New, decision);
        Assert.Equal(InboxMessageStatus.Processing, inbox.Status);
        Assert.Equal(envelope.EventId, inbox.EventId);
    }

    [Fact]
    public async Task Processed_event_is_acknowledged_without_processing_again()
    {
        var envelope = CreateEnvelope();
        const string consumerName = "queue-service.appointment-checked-in";
        var databaseName = Guid.NewGuid().ToString();

        // Simulate the consumer process crashing after the inbox commit but before ACK.
        await using (var committedDb = CreateDbContext(databaseName))
        {
            var committedStore = new InboxStore(committedDb);
            await committedStore.TryBeginProcessingAsync(
                envelope,
                consumerName,
                CancellationToken.None);
            committedStore.MarkProcessed(envelope.EventId, consumerName, DateTimeOffset.UtcNow);
            await committedDb.SaveChangesAsync();
        }

        // The restarted consumer receives the broker's redelivery using a fresh DbContext.
        await using (var restartedDb = CreateDbContext(databaseName))
        {
            var restartedStore = new InboxStore(restartedDb);
            var duplicateDecision = await restartedStore.TryBeginProcessingAsync(
                envelope,
                consumerName,
                CancellationToken.None);

            Assert.Equal(InboxDecision.AlreadyProcessed, duplicateDecision);
            Assert.Single(await restartedDb.InboxMessages.ToListAsync());
        }
    }

    [Fact]
    public async Task Same_event_can_be_processed_by_different_consumers()
    {
        await using var db = CreateDbContext();
        var store = new InboxStore(db);
        var envelope = CreateEnvelope();

        var queueDecision = await store.TryBeginProcessingAsync(
            envelope,
            "queue-service.appointment-checked-in",
            CancellationToken.None);
        var notificationDecision = await store.TryBeginProcessingAsync(
            envelope,
            "notification-service.appointment-checked-in",
            CancellationToken.None);

        await db.SaveChangesAsync();

        Assert.Equal(InboxDecision.New, queueDecision);
        Assert.Equal(InboxDecision.New, notificationDecision);
        Assert.Equal(2, await db.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Expired_processing_lease_can_be_reclaimed()
    {
        await using var db = CreateDbContext();
        var store = new InboxStore(db);
        var envelope = CreateEnvelope();
        const string consumerName = "queue-service.appointment-checked-in";

        await store.TryBeginProcessingAsync(
            envelope,
            consumerName,
            CancellationToken.None);
        await db.SaveChangesAsync();
        var inbox = await db.InboxMessages.SingleAsync();
        inbox.ProcessingStartedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
        await db.SaveChangesAsync();

        var decision = await store.TryBeginProcessingAsync(
            envelope,
            consumerName,
            CancellationToken.None);

        Assert.Equal(InboxDecision.New, decision);
        Assert.Equal(1, inbox.RetryCount);
        Assert.True(inbox.ProcessingStartedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Failed_event_records_error_and_retry_count()
    {
        await using var db = CreateDbContext();
        var store = new InboxStore(db);
        var envelope = CreateEnvelope();
        const string consumerName = "appointment-service.exam-completed";

        await store.TryBeginProcessingAsync(
            envelope,
            consumerName,
            CancellationToken.None);
        store.MarkFailed(envelope.EventId, consumerName, "database unavailable");
        await db.SaveChangesAsync();

        var inbox = await db.InboxMessages.SingleAsync();

        Assert.Equal(InboxMessageStatus.Failed, inbox.Status);
        Assert.Equal(1, inbox.RetryCount);
        Assert.Equal("database unavailable", inbox.LastError);
        Assert.Null(inbox.ProcessingStartedAt);
    }

    private static EventEnvelope<AppointmentCheckedIn> CreateEnvelope()
    {
        return new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "appointment-service",
            Payload = new AppointmentCheckedIn(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow)
        };
    }

    private static ApplicationDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}

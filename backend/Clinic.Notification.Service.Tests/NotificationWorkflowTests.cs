using Clinic.Contracts.Events;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Tests;

public sealed class NotificationWorkflowTests
{
    private static NotificationDbContext Db() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task DuplicateBusinessEvent_CreatesOneMessageAndOneDeliveryPerChannel()
    {
        await using var db = Db(); var service = new NotificationIngestionService(db);
        var eventId = Guid.NewGuid(); var correlationId = Guid.NewGuid();
        var request = new NotificationRequested(Guid.NewGuid(), "appointment-confirmed", "Confirmed", "Body", ["in-app", "IN-APP", "email"], "patient@example.test");

        Assert.True(await service.PersistAsync(eventId, correlationId, request, default));
        Assert.False(await service.PersistAsync(eventId, correlationId, request, default));

        Assert.Single(await db.Messages.ToListAsync());
        Assert.Equal(2, await db.Deliveries.CountAsync());
        Assert.Single(await db.Inbox.ToListAsync());
    }

    [Fact]
    public async Task TransientFailure_IsAuditedAndScheduledWithBoundedBackoff()
    {
        await using var db = Db(); var delivery = Seed(db, "sms"); await db.SaveChangesAsync();
        var processor = new DeliveryProcessor(db, [new FakeProvider("sms", new(false, "provider.timeout", true))]);

        Assert.True(await processor.ProcessAsync(delivery.Id, default));

        Assert.Equal(DeliveryStatus.RetryScheduled, delivery.Status); Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.NextAttemptAt); Assert.Null(delivery.ProcessingLeaseUntil);
        var attempt = Assert.Single(delivery.Attempts); Assert.False(attempt.Succeeded); Assert.Equal("provider.timeout", attempt.ErrorCode);
    }

    [Fact]
    public async Task PermanentFailure_IsDeadLettered_AndCannotBeDeliveredTwice()
    {
        await using var db = Db(); var delivery = Seed(db, "email"); await db.SaveChangesAsync();
        var processor = new DeliveryProcessor(db, [new FakeProvider("email", new(false, "recipient.missing", false))]);

        Assert.True(await processor.ProcessAsync(delivery.Id, default));
        Assert.False(await processor.ProcessAsync(delivery.Id, default));

        Assert.Equal(DeliveryStatus.DeadLettered, delivery.Status); Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task ProviderTimeout_IsTreatedAsTransientAndAudited()
    {
        await using var db = Db(); var delivery = Seed(db, "sms"); await db.SaveChangesAsync();
        var processor = new DeliveryProcessor(db, [new ThrowingProvider("sms")]);

        Assert.True(await processor.ProcessAsync(delivery.Id, default));

        Assert.Equal(DeliveryStatus.RetryScheduled, delivery.Status);
        Assert.Equal("provider.timeout", Assert.Single(delivery.Attempts).ErrorCode);
    }

    [Fact]
    public async Task FifthTransientFailure_ExhaustsRetryAndDeadLetters()
    {
        await using var db = Db(); var delivery = Seed(db, "sms"); delivery.AttemptCount = 4; await db.SaveChangesAsync();
        var processor = new DeliveryProcessor(db, [new FakeProvider("sms", new(false, "provider.unavailable", true))]);

        Assert.True(await processor.ProcessAsync(delivery.Id, default));

        Assert.Equal(DeliveryStatus.DeadLettered, delivery.Status); Assert.Equal(5, delivery.AttemptCount); Assert.Null(delivery.NextAttemptAt);
    }

    private static NotificationDelivery Seed(NotificationDbContext db, string channel)
    {
        var message = new NotificationMessage { EventId = Guid.NewGuid(), RecipientId = Guid.NewGuid(), TemplateKey = "test", Title = "title", Body = "body", CorrelationId = Guid.NewGuid() };
        var delivery = new NotificationDelivery { Channel = channel, Status = DeliveryStatus.Pending, NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        message.Deliveries.Add(delivery); db.Messages.Add(message); return delivery;
    }

    private sealed class FakeProvider(string channel, ProviderResult result) : IDeliveryProvider
    {
        public string Channel => channel;
        public Task<ProviderResult> SendAsync(NotificationMessage message, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class ThrowingProvider(string channel) : IDeliveryProvider
    {
        public string Channel => channel;
        public Task<ProviderResult> SendAsync(NotificationMessage message, CancellationToken ct) => throw new OperationCanceledException();
    }
}

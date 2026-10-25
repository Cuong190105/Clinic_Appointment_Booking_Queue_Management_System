namespace Clinic.Queue.Service.Tests;

using Clinic.Queue.Service.Entities;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class QueueDbContextTests
{
    private static QueueDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new QueueDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrieveQueueTicket()
    {
        using var context = CreateDbContext();

        var ticket = new QueueTicket
        {
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            QueueDate = new DateOnly(2026, 10, 23),
            QueueNumber = 1,
            PatientName = "Nguyen Van A",
            DoctorName = "Dr. Strange",
            Priority = true,
            Status = QueueStatus.Waiting,
            CheckInTime = DateTime.UtcNow
        };

        context.QueueTickets.Add(ticket);
        await context.SaveChangesAsync();

        var retrieved = await context.QueueTickets.FindAsync(ticket.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(ticket.AppointmentId, retrieved.AppointmentId);
        Assert.Equal("Nguyen Van A", retrieved.PatientName);
        Assert.Equal("Dr. Strange", retrieved.DoctorName);
        Assert.True(retrieved.Priority);
        Assert.Equal(QueueStatus.Waiting, retrieved.Status);
    }

    [Fact]
    public async Task CanAddAndRetrieveQueueCounter()
    {
        using var context = CreateDbContext();

        var doctorId = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 23);

        var counter = new QueueCounter
        {
            DoctorId = doctorId,
            QueueDate = date,
            LastNumber = 5,
            UpdatedAt = DateTime.UtcNow
        };

        context.QueueCounters.Add(counter);
        await context.SaveChangesAsync();

        var retrieved = await context.QueueCounters.FindAsync(doctorId, date);
        Assert.NotNull(retrieved);
        Assert.Equal(5, retrieved.LastNumber);
    }

    [Fact]
    public async Task CanAddAndRetrieveInboxMessage()
    {
        using var context = CreateDbContext();

        var eventId = Guid.NewGuid();
        var inbox = new InboxMessage
        {
            EventId = eventId,
            ConsumerName = "queue-service.appointment-checked-in",
            EventType = "AppointmentCheckedIn",
            CorrelationId = Guid.NewGuid(),
            Status = InboxMessageStatus.Processed,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        context.InboxMessages.Add(inbox);
        await context.SaveChangesAsync();

        var retrieved = await context.InboxMessages.FirstOrDefaultAsync(m => m.EventId == eventId);
        Assert.NotNull(retrieved);
        Assert.Equal(InboxMessageStatus.Processed, retrieved.Status);
    }

    [Fact]
    public async Task CanAddAndRetrieveOutboxMessage()
    {
        using var context = CreateDbContext();

        var eventId = Guid.NewGuid();
        var outbox = new OutboxMessage
        {
            EventId = eventId,
            EventType = "ExamCompleted",
            RoutingKey = "exam.completed",
            Payload = "{\"appointmentId\":\"" + Guid.NewGuid() + "\"}",
            Status = OutboxMessageStatus.Pending
        };

        context.OutboxMessages.Add(outbox);
        await context.SaveChangesAsync();

        var retrieved = await context.OutboxMessages.FirstOrDefaultAsync(m => m.EventId == eventId);
        Assert.NotNull(retrieved);
        Assert.Equal("exam.completed", retrieved.RoutingKey);
        Assert.Equal(OutboxMessageStatus.Pending, retrieved.Status);
    }
}

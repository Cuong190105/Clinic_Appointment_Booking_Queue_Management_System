using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Queue.Service.Common.Exceptions;
using Clinic.Queue.Service.Entities;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Clinic.Queue.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.Queue.Service.Tests;

public class QueueManagerTests
{
    private static QueueDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new QueueDbContext(options);
    }

    [Fact]
    public async Task ProcessAppointmentCheckedIn_CreatesTicket_AndInboxMessage_WithWaitingStatus()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = eventId,
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, DateTimeOffset.UtcNow, false, "Nguyen Van A", "Dr. Tran")
        };

        var ticketDto = await manager.ProcessAppointmentCheckedInAsync(envelope);

        Assert.NotNull(ticketDto);
        Assert.Equal(appointmentId, ticketDto.AppointmentId);
        Assert.Equal(doctorId, ticketDto.DoctorId);
        Assert.Equal(patientId, ticketDto.PatientId);
        Assert.Equal(1, ticketDto.QueueNumber);
        Assert.Equal("Waiting", ticketDto.Status);
        Assert.Equal("Nguyen Van A", ticketDto.PatientName);
        Assert.Equal("Dr. Tran", ticketDto.DoctorName);
        Assert.False(ticketDto.Priority);

        var inbox = await db.InboxMessages.FirstOrDefaultAsync(m => m.EventId == eventId);
        Assert.NotNull(inbox);
        Assert.Equal(InboxMessageStatus.Processed, inbox.Status);

        var counter = await db.QueueCounters.FirstOrDefaultAsync(c => c.DoctorId == doctorId);
        Assert.NotNull(counter);
        Assert.Equal(1, counter.LastNumber);
    }

    [Fact]
    public async Task ProcessAppointmentCheckedIn_DuplicateEventId_IsIdempotent_DoesNotIncrementCounter()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = eventId,
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, DateTimeOffset.UtcNow, false, "Nguyen Van A", "Dr. Tran")
        };

        var ticket1 = await manager.ProcessAppointmentCheckedInAsync(envelope);
        var ticket2 = await manager.ProcessAppointmentCheckedInAsync(envelope);

        Assert.Equal(ticket1.Id, ticket2.Id);
        Assert.Equal(1, ticket1.QueueNumber);
        Assert.Equal(1, ticket2.QueueNumber);

        var counter = await db.QueueCounters.FirstOrDefaultAsync(c => c.DoctorId == doctorId);
        Assert.NotNull(counter);
        Assert.Equal(1, counter.LastNumber);

        var ticketCount = await db.QueueTickets.CountAsync();
        Assert.Equal(1, ticketCount);
    }

    [Fact]
    public async Task ProcessAppointmentCheckedIn_SameAppointmentId_DifferentEventId_IsIdempotent()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var envelope1 = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, DateTimeOffset.UtcNow, false, "Nguyen Van A", "Dr. Tran")
        };

        var envelope2 = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(appointmentId, patientId, doctorId, DateTimeOffset.UtcNow, false, "Nguyen Van A", "Dr. Tran")
        };

        var ticket1 = await manager.ProcessAppointmentCheckedInAsync(envelope1);
        var ticket2 = await manager.ProcessAppointmentCheckedInAsync(envelope2);

        Assert.Equal(ticket1.Id, ticket2.Id);
        Assert.Equal(ticket1.QueueNumber, ticket2.QueueNumber);

        var ticketCount = await db.QueueTickets.CountAsync();
        Assert.Equal(1, ticketCount);

        var counter = await db.QueueCounters.FirstOrDefaultAsync(c => c.DoctorId == doctorId);
        Assert.NotNull(counter);
        Assert.Equal(1, counter.LastNumber);
    }

    [Fact]
    public async Task ProcessAppointmentCheckedIn_InvalidPayload_ThrowsValidationException()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var envelope = new EventEnvelope<AppointmentCheckedIn>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.AppointmentCheckedIn,
            SchemaVersion = 1,
            Producer = "Clinic.Appointment.Service",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new AppointmentCheckedIn(Guid.Empty, Guid.Empty, Guid.Empty, DateTimeOffset.UtcNow)
        };

        await Assert.ThrowsAsync<QueueValidationException>(() => manager.ProcessAppointmentCheckedInAsync(envelope));
    }

    [Fact]
    public async Task CallNext_PicksPriorityFirst_ThenQueueNumber()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var today = Common.ClinicTime.GetTodayQueueDate();

        // Normal patient #1
        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Waiting
        });

        // Urgent patient #2 (checked in later)
        var urgentTicketId = Guid.NewGuid();
        db.QueueTickets.Add(new QueueTicket
        {
            Id = urgentTicketId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 2,
            Priority = true,
            Status = QueueStatus.Waiting
        });

        await db.SaveChangesAsync();

        var called = await manager.CallNextAsync(doctorId, today);

        // Priority should be called first despite higher queue number!
        Assert.Equal(urgentTicketId, called.Id);
        Assert.Equal("Called", called.Status);
        Assert.True(called.Priority);

        // Outbox message should be created
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == EventTypes.PatientCalled);
        Assert.NotNull(outbox);
        Assert.Equal("queue.patient-called", outbox.RoutingKey);
    }

    [Fact]
    public async Task CallNext_WhenDoctorHasActivePatient_ThrowsQueueConflictException()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var today = Common.ClinicTime.GetTodayQueueDate();

        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Called
        });

        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 2,
            Priority = false,
            Status = QueueStatus.Waiting
        });

        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<QueueConflictException>(() => manager.CallNextAsync(doctorId, today));
        Assert.Contains("trạng thái 'Called'", ex.Message);
    }

    [Fact]
    public async Task StartExam_And_CompleteExam_TransitionsAndEmitsOutbox()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var today = Common.ClinicTime.GetTodayQueueDate();
        var ticketId = Guid.NewGuid();

        var ticket = new QueueTicket
        {
            Id = ticketId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Called
        };
        db.QueueTickets.Add(ticket);
        await db.SaveChangesAsync();

        // 1. StartExam
        await manager.StartExamAsync(ticketId, doctorId, false);
        var inProgressTicket = await db.QueueTickets.FindAsync(ticketId);
        Assert.Equal(QueueStatus.InProgress, inProgressTicket!.Status);

        var examStartedOutbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == EventTypes.ExamStarted);
        Assert.NotNull(examStartedOutbox);
        Assert.Equal("queue.exam-started", examStartedOutbox.RoutingKey);
        var startedEnvelope = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope<ExamStarted>>(
            examStartedOutbox.Payload,
            EventJsonSerializerOptions.Default);
        Assert.NotNull(startedEnvelope);
        Assert.Equal(examStartedOutbox.EventId, startedEnvelope.EventId);
        Assert.Equal(examStartedOutbox.CorrelationId, startedEnvelope.CorrelationId);

        // 2. CompleteExam
        await manager.CompleteExamAsync(ticketId, doctorId, false);
        var completedTicket = await db.QueueTickets.FindAsync(ticketId);
        Assert.Equal(QueueStatus.Completed, completedTicket!.Status);

        var examCompletedOutbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == EventTypes.ExamCompleted);
        Assert.NotNull(examCompletedOutbox);
        Assert.Equal("queue.exam-completed", examCompletedOutbox.RoutingKey);
    }

    [Fact]
    public async Task StartExam_WithWrongDoctorId_AndNoAnyScope_ThrowsForbiddenException()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        db.QueueTickets.Add(new QueueTicket
        {
            Id = ticketId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = Common.ClinicTime.GetTodayQueueDate(),
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Called
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<QueueForbiddenException>(() =>
            manager.StartExamAsync(ticketId, otherDoctorId, hasAnyScope: false));
    }

    [Fact]
    public async Task Skip_WhenCalled_AutomaticallyCallsNextWaitingPatient()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var today = Common.ClinicTime.GetTodayQueueDate();
        var calledTicketId = Guid.NewGuid();
        var nextWaitingId = Guid.NewGuid();

        db.QueueTickets.Add(new QueueTicket
        {
            Id = calledTicketId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Called
        });

        db.QueueTickets.Add(new QueueTicket
        {
            Id = nextWaitingId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 2,
            Priority = false,
            Status = QueueStatus.Waiting
        });

        await db.SaveChangesAsync();

        var result = await manager.SkipAsync(calledTicketId);

        Assert.Equal("Skipped", result.SkippedTicket.Status);
        Assert.NotNull(result.NextCalledTicket);
        Assert.Equal(nextWaitingId, result.NextCalledTicket.Id);
        Assert.Equal("Called", result.NextCalledTicket.Status);

        var skippedOutbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == EventTypes.PatientSkipped);
        Assert.NotNull(skippedOutbox);

        var calledOutbox = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == EventTypes.PatientCalled);
        Assert.NotNull(calledOutbox);
    }

    [Fact]
    public async Task GetQueueStatusByPatient_CalculatesCorrectPositionAndEstimatedWaitTime()
    {
        using var db = CreateInMemoryDbContext();
        var manager = new QueueManager(db, NullLogger<QueueManager>.Instance);

        var doctorId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var today = Common.ClinicTime.GetTodayQueueDate();

        // 1 patient Called
        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 1,
            Priority = false,
            Status = QueueStatus.Called,
            DoctorName = "Dr. Strange"
        });

        // 1 patient Waiting ahead
        db.QueueTickets.Add(new QueueTicket
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 2,
            Priority = false,
            Status = QueueStatus.Waiting,
            DoctorName = "Dr. Strange"
        });

        // Our patient Waiting
        var myTicketId = Guid.NewGuid();
        db.QueueTickets.Add(new QueueTicket
        {
            Id = myTicketId,
            AppointmentId = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            QueueDate = today,
            QueueNumber = 3,
            Priority = false,
            Status = QueueStatus.Waiting,
            DoctorName = "Dr. Strange"
        });

        await db.SaveChangesAsync();

        var statusList = await manager.GetQueueStatusByPatientAsync(patientId, today);

        Assert.Single(statusList);
        var status = statusList[0];
        Assert.Equal(myTicketId, status.QueueTicketId);
        Assert.Equal(3, status.QueueNumber);
        Assert.Equal("Dr. Strange", status.DoctorName);
        Assert.Equal(3, status.PositionInQueue); // 2 ahead + 1 = 3
        Assert.Equal(20, status.EstimatedWaitMinutes); // 2 ahead * 10 = 20 mins
    }
}

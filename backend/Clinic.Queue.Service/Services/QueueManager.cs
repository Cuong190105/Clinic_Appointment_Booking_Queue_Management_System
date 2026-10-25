using System.Data;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.Queue.Service.Common;
using Clinic.Queue.Service.Common.Exceptions;
using Clinic.Queue.Service.DTOs;
using Clinic.Queue.Service.Entities;
using Clinic.Queue.Service.Enums;
using Clinic.Queue.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Queue.Service.Services;

public class QueueManager : IQueueManager
{
    private const string ConsumerName = "queue-service";
    private readonly QueueDbContext _dbContext;
    private readonly ILogger<QueueManager> _logger;

    public QueueManager(QueueDbContext dbContext, ILogger<QueueManager> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> GetNextQueueNumberAsync(Guid doctorId, DateOnly queueDate, CancellationToken ct = default)
    {
        if (_dbContext.Database.IsSqlServer())
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = @"
                MERGE INTO QueueCounters WITH (HOLDLOCK) AS target
                USING (SELECT @DoctorId AS DoctorId, @QueueDate AS QueueDate) AS source
                    ON target.DoctorId = source.DoctorId AND target.QueueDate = source.QueueDate
                WHEN MATCHED THEN
                    UPDATE SET LastNumber = target.LastNumber + 1, UpdatedAt = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (DoctorId, QueueDate, LastNumber, UpdatedAt)
                    VALUES (source.DoctorId, source.QueueDate, 1, SYSUTCDATETIME())
                OUTPUT INSERTED.LastNumber;";

            var pDoctorId = command.CreateParameter();
            pDoctorId.ParameterName = "@DoctorId";
            pDoctorId.Value = doctorId;
            command.Parameters.Add(pDoctorId);

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@QueueDate";
            pDate.Value = queueDate.ToDateTime(TimeOnly.MinValue);
            pDate.DbType = DbType.Date;
            command.Parameters.Add(pDate);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }

        // In-memory or fallback provider (for testing)
        var counter = await _dbContext.QueueCounters.FirstOrDefaultAsync(c => c.DoctorId == doctorId && c.QueueDate == queueDate, ct);
        if (counter == null)
        {
            counter = new QueueCounter
            {
                DoctorId = doctorId,
                QueueDate = queueDate,
                LastNumber = 1,
                UpdatedAt = DateTime.UtcNow
            };
            await _dbContext.QueueCounters.AddAsync(counter, ct);
        }
        else
        {
            counter.LastNumber++;
            counter.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return counter.LastNumber;
    }

    public async Task<QueueTicketDto?> ProcessAppointmentCheckedInAsync(EventEnvelope<AppointmentCheckedIn> envelope, CancellationToken ct = default)
    {
        if (envelope == null || envelope.Payload == null)
        {
            throw new QueueValidationException("Event envelope or payload cannot be null.");
        }

        var payload = envelope.Payload;
        if (payload.AppointmentId == Guid.Empty || payload.DoctorId == Guid.Empty || payload.PatientId == Guid.Empty)
        {
            throw new QueueValidationException("AppointmentId, DoctorId, and PatientId must be valid non-empty GUIDs.");
        }

        // Cancellation may be published before a delayed/retried check-in event. Keep a tombstone
        // so that redelivery cannot resurrect a cancelled appointment in the queue.
        if (await _dbContext.CancelledAppointments.AnyAsync(x => x.AppointmentId == payload.AppointmentId, ct))
        {
            var inbox = await _dbContext.InboxMessages.FirstOrDefaultAsync(
                m => m.EventId == envelope.EventId && m.ConsumerName == ConsumerName, ct);
            if (inbox is null)
            {
                _dbContext.InboxMessages.Add(CreateProcessedInboxMessage(envelope.EventId, envelope.EventType, envelope.SchemaVersion, envelope.CorrelationId));
            }
            else
            {
                inbox.Status = InboxMessageStatus.Processed;
                inbox.ProcessedAt = DateTimeOffset.UtcNow;
                inbox.LastError = null;
            }
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Ignoring delayed check-in for cancelled AppointmentId={AppointmentId}", payload.AppointmentId);
            return null;
        }

        // 1. Check InboxMessage by (ConsumerName, EventId) for exact duplicate delivery
        var existingInbox = await _dbContext.InboxMessages
            .FirstOrDefaultAsync(m => m.EventId == envelope.EventId && m.ConsumerName == ConsumerName, ct);

        if (existingInbox != null && existingInbox.Status == InboxMessageStatus.Processed)
        {
            _logger.LogInformation("Event {EventId} has already been processed by {Consumer}. Returning existing ticket.", envelope.EventId, ConsumerName);
            var existingTicket = await _dbContext.QueueTickets
                .FirstOrDefaultAsync(t => t.AppointmentId == payload.AppointmentId, ct);
            if (existingTicket != null)
            {
                return MapToDto(existingTicket);
            }
        }

        // 2. Idempotency by AppointmentId: if ticket already exists (even under different EventId), return it
        var existingTicketByAppt = await _dbContext.QueueTickets
            .FirstOrDefaultAsync(t => t.AppointmentId == payload.AppointmentId, ct);

        if (existingTicketByAppt != null)
        {
            _logger.LogInformation("Appointment {AppointmentId} already has QueueTicket {TicketId}. Skipping counter increment.", payload.AppointmentId, existingTicketByAppt.Id);
            if (existingInbox == null)
            {
                _dbContext.InboxMessages.Add(new InboxMessage
                {
                    EventId = envelope.EventId,
                    ConsumerName = ConsumerName,
                    EventType = envelope.EventType,
                    SchemaVersion = envelope.SchemaVersion,
                    CorrelationId = envelope.CorrelationId,
                    Status = InboxMessageStatus.Processed,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    ProcessedAt = DateTimeOffset.UtcNow
                });
                await _dbContext.SaveChangesAsync(ct);
            }
            return MapToDto(existingTicketByAppt);
        }

        // 3. Atomically allocate number, create ticket and record inbox message in local transaction
        var queueDate = ClinicTime.GetLocalQueueDate(payload.CheckedInAt);

        async Task<QueueTicketDto> CreateTicketAction()
        {
            var queueNumber = await GetNextQueueNumberAsync(payload.DoctorId, queueDate, ct);

            var ticket = new QueueTicket
            {
                Id = Guid.NewGuid(),
                AppointmentId = payload.AppointmentId,
                PatientId = payload.PatientId,
                DoctorId = payload.DoctorId,
                QueueDate = queueDate,
                QueueNumber = queueNumber,
                PatientName = payload.PatientName,
                DoctorName = payload.DoctorName,
                Priority = payload.Priority,
                Status = QueueStatus.Waiting,
                CheckInTime = payload.CheckedInAt.UtcDateTime,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.QueueTickets.Add(ticket);

            if (existingInbox == null)
            {
                _dbContext.InboxMessages.Add(new InboxMessage
                {
                    EventId = envelope.EventId,
                    ConsumerName = ConsumerName,
                    EventType = envelope.EventType,
                    SchemaVersion = envelope.SchemaVersion,
                    CorrelationId = envelope.CorrelationId,
                    Status = InboxMessageStatus.Processed,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    ProcessedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existingInbox.Status = InboxMessageStatus.Processed;
                existingInbox.ProcessedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            return MapToDto(ticket);
        }

        QueueTicketDto createdTicket;
        if (_dbContext.Database.IsSqlServer())
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            createdTicket = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                try
                {
                    var result = await CreateTicketAction();
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(ex, "DbUpdateException occurred during ticket creation. Checking for concurrent duplicate.");
                    var concurrentTicket = await _dbContext.QueueTickets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.AppointmentId == payload.AppointmentId, ct);
                    if (concurrentTicket != null)
                    {
                        return MapToDto(concurrentTicket);
                    }
                    throw;
                }
            });
        }
        else
        {
            try
            {
                createdTicket = await CreateTicketAction();
            }
            catch (DbUpdateException)
            {
                var concurrentTicket = await _dbContext.QueueTickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.AppointmentId == payload.AppointmentId, ct);
                if (concurrentTicket != null)
                {
                    return MapToDto(concurrentTicket);
                }
                throw;
            }
        }

        QueueTelemetry.TicketsCreated.Add(1);
        return createdTicket;
    }

    public async Task ProcessAppointmentCancelledAsync(EventEnvelope<AppointmentCancelled> envelope, CancellationToken ct = default)
    {
        if (envelope?.Payload is null || envelope.Payload.AppointmentId == Guid.Empty)
            throw new QueueValidationException("AppointmentCancelled must contain a valid AppointmentId.");

        var payload = envelope.Payload;
        if (payload.IsRescheduleRelease)
        {
            var releaseInbox = await _dbContext.InboxMessages.FirstOrDefaultAsync(
                m => m.EventId == envelope.EventId && m.ConsumerName == ConsumerName, ct);
            if (releaseInbox is null)
                _dbContext.InboxMessages.Add(CreateProcessedInboxMessage(envelope.EventId, envelope.EventType, envelope.SchemaVersion, envelope.CorrelationId));
            else
            {
                releaseInbox.Status = InboxMessageStatus.Processed;
                releaseInbox.ProcessedAt = DateTimeOffset.UtcNow;
                releaseInbox.LastError = null;
            }
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        if (await _dbContext.InboxMessages.AnyAsync(m => m.EventId == envelope.EventId && m.ConsumerName == ConsumerName && m.Status == InboxMessageStatus.Processed, ct))
            return;

        async Task ProcessAsync()
        {
            var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.AppointmentId == payload.AppointmentId, ct);
            if (ticket is not null && (ticket.Status == QueueStatus.Waiting || ticket.Status == QueueStatus.Called))
            {
                ticket.Cancel();
            }
            else if (ticket is not null)
            {
                _logger.LogWarning("Appointment {AppointmentId} was cancelled while its queue ticket is {Status}; preserving the in-progress/completed queue state.", payload.AppointmentId, ticket.Status);
            }

            if (!await _dbContext.CancelledAppointments.AnyAsync(x => x.AppointmentId == payload.AppointmentId, ct))
            {
                _dbContext.CancelledAppointments.Add(new CancelledAppointment
                {
                    AppointmentId = payload.AppointmentId,
                    CancelledAt = payload.CancelledAt
                });
            }

            var inbox = await _dbContext.InboxMessages.FirstOrDefaultAsync(
                m => m.EventId == envelope.EventId && m.ConsumerName == ConsumerName, ct);
            if (inbox is null)
            {
                _dbContext.InboxMessages.Add(CreateProcessedInboxMessage(envelope.EventId, envelope.EventType, envelope.SchemaVersion, envelope.CorrelationId));
            }
            else
            {
                inbox.Status = InboxMessageStatus.Processed;
                inbox.ProcessedAt = DateTimeOffset.UtcNow;
                inbox.LastError = null;
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        if (_dbContext.Database.IsSqlServer())
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                await ProcessAsync();
                await transaction.CommitAsync(ct);
            });
        }
        else
        {
            await ProcessAsync();
        }
    }

    private static InboxMessage CreateProcessedInboxMessage(Guid eventId, string eventType, int schemaVersion, Guid correlationId) => new()
    {
        EventId = eventId,
        ConsumerName = ConsumerName,
        EventType = eventType,
        SchemaVersion = schemaVersion,
        CorrelationId = correlationId,
        Status = InboxMessageStatus.Processed,
        ReceivedAt = DateTimeOffset.UtcNow,
        ProcessedAt = DateTimeOffset.UtcNow
    };

    public async Task<QueueTicketDto> CallNextAsync(Guid doctorId, DateOnly? date = null, CancellationToken ct = default)
    {
        var queueDate = date ?? ClinicTime.GetTodayQueueDate();

        var nextTicket = await _dbContext.QueueTickets
            .Where(t => t.DoctorId == doctorId
                        && t.QueueDate == queueDate
                        && !t.IsDeleted
                        && t.Status == QueueStatus.Waiting)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.QueueNumber)
            .FirstOrDefaultAsync(ct);

        if (nextTicket == null)
        {
            throw new QueueNotFoundException("There are no patients waiting in the queue for this doctor.");
        }

        QueueTelemetry.WaitTime.Record(Math.Max(0, (DateTime.UtcNow - nextTicket.CheckInTime).TotalSeconds));

        nextTicket.Call();

        AddOutboxMessage(
            new PatientCalled(nextTicket.Id, nextTicket.AppointmentId, nextTicket.DoctorId, nextTicket.QueueNumber, DateTimeOffset.UtcNow),
            EventTypes.PatientCalled,
            "queue.patient-called",
            Guid.NewGuid());

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(nextTicket);
    }

    public async Task<QueueTicketDto> CallAsync(Guid queueTicketId, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");
        if (ticket.Status != QueueStatus.Waiting)
            throw new InvalidOperationException($"Cannot call patient from status '{ticket.Status}'. Must be Waiting.");

        ticket.Call();
        AddOutboxMessage(
            new PatientCalled(ticket.Id, ticket.AppointmentId, ticket.DoctorId, ticket.QueueNumber, DateTimeOffset.UtcNow),
            EventTypes.PatientCalled,
            "queue.patient-called",
            Guid.NewGuid());
        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(ticket);
    }

    public async Task StartExamAsync(Guid queueTicketId, Guid? actorDoctorId, bool hasAnyScope, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");

        if (!hasAnyScope && (!actorDoctorId.HasValue || actorDoctorId.Value == Guid.Empty || ticket.DoctorId != actorDoctorId.Value))
        {
            throw new QueueForbiddenException("You are not allowed to start exam for another doctor's queue.");
        }

        ticket.StartExam();

        AddOutboxMessage(
            new ExamStarted(ticket.Id, ticket.AppointmentId, ticket.DoctorId, DateTimeOffset.UtcNow),
            EventTypes.ExamStarted,
            "queue.exam-started",
            Guid.NewGuid());

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task CompleteExamAsync(Guid queueTicketId, Guid? actorDoctorId, bool hasAnyScope, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");

        if (!hasAnyScope && (!actorDoctorId.HasValue || actorDoctorId.Value == Guid.Empty || ticket.DoctorId != actorDoctorId.Value))
        {
            throw new QueueForbiddenException("You are not allowed to complete exam for another doctor's queue.");
        }

        ticket.CompleteExam();

        AddOutboxMessage(
            new ExamCompleted(ticket.Id, ticket.AppointmentId, ticket.DoctorId, DateTimeOffset.UtcNow),
            EventTypes.ExamCompleted,
            "queue.exam-completed",
            Guid.NewGuid());

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<SkipQueueResultDto> SkipAsync(Guid queueTicketId, QueueStatus? requiredStatus = null, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");

        if (requiredStatus.HasValue && ticket.Status != requiredStatus.Value)
            throw new InvalidOperationException($"Cannot skip patient from status '{ticket.Status}'. Must be '{requiredStatus.Value}'.");
        ticket.Skip();

        AddOutboxMessage(
            new PatientSkipped(ticket.Id, ticket.AppointmentId, ticket.DoctorId, DateTimeOffset.UtcNow),
            EventTypes.PatientSkipped,
            "queue.patient-skipped",
            Guid.NewGuid());

        await _dbContext.SaveChangesAsync(ct);
        return new SkipQueueResultDto(MapToDto(ticket), null);
    }

    public async Task<QueueTicketDto> RetrySkippedAsync(Guid queueTicketId, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");
        ticket.Retry();
        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(ticket);
    }

    public async Task SetPriorityAsync(Guid queueTicketId, bool priority, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets.FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct)
            ?? throw new QueueNotFoundException($"Queue ticket '{queueTicketId}' not found.");

        if (ticket.Status != QueueStatus.Waiting)
            throw new InvalidOperationException("Priority can only be changed before the ticket is called.");
        ticket.SetPriority(priority);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<QueueTicketDto>> GetQueueByDoctorAsync(Guid doctorId, DateOnly? date = null, CancellationToken ct = default)
    {
        var queueDate = date ?? ClinicTime.GetTodayQueueDate();

        var tickets = await _dbContext.QueueTickets
            .Where(t => t.DoctorId == doctorId && t.QueueDate == queueDate && !t.IsDeleted)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.QueueNumber)
            .ToListAsync(ct);

        return tickets.Select(MapToDto).ToList();
    }

    public async Task<List<MyQueueStatusDto>> GetQueueStatusByPatientAsync(Guid patientId, DateOnly? date = null, CancellationToken ct = default)
    {
        var queueDate = date ?? ClinicTime.GetTodayQueueDate();

        var patientTickets = await _dbContext.QueueTickets
            .Where(t => t.PatientId == patientId
                        && t.QueueDate == queueDate
                        && !t.IsDeleted
                        && (t.Status == QueueStatus.Waiting || t.Status == QueueStatus.Called || t.Status == QueueStatus.InProgress))
            .OrderBy(t => t.CheckInTime)
            .ToListAsync(ct);

        if (patientTickets.Count == 0) return [];

        var result = new List<MyQueueStatusDto>(patientTickets.Count);
        var doctorQueues = new Dictionary<Guid, List<QueueTicket>>();

        foreach (var pt in patientTickets)
        {
            if (!doctorQueues.TryGetValue(pt.DoctorId, out var docQueue))
            {
                docQueue = await _dbContext.QueueTickets
                    .Where(t => t.DoctorId == pt.DoctorId && t.QueueDate == queueDate && !t.IsDeleted)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.QueueNumber)
                    .ToListAsync(ct);
                doctorQueues[pt.DoctorId] = docQueue;
            }

            var waitingAhead = 0;
            foreach (var ticket in docQueue)
            {
                if (ticket.Id == pt.Id) break;
                if (ticket.Status == QueueStatus.Waiting || ticket.Status == QueueStatus.Called)
                {
                    waitingAhead++;
                }
            }

            result.Add(new MyQueueStatusDto
            {
                QueueTicketId = pt.Id,
                AppointmentId = pt.AppointmentId,
                QueueNumber = pt.QueueNumber,
                Status = pt.Status.ToString(),
                DoctorName = pt.DoctorName ?? string.Empty,
                PositionInQueue = waitingAhead + 1,
                EstimatedWaitMinutes = waitingAhead * 10
            });
        }

        return result;
    }

    public async Task<QueueTicketDto?> GetByIdAsync(Guid queueTicketId, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets
            .FirstOrDefaultAsync(t => t.Id == queueTicketId && !t.IsDeleted, ct);

        return ticket != null ? MapToDto(ticket) : null;
    }

    public async Task<QueueTicketDto?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var ticket = await _dbContext.QueueTickets
            .FirstOrDefaultAsync(t => t.AppointmentId == appointmentId && !t.IsDeleted, ct);

        return ticket != null ? MapToDto(ticket) : null;
    }

    private void AddOutboxMessage<T>(T payload, string eventType, string routingKey, Guid correlationId, Guid? causationId = null)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var corrId = correlationId != Guid.Empty ? correlationId : Guid.NewGuid();

        var envelope = new EventEnvelope<T>
        {
            EventId = eventId,
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = occurredAt,
            CorrelationId = corrId,
            CausationId = causationId,
            Producer = "Clinic.Queue.Service",
            Payload = payload
        };

        var outbox = new OutboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = occurredAt,
            CorrelationId = corrId,
            CausationId = causationId,
            Producer = "Clinic.Queue.Service",
            Payload = JsonSerializer.Serialize(envelope),
            RoutingKey = routingKey,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = occurredAt
        };
        _dbContext.OutboxMessages.Add(outbox);
    }

    private static QueueTicketDto MapToDto(QueueTicket ticket) => new()
    {
        Id = ticket.Id,
        AppointmentId = ticket.AppointmentId,
        PatientId = ticket.PatientId,
        DoctorId = ticket.DoctorId,
        QueueDate = ticket.QueueDate,
        QueueNumber = ticket.QueueNumber,
        Priority = ticket.Priority,
        Status = ticket.Status.ToString(),
        CheckInTime = ticket.CheckInTime,
        CalledAt = ticket.CalledAt,
        ExamStartedAt = ticket.ExamStartedAt,
        CompletedAt = ticket.CompletedAt,
        SkippedAt = ticket.SkippedAt,
        PatientName = ticket.PatientName,
        DoctorName = ticket.DoctorName
    };
}

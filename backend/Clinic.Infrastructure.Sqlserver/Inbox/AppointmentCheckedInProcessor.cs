using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Inbox;

public enum AppointmentCheckedInProcessingResult
{
    Processed = 0,
    AlreadyProcessed = 1,
    CurrentlyProcessing = 2
}

public sealed class AppointmentCheckedInProcessor
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IInboxStore _inboxStore;
    private readonly string _consumerName;

    public AppointmentCheckedInProcessor(
        ApplicationDbContext dbContext,
        IInboxStore inboxStore,
        string consumerName)
    {
        _dbContext = dbContext;
        _inboxStore = inboxStore;
        _consumerName = consumerName;
    }

    public async Task<AppointmentCheckedInProcessingResult> ProcessAsync(
        EventEnvelope<AppointmentCheckedIn> envelope,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var decision = await _inboxStore.TryBeginProcessingAsync(
            envelope,
            _consumerName,
            cancellationToken);

        if (decision == InboxDecision.AlreadyProcessed)
        {
            await transaction.CommitAsync(cancellationToken);
            return AppointmentCheckedInProcessingResult.AlreadyProcessed;
        }

        if (decision == InboxDecision.CurrentlyProcessing)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentCheckedInProcessingResult.CurrentlyProcessing;
        }

        var appointment = await _dbContext.Appointments
            .Include(x => x.QueueTicket)
            .SingleOrDefaultAsync(
                x => x.Id == envelope.Payload.AppointmentId && !x.IsDeleted,
                cancellationToken);

        if (appointment is null)
        {
            throw new InvalidOperationException(
                $"Appointment '{envelope.Payload.AppointmentId}' was not found.");
        }

        if (appointment.PatientId != envelope.Payload.PatientId ||
            appointment.DoctorId != envelope.Payload.DoctorId)
        {
            throw new InvalidOperationException(
                $"AppointmentCheckedIn payload does not match appointment '{appointment.Id}'.");
        }

        // The current monolith creates QueueTicket in CheckInHandler before publishing
        // AppointmentCheckedIn. This guard makes the transitional consumer idempotent.
        if (appointment.QueueTicket is null)
        {
            throw new InvalidOperationException(
                $"Appointment '{appointment.Id}' has no QueueTicket for a check-in event.");
        }

        _inboxStore.MarkProcessed(
            envelope.EventId,
            _consumerName,
            DateTimeOffset.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AppointmentCheckedInProcessingResult.Processed;
    }
}

namespace Clinic.Appointment.Service.Services;

using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;
using Clinic.Contracts.Events;
using Microsoft.EntityFrameworkCore;

public sealed class AppointmentConfirmationWorker : BackgroundService
{
    private const int MaxAttempts = 4;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentConfirmationWorker> _logger;

    public AppointmentConfirmationWorker(
        IServiceProvider serviceProvider,
        ILogger<AppointmentConfirmationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentConfirmationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAppointmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Appointment confirmation cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("AppointmentConfirmationWorker stopped.");
    }

    private async Task ProcessPendingAppointmentsAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        var scheduling = scope.ServiceProvider.GetRequiredService<ISchedulingClient>();
        var outbox = scope.ServiceProvider.GetRequiredService<IAppointmentOutboxWriter>();

        var now = DateTime.UtcNow;
        var appointments = await db.Appointments
            .Where(a => !a.IsWalkIn
                && a.Status == AppointmentStatus.Pending
                && a.SchedulingReservationId.HasValue
                && (!a.NextConfirmationAttemptAt.HasValue || a.NextConfirmationAttemptAt <= now))
            .OrderBy(a => a.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var appointment in appointments)
        {
            await ConfirmAppointmentAsync(db, scheduling, outbox, appointment, ct);
        }
    }

    private async Task ConfirmAppointmentAsync(
        AppointmentDbContext db,
        ISchedulingClient scheduling,
        IAppointmentOutboxWriter outbox,
        Clinic.Appointment.Service.Entities.Appointment appointment,
        CancellationToken ct)
    {
        var reservationId = appointment.SchedulingReservationId!.Value;
        bool confirmed;
        try
        {
            confirmed = await scheduling.ConfirmReservationAsync(appointment.WorkScheduleId, reservationId, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Scheduling confirmation request failed for appointment {AppointmentId}.", appointment.Id);
            confirmed = false;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Scheduling confirmation timed out for appointment {AppointmentId}.", appointment.Id);
            confirmed = false;
        }

        if (confirmed)
        {
            appointment.Confirm(appointment.UpdatedByUserId);
            outbox.Add(new EventEnvelope<AppointmentConfirmed>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentConfirmed,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentConfirmed(
                    appointment.Id,
                    appointment.PatientId,
                    appointment.DoctorId,
                    appointment.WorkScheduleId,
                    appointment.ScheduleDate,
                    appointment.TimeSlot,
                    appointment.UpdatedByUserId,
                    DateTimeOffset.UtcNow,
                    PatientName: appointment.PatientName,
                    PatientEmail: null,
                    PatientPhone: appointment.PatientPhone,
                    IdentityUserId: appointment.PatientIdentityUserId)
            }, "appointment.confirmed");

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Confirmed appointment {AppointmentId} after {Attempts} attempt(s).",
                appointment.Id, appointment.ConfirmationAttempts + 1);
            return;
        }

        appointment.ConfirmationAttempts++;
        if (appointment.ConfirmationAttempts >= MaxAttempts)
        {
            try
            {
                await scheduling.ReleaseReservationAsync(appointment.WorkScheduleId, reservationId, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to release reservation {ReservationId} after confirmation failure for appointment {AppointmentId}.",
                    reservationId, appointment.Id);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogError("Timed out releasing reservation {ReservationId} after confirmation failure for appointment {AppointmentId}.",
                    reservationId, appointment.Id);
            }

            appointment.Cancel(appointment.UpdatedByUserId, isAdminOrReceptionist: true);
            appointment.NextConfirmationAttemptAt = null;
            await db.SaveChangesAsync(ct);
            _logger.LogError("Unable to confirm appointment {AppointmentId}; reservation released after {Attempts} attempts.",
                appointment.Id, appointment.ConfirmationAttempts);
            return;
        }

        appointment.NextConfirmationAttemptAt = DateTime.UtcNow + GetRetryDelay(appointment.ConfirmationAttempts);
        await db.SaveChangesAsync(ct);
        _logger.LogWarning("Scheduling confirmation failed for appointment {AppointmentId}; retry {Attempt} scheduled at {NextAttemptAt}.",
            appointment.Id, appointment.ConfirmationAttempts, appointment.NextConfirmationAttemptAt);
    }

    private static TimeSpan GetRetryDelay(int attempt) => attempt switch
    {
        1 => TimeSpan.FromSeconds(10),
        2 => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromMinutes(5)
    };
}

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
    private readonly IConfiguration? _configuration;
    private readonly IAppointmentWorkSignal? _workSignal;

    public AppointmentConfirmationWorker(
        IServiceProvider serviceProvider,
        ILogger<AppointmentConfirmationWorker> logger,
        IConfiguration? configuration = null,
        IAppointmentWorkSignal? workSignal = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _workSignal = workSignal;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentConfirmationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessPendingAppointmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Appointment confirmation cycle failed.");
            }

            if (processed > 0)
            {
                continue;
            }

            try
            {
                var rawDelayMs = _configuration?.GetValue<int?>("Outbox:ConfirmationIntervalMs") ?? 1000;
                var delayMs = Math.Clamp(rawDelayMs, 100, 60_000);
                var delay = TimeSpan.FromMilliseconds(delayMs);

                if (_workSignal != null)
                {
                    await _workSignal.WaitForConfirmationAsync(delay, stoppingToken);
                }
                else
                {
                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("AppointmentConfirmationWorker stopped.");
    }

    private async Task<int> ProcessPendingAppointmentsAsync(CancellationToken ct)
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

        if (appointments.Count == 0)
        {
            return 0;
        }

        // Confirm reservations concurrently over HTTP to avoid serializing N round-trips
        var confirmationTasks = appointments.Select(async appointment =>
        {
            var reservationId = appointment.SchedulingReservationId!.Value;
            try
            {
                var ok = await scheduling.ConfirmReservationAsync(appointment.WorkScheduleId, reservationId, ct);
                return (appointment, confirmed: ok);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Scheduling confirmation request failed for appointment {AppointmentId}.", appointment.Id);
                return (appointment, confirmed: false);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Scheduling confirmation timed out for appointment {AppointmentId}.", appointment.Id);
                return (appointment, confirmed: false);
            }
        }).ToList();

        var results = await Task.WhenAll(confirmationTasks);
        var anyConfirmed = false;

        foreach (var (appointment, confirmed) in results)
        {
            var reservationId = appointment.SchedulingReservationId!.Value;
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

                anyConfirmed = true;
                _logger.LogInformation("Confirmed appointment {AppointmentId} after {Attempts} attempt(s).",
                    appointment.Id, appointment.ConfirmationAttempts + 1);
                continue;
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
                _logger.LogError("Unable to confirm appointment {AppointmentId}; reservation released after {Attempts} attempts.",
                    appointment.Id, appointment.ConfirmationAttempts);
                continue;
            }

            appointment.NextConfirmationAttemptAt = DateTime.UtcNow + GetRetryDelay(appointment.ConfirmationAttempts);
            _logger.LogWarning("Scheduling confirmation failed for appointment {AppointmentId}; retry {Attempt} scheduled at {NextAttemptAt}.",
                appointment.Id, appointment.ConfirmationAttempts, appointment.NextConfirmationAttemptAt);
        }

        // Single batched database commit for all processed appointments and outbox events
        await db.SaveChangesAsync(ct);

        if (anyConfirmed)
        {
            _workSignal?.SignalOutbox();
        }

        return appointments.Count;
    }

    private static TimeSpan GetRetryDelay(int attempt) => attempt switch
    {
        1 => TimeSpan.FromSeconds(10),
        2 => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromMinutes(5)
    };
}

namespace Clinic.Appointment.Service.Services;

using Clinic.Appointment.Service.DTOs;
using Clinic.Appointment.Service.Entities;
using Clinic.Appointment.Service.Enums;
using Clinic.Appointment.Service.Persistence;
using Clinic.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public interface IAppointmentManager
{
    Task<(bool Success, string? ErrorMessage, AppointmentDto? Result, int StatusCode)> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        Guid currentUserId,
        Guid? tokenPatientId,
        bool isStaff,
        string? idempotencyKey,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, AppointmentDetailDto? Result, int StatusCode)> GetAppointmentByIdAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        Guid? tokenPatientId,
        Guid? tokenDoctorId,
        CancellationToken ct = default);

    Task<List<AppointmentDto>> GetMyAppointmentsAsync(
        Guid patientId,
        string? category,
        CancellationToken ct = default);

    Task<List<AppointmentDto>> GetPendingAppointmentsAsync(CancellationToken ct = default);

    Task<List<AppointmentDto>> GetAppointmentsByDateAsync(DateOnly date, CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, AppointmentChangelogDto? Result, int StatusCode)> GetAppointmentChangelogAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, int StatusCode)> ConfirmAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        string? updaterName,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, int StatusCode)> CancelAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        bool isAdminOrReceptionist,
        string? updaterName,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, AppointmentDto? Result, int StatusCode)> RescheduleAppointmentAsync(
        Guid appointmentId,
        UpdateAppointmentRequest request,
        Guid currentUserId,
        string? updaterName,
        string? idempotencyKey,
        CancellationToken ct = default);

    Task<(bool Success, string? ErrorMessage, int StatusCode)> CheckInAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        string? idempotencyKey,
        bool isUrgent = false,
        CancellationToken ct = default);

    Task<TotalAppointmentSummaryDto> GetSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        Guid? doctorId = null,
        Guid? specialtyId = null,
        CancellationToken ct = default);

    Task<PagedAppointmentsDto> GetPagedAppointmentsByDateAsync(
        DateOnly date,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<bool> SyncWalkInAppointmentAsync(SyncWalkInAppointmentRequest request, CancellationToken ct = default);

    Task<bool> ProcessExamCompletedAsync(ExamCompleted evt, CancellationToken ct = default);

    Task<bool> ProcessUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default);
}

public class AppointmentManager : IAppointmentManager
{
    private readonly AppointmentDbContext _db;
    private readonly ISchedulingClient _schedulingClient;
    private readonly IAppointmentOutboxWriter _outbox;
    private readonly ILogger<AppointmentManager> _logger;

    public AppointmentManager(
        AppointmentDbContext db,
        ISchedulingClient schedulingClient,
        IAppointmentOutboxWriter outbox,
        ILogger<AppointmentManager> logger)
    {
        _db = db;
        _schedulingClient = schedulingClient;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, AppointmentDto? Result, int StatusCode)> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        Guid currentUserId,
        Guid? tokenPatientId,
        bool isStaff,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        // 0. Idempotency Check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingApp = await _db.Appointments
                .FirstOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey && !a.IsDeleted, ct);
            if (existingApp != null)
            {
                _logger.LogInformation("Idempotent retry detected for key '{IdempotencyKey}', returning existing appointment {AppointmentId}.",
                    idempotencyKey, existingApp.Id);
                return (true, null, ToDto(existingApp), StatusCodes.Status200OK);
            }
        }

        // 1. Resolve Patient
        Guid patientId;
        string patientName = request.PatientName ?? string.Empty;
        string? patientPhone = request.PatientPhone;
        string? patientGender = request.PatientGender;
        DateOnly? patientDob = request.PatientDateOfBirth;

        if (isStaff && request.PatientId.HasValue && request.PatientId.Value != Guid.Empty)
        {
            patientId = request.PatientId.Value;
            var proj = await _db.PatientProjections.FirstOrDefaultAsync(p => p.Id == patientId, ct);
            if (proj != null)
            {
                patientName = string.IsNullOrEmpty(patientName) ? proj.FullName : patientName;
                patientPhone = patientPhone ?? proj.PhoneNumber;
                patientGender = patientGender ?? proj.Gender;
                patientDob = patientDob ?? proj.DateOfBirth;
            }
        }
        else
        {
            if (tokenPatientId.HasValue && tokenPatientId.Value != Guid.Empty)
            {
                patientId = tokenPatientId.Value;
            }
            else
            {
                var proj = await _db.PatientProjections.FirstOrDefaultAsync(p => p.IdentityUserId == currentUserId, ct);
                if (proj == null)
                {
                    return (false, "Patient profile not found for the current user.", null, StatusCodes.Status404NotFound);
                }
                patientId = proj.Id;
                patientName = proj.FullName;
                patientPhone = proj.PhoneNumber;
                patientGender = proj.Gender;
                patientDob = proj.DateOfBirth;
            }

            if (string.IsNullOrEmpty(patientName))
            {
                var proj = await _db.PatientProjections.FirstOrDefaultAsync(p => p.Id == patientId, ct);
                if (proj != null)
                {
                    patientName = proj.FullName;
                    patientPhone = proj.PhoneNumber;
                    patientGender = proj.Gender;
                    patientDob = proj.DateOfBirth;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(patientName))
        {
            patientName = "Patient " + patientId.ToString()[..8];
        }

        // 2. Booking Workflow
        if (!request.IsWalkIn)
        {
            // CASE 1: Online Booking (SAGA Orchestration)
            var key = idempotencyKey ?? $"booking-{patientId}-{request.WorkScheduleId}-{request.TimeSlot}";
            var reservation = await _schedulingClient.ReserveSlotAsync(
                request.WorkScheduleId,
                request.TimeSlot,
                patientId,
                key,
                ct);

            if (reservation == null)
            {
                return (false, "Could not reserve slot. The slot may be full or already booked.", null, StatusCodes.Status409Conflict);
            }

            var appointment = new Appointment(
                patientId,
                patientName,
                patientPhone,
                patientGender,
                patientDob,
                request.WorkScheduleId,
                reservation.DoctorId,
                reservation.DoctorName ?? string.Empty,
                reservation.SpecialtyName,
                reservation.ScheduleDate,
                request.TimeSlot,
                request.Reason,
                currentUserId,
                false)
            {
                IdempotencyKey = idempotencyKey
            };

            await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                _db.Appointments.Add(appointment);
                await _db.SaveChangesAsync(ct);

                // Confirm reservation with Scheduling Service
                var confirmed = await _schedulingClient.ConfirmReservationAsync(request.WorkScheduleId, reservation.Id, ct);
                if (!confirmed)
                {
                    // Compensation: release slot
                    await _schedulingClient.ReleaseReservationAsync(request.WorkScheduleId, reservation.Id, ct);
                    if (tx != null) await tx.RollbackAsync(ct);
                    return (false, "Failed to confirm reservation with Scheduling service.", null, StatusCodes.Status409Conflict);
                }

                appointment.Confirm(currentUserId);

                // Outbox Events
                _outbox.Add(new EventEnvelope<AppointmentCreated>
                {
                    EventId = Guid.NewGuid(),
                    EventType = EventTypes.AppointmentCreated,
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid(),
                    Producer = "Clinic.Appointment.Service",
                    Payload = new AppointmentCreated(
                        appointment.Id,
                        appointment.PatientId,
                        appointment.DoctorId,
                        appointment.WorkScheduleId,
                        appointment.ScheduleDate,
                        appointment.TimeSlot,
                        appointment.Reason,
                        false,
                        DateTimeOffset.UtcNow)
                }, "appointment.created");

                _outbox.Add(new EventEnvelope<AppointmentConfirmed>
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
                        currentUserId,
                        DateTimeOffset.UtcNow)
                }, "appointment.confirmed");

                await _db.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                return (true, null, ToDto(appointment), StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                // Compensation
                await _schedulingClient.ReleaseReservationAsync(request.WorkScheduleId, reservation.Id, ct);
                _logger.LogError(ex, "Failed to create appointment for patient {PatientId}", patientId);
                return (false, "Internal error saving appointment.", null, StatusCodes.Status500InternalServerError);
            }
        }
        else
        {
            // CASE 2: Walk-In Reception (Bypasses capacity check, immediately confirmed)
            var scheduleInfo = await _schedulingClient.GetScheduleAsync(request.WorkScheduleId, ct);
            if (scheduleInfo == null)
            {
                return (false, "Work schedule not found in Scheduling service.", null, StatusCodes.Status404NotFound);
            }

            var appointment = new Appointment(
                patientId,
                patientName,
                patientPhone,
                patientGender,
                patientDob,
                request.WorkScheduleId,
                scheduleInfo.DoctorId,
                scheduleInfo.DoctorName,
                scheduleInfo.SpecialtyName,
                scheduleInfo.Date,
                request.TimeSlot,
                request.Reason,
                currentUserId,
                true)
            {
                IdempotencyKey = idempotencyKey
            };

            _db.Appointments.Add(appointment);

            _outbox.Add(new EventEnvelope<AppointmentCreated>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentCreated,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentCreated(
                    appointment.Id,
                    appointment.PatientId,
                    appointment.DoctorId,
                    appointment.WorkScheduleId,
                    appointment.ScheduleDate,
                    appointment.TimeSlot,
                    appointment.Reason,
                    true,
                    DateTimeOffset.UtcNow)
            }, "appointment.created");

            await _db.SaveChangesAsync(ct);
            return (true, null, ToDto(appointment), StatusCodes.Status201Created);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, AppointmentDetailDto? Result, int StatusCode)> GetAppointmentByIdAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        Guid? tokenPatientId,
        Guid? tokenDoctorId,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        if (app == null)
        {
            return (false, "Appointment not found.", null, StatusCodes.Status404NotFound);
        }

        var rolesList = roles.ToList();
        bool isStaff = rolesList.Contains("Receptionist") || rolesList.Contains("Admin");
        bool isOwnerPatient = rolesList.Contains("Patient") && tokenPatientId == app.PatientId;
        bool isAssignedDoctor = rolesList.Contains("Doctor") && tokenDoctorId == app.DoctorId;

        if (!isStaff && !isOwnerPatient && !isAssignedDoctor)
        {
            return (false, "Forbidden.", null, StatusCodes.Status403Forbidden);
        }

        return (true, null, ToDetailDto(app), StatusCodes.Status200OK);
    }

    public async Task<List<AppointmentDto>> GetMyAppointmentsAsync(
        Guid patientId,
        string? category,
        CancellationToken ct = default)
    {
        var query = _db.Appointments.Where(a => a.PatientId == patientId && !a.IsDeleted);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (string.Equals(category, "upcoming", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.ScheduleDate >= today && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed);
        }
        else if (string.Equals(category, "past", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.ScheduleDate < today || a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled);
        }

        var list = await query.OrderByDescending(a => a.ScheduleDate).ThenByDescending(a => a.TimeSlot).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<List<AppointmentDto>> GetPendingAppointmentsAsync(CancellationToken ct = default)
    {
        var list = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Pending && !a.IsDeleted)
            .OrderBy(a => a.ScheduleDate)
            .ThenBy(a => a.TimeSlot)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<List<AppointmentDto>> GetAppointmentsByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var list = await _db.Appointments
            .Where(a => a.ScheduleDate == date && !a.IsDeleted)
            .OrderBy(a => a.TimeSlot)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<(bool Success, string? ErrorMessage, AppointmentChangelogDto? Result, int StatusCode)> GetAppointmentChangelogAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments
            .Include(a => a.Snapshots)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);

        if (app == null)
        {
            return (false, "Appointment not found.", null, StatusCodes.Status404NotFound);
        }

        var changelog = new List<AppointmentHistoryDto>();

        // Current state as first entry
        changelog.Add(new AppointmentHistoryDto(
            app.DoctorId,
            app.DoctorName,
            app.SpecialtyName,
            app.TimeSlot,
            app.ScheduleDate,
            app.Reason,
            app.Status,
            app.UpdatedAt ?? app.CreatedAt,
            app.UpdatedByUserId,
            "Current State"
        ));

        // Snapshots in descending order
        foreach (var s in app.Snapshots.OrderByDescending(x => x.UpdatedAt))
        {
            changelog.Add(new AppointmentHistoryDto(
                s.OldDoctorId,
                s.OldDoctorName,
                s.OldSpecialtyName,
                s.TimeSlot,
                s.OldScheduleDate,
                s.Reason,
                s.Status,
                s.UpdatedAt,
                s.UpdatedByUserId,
                s.UpdatedByUserName ?? "Staff"
            ));
        }

        return (true, null, new AppointmentChangelogDto(app.Id, app.PatientId, app.PatientName, changelog), StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? ErrorMessage, int StatusCode)> ConfirmAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        string? updaterName,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);
        if (app == null) return (false, "Appointment not found.", StatusCodes.Status404NotFound);

        try
        {
            app.Confirm(currentUserId, updaterName);

            _outbox.Add(new EventEnvelope<AppointmentConfirmed>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentConfirmed,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentConfirmed(
                    app.Id,
                    app.PatientId,
                    app.DoctorId,
                    app.WorkScheduleId,
                    app.ScheduleDate,
                    app.TimeSlot,
                    currentUserId,
                    DateTimeOffset.UtcNow)
            }, "appointment.confirmed");

            await _db.SaveChangesAsync(ct);
            return (true, null, StatusCodes.Status204NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, int StatusCode)> CancelAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        bool isAdminOrReceptionist,
        string? updaterName,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);
        if (app == null) return (false, "Appointment not found.", StatusCodes.Status404NotFound);

        try
        {
            app.Cancel(currentUserId, isAdminOrReceptionist, updaterName);

            _outbox.Add(new EventEnvelope<AppointmentCancelled>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentCancelled,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentCancelled(
                    app.Id,
                    app.PatientId,
                    currentUserId,
                    DateTimeOffset.UtcNow,
                    app.WorkScheduleId,
                    app.TimeSlot)
            }, "appointment.cancelled");

            await _db.SaveChangesAsync(ct);

            // Best-effort direct release on Scheduling Service alongside Outbox event
            _ = await _schedulingClient.ReleaseSlotByAppointmentAsync(
                app.Id,
                app.WorkScheduleId,
                app.TimeSlot,
                app.PatientId,
                currentUserId,
                ct);

            return (true, null, StatusCodes.Status204NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, AppointmentDto? Result, int StatusCode)> RescheduleAppointmentAsync(
        Guid appointmentId,
        UpdateAppointmentRequest request,
        Guid currentUserId,
        string? updaterName,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);
        if (app == null) return (false, "Appointment not found.", null, StatusCodes.Status404NotFound);

        if (app.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
        {
            return (false, $"{app.Status} appointments cannot be rescheduled.", null, StatusCodes.Status400BadRequest);
        }

        // Reschedule Saga:
        // 1. Reserve slot at new work schedule
        var key = idempotencyKey ?? $"reschedule-{app.Id}-{request.NewWorkScheduleId}-{request.TimeSlot}";
        var newReservation = await _schedulingClient.ReserveSlotAsync(
            request.NewWorkScheduleId,
            request.TimeSlot,
            app.PatientId,
            key,
            ct);

        if (newReservation == null)
        {
            return (false, "Could not reserve new slot. It may be full or already booked.", null, StatusCodes.Status409Conflict);
        }

        var oldScheduleId = app.WorkScheduleId;
        var oldTimeSlot = app.TimeSlot;

        await using var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Update local appointment
            app.Reschedule(
                request.NewWorkScheduleId,
                newReservation.DoctorId,
                newReservation.DoctorName ?? string.Empty,
                newReservation.SpecialtyName,
                newReservation.ScheduleDate,
                request.TimeSlot,
                request.Reason,
                currentUserId,
                updaterName);

            await _db.SaveChangesAsync(ct);

            // Confirm new reservation
            var confirmed = await _schedulingClient.ConfirmReservationAsync(request.NewWorkScheduleId, newReservation.Id, ct);
            if (!confirmed)
            {
                await _schedulingClient.ReleaseReservationAsync(request.NewWorkScheduleId, newReservation.Id, ct);
                if (tx != null) await tx.RollbackAsync(ct);
                return (false, "Failed to confirm new slot with Scheduling service.", null, StatusCodes.Status409Conflict);
            }

            // Release old slot via event
            _outbox.Add(new EventEnvelope<AppointmentCancelled>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentCancelled,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentCancelled(
                    app.Id,
                    app.PatientId,
                    currentUserId,
                    DateTimeOffset.UtcNow,
                    oldScheduleId,
                    oldTimeSlot)
            }, "appointment.cancelled");

            await _db.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            // Best-effort direct release of old slot on Scheduling Service alongside Outbox event
            _ = await _schedulingClient.ReleaseSlotByAppointmentAsync(
                app.Id,
                oldScheduleId,
                oldTimeSlot,
                app.PatientId,
                currentUserId,
                ct);

            return (true, null, ToDto(app), StatusCodes.Status200OK);
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            await _schedulingClient.ReleaseReservationAsync(request.NewWorkScheduleId, newReservation.Id, ct);
            _logger.LogError(ex, "Failed to reschedule appointment {AppointmentId}", appointmentId);
            return (false, "Internal error rescheduling appointment.", null, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, int StatusCode)> CheckInAppointmentAsync(
        Guid appointmentId,
        Guid currentUserId,
        string? idempotencyKey,
        bool isUrgent = false,
        CancellationToken ct = default)
    {
        var app = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && !a.IsDeleted, ct);
        if (app == null) return (false, "Appointment not found.", StatusCodes.Status404NotFound);

        try
        {
            app.CheckIn(DateTime.UtcNow);

            _outbox.Add(new EventEnvelope<AppointmentCheckedIn>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.AppointmentCheckedIn,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Appointment.Service",
                Payload = new AppointmentCheckedIn(
                    app.Id,
                    app.PatientId,
                    app.DoctorId,
                    app.Id, // Stable VisitId
                    DateTimeOffset.UtcNow,
                    isUrgent,
                    app.PatientName,
                    app.DoctorName)
            }, "appointment.checked-in");

            await _db.SaveChangesAsync(ct);
            return (true, null, StatusCodes.Status204NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, StatusCodes.Status409Conflict);
        }
    }

    public async Task<TotalAppointmentSummaryDto> GetSummaryAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        Guid? doctorId = null,
        Guid? specialtyId = null,
        CancellationToken ct = default)
    {
        var query = _db.Appointments.Where(a => !a.IsDeleted);
        if (startDate.HasValue)
        {
            query = query.Where(a => a.ScheduleDate >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            query = query.Where(a => a.ScheduleDate <= endDate.Value);
        }
        if (doctorId.HasValue && doctorId.Value != Guid.Empty)
        {
            query = query.Where(a => a.DoctorId == doctorId.Value);
        }

        var total = await query.CountAsync(ct);
        var onlineCount = await query.CountAsync(a => !a.IsWalkIn, ct);
        var pending = await query.CountAsync(a => a.Status == AppointmentStatus.Pending, ct);
        var confirmed = await query.CountAsync(a => a.Status == AppointmentStatus.Confirmed, ct);
        var checkedIn = await query.CountAsync(a => a.Status == AppointmentStatus.CheckedIn, ct);
        var completed = await query.CountAsync(a => a.Status == AppointmentStatus.Completed, ct);
        var cancelled = await query.CountAsync(a => a.Status == AppointmentStatus.Cancelled, ct);
        var noShow = await query.CountAsync(a => a.Status == AppointmentStatus.NoShow, ct);
        var onlineCancelled = await query.CountAsync(a => !a.IsWalkIn && a.Status == AppointmentStatus.Cancelled, ct);
        var cancellationRate = onlineCount == 0 ? 0.0 : (double)onlineCancelled / onlineCount;

        return new TotalAppointmentSummaryDto(
            TotalAppointments: total,
            PendingAppointments: pending,
            ConfirmedAppointments: confirmed,
            CheckedInAppointments: checkedIn,
            CompletedAppointments: completed,
            CancelledAppointments: cancelled,
            NoShowAppointments: noShow,
            AppointmentCount: total,
            AppointmentOnlineCount: onlineCount,
            CheckedIn: checkedIn,
            CanceledAppointments: cancelled,
            CancellationRate: cancellationRate,
            AverageWaitingMinutes: 0
        );
    }

    public async Task<PagedAppointmentsDto> GetPagedAppointmentsByDateAsync(
        DateOnly date,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _db.Appointments
            .Where(a => a.ScheduleDate == date && !a.IsDeleted);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.TimeSlot)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        return new PagedAppointmentsDto(
            items.Select(ToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize,
            totalPages);
    }

    public async Task<bool> SyncWalkInAppointmentAsync(SyncWalkInAppointmentRequest request, CancellationToken ct = default)
    {
        var existing = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct);
        if (existing != null)
        {
            existing.Status = AppointmentStatus.CheckedIn;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        var appointment = new Appointment(
            request.PatientId,
            request.PatientName,
            request.PatientPhone,
            request.PatientGender,
            request.PatientDateOfBirth,
            request.WorkScheduleId,
            request.DoctorId,
            request.DoctorName,
            request.SpecialtyName,
            request.ScheduleDate,
            request.TimeSlot,
            request.Reason,
            request.CreatedByUserId,
            isWalkIn: true)
        {
            Id = request.AppointmentId,
            Status = AppointmentStatus.CheckedIn,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Synced walk-in appointment {AppointmentId} into ClinicAppointmentDB", request.AppointmentId);
        return true;
    }

    public async Task<bool> ProcessExamCompletedAsync(ExamCompleted evt, CancellationToken ct = default)
    {
        if (evt.AppointmentId == Guid.Empty)
        {
            return false;
        }

        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == evt.AppointmentId, ct);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found when processing ExamCompleted event", evt.AppointmentId);
            return false;
        }

        if (appointment.Status == AppointmentStatus.Completed)
        {
            return true;
        }

        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Appointment {AppointmentId} updated to Completed via ExamCompleted event", evt.AppointmentId);
        return true;
    }

    public async Task<bool> ProcessUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default)
    {
        if (evt.PersonId == Guid.Empty)
        {
            return false;
        }

        var patient = await _db.PatientProjections
            .FirstOrDefaultAsync(p => p.PersonId == evt.PersonId || p.Id == evt.PersonId, ct);

        if (patient != null)
        {
            patient.FullName = evt.FullName;
            if (!string.IsNullOrWhiteSpace(evt.PhoneNumber)) patient.PhoneNumber = evt.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(evt.Gender)) patient.Gender = evt.Gender;
            patient.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            patient = new PatientProjection
            {
                Id = evt.PersonId,
                PersonId = evt.PersonId,
                FullName = evt.FullName,
                PhoneNumber = evt.PhoneNumber,
                Gender = evt.Gender,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.PatientProjections.Add(patient);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("PatientProjection updated for PersonId {PersonId}", evt.PersonId);
        return true;
    }

    private static AppointmentDto ToDto(Appointment a) => new(
        a.Id,
        a.PatientId,
        a.DoctorId,
        a.PatientName,
        a.DoctorName,
        a.TimeSlot,
        a.Reason,
        a.ScheduleDate,
        a.Status,
        a.CreatedAt,
        a.IsWalkIn
    );

    private static AppointmentDetailDto ToDetailDto(Appointment a) => new(
        a.Id,
        a.PatientId,
        a.DoctorId,
        a.PatientName,
        a.PatientPhone,
        a.PatientGender,
        a.PatientDateOfBirth,
        a.DoctorName,
        a.SpecialtyName,
        a.TimeSlot,
        a.ScheduleDate,
        a.Reason,
        a.Status,
        a.CreatedAt,
        a.IsWalkIn,
        a.WorkScheduleId
    );
}

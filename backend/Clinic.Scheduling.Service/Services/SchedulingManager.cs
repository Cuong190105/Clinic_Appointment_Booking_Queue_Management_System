namespace Clinic.Scheduling.Service.Services;

using System.Data;
using Clinic.Contracts.Events;
using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Microsoft.EntityFrameworkCore;

public interface ISchedulingManager
{
    Task<IReadOnlyList<AvailableSlotDto>> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken ct = default);
    Task<ReservationResponse> ReserveSlotAsync(Guid scheduleId, TimeOnly timeSlot, Guid patientId, string idempotencyKey, CancellationToken ct = default);
    Task<bool> ConfirmReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<bool> ReleaseReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default);
    Task<WorkSchedule> ApproveShiftRequestAsync(Guid shiftRequestId, string? note, CancellationToken ct = default);
    Task<bool> RejectShiftRequestAsync(Guid shiftRequestId, string? reason, CancellationToken ct = default);
    Task<bool> CancelShiftRequestAsync(Guid shiftRequestId, CancellationToken ct = default);
    Task<WorkSchedule> UpdateScheduleAsync(Guid scheduleId, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int recommendedCapacity, CancellationToken ct = default);
    Task<bool> CancelScheduleAsync(Guid scheduleId, string reason, CancellationToken ct = default);
    Task<int> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default);
}

public class SchedulingManager : ISchedulingManager
{
    private readonly SchedulingDbContext _dbContext;
    private readonly ISchedulingOutboxWriter _outboxWriter;
    private readonly ILogger<SchedulingManager> _logger;

    public SchedulingManager(
        SchedulingDbContext dbContext,
        ISchedulingOutboxWriter outboxWriter,
        ILogger<SchedulingManager> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableSlotDto>> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = query.FromDate ?? today;
        var toDate = query.ToDate ?? fromDate.AddDays(14);

        var schedulesQuery = _dbContext.WorkSchedules
            .AsNoTracking()
            .Include(ws => ws.Doctor)
                .ThenInclude(d => d.Employee)
            .Include(ws => ws.Doctor)
                .ThenInclude(d => d.WorkHistories)
                .ThenInclude(wh => wh.Specialty)
            .Include(ws => ws.SlotReservations)
            .Where(ws => ws.Status == WorkScheduleStatus.Active && !ws.IsDeleted)
            .Where(ws => ws.Date >= fromDate && ws.Date <= toDate);

        if (query.DoctorId.HasValue)
        {
            schedulesQuery = schedulesQuery.Where(ws => ws.DoctorId == query.DoctorId.Value);
        }

        if (query.SpecialtyId.HasValue)
        {
            schedulesQuery = schedulesQuery.Where(ws => ws.Doctor.WorkHistories.Any(wh => wh.SpecialtyId == query.SpecialtyId.Value && !wh.IsDeleted && wh.EndDate == null));
        }

        var schedules = await schedulesQuery
            .OrderBy(ws => ws.Date)
            .ThenBy(ws => ws.ShiftStart)
            .ToListAsync(ct);

        var availableSlots = new List<AvailableSlotDto>();

        foreach (var ws in schedules)
        {
            // Active reservations: Confirmed OR (Reserved and not expired)
            var activeReservations = ws.SlotReservations
                .Where(r => r.Status == ReservationStatus.Confirmed ||
                           (r.Status == ReservationStatus.Reserved && r.ExpiresAt > now))
                .ToList();

            var onlineCount = activeReservations.Count;

            // RULE: If onlineCount >= RecommendedCapacity, the entire shift is excluded (no slots returned)
            if (onlineCount >= ws.RecommendedCapacity)
            {
                continue;
            }

            var reservedTimeSlots = activeReservations.Select(r => r.TimeSlot).ToHashSet();
            var remainingCapacity = ws.RecommendedCapacity - onlineCount;

            var doctorName = ws.Doctor?.Employee?.FullName ?? "Unknown Doctor";
            var specialtyName = ws.Doctor?.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null).Select(wh => wh.Specialty?.Name).FirstOrDefault();

            // Generate 15-minute slots across [ShiftStart, ShiftEnd)
            var slotInterval = TimeSpan.FromMinutes(WorkSchedule.SlotIntervalMinutes);
            var currentSlot = ws.ShiftStart;

            while (currentSlot.AddMinutes(WorkSchedule.SlotIntervalMinutes) <= ws.ShiftEnd)
            {
                // Slot is only available if not already reserved/confirmed
                if (!reservedTimeSlots.Contains(currentSlot))
                {
                    availableSlots.Add(new AvailableSlotDto(
                        ScheduleId: ws.Id,
                        DoctorId: ws.DoctorId,
                        DoctorName: doctorName,
                        SpecialtyName: specialtyName,
                        Date: ws.Date,
                        TimeSlot: currentSlot,
                        ShiftStart: ws.ShiftStart,
                        ShiftEnd: ws.ShiftEnd,
                        RecommendedCapacity: ws.RecommendedCapacity,
                        RemainingCapacity: remainingCapacity
                    ));
                }

                currentSlot = currentSlot.AddMinutes(WorkSchedule.SlotIntervalMinutes);
            }
        }

        return availableSlots;
    }

    public async Task<ReservationResponse> ReserveSlotAsync(
        Guid scheduleId,
        TimeOnly timeSlot,
        Guid patientId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency-Key header is required.", nameof(idempotencyKey));

        // 1. Idempotency Check
        var existing = await _dbContext.SlotReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

        if (existing != null)
        {
            _logger.LogInformation("Idempotent request detected for key {Key}, returning existing reservation {Id}.", idempotencyKey, existing.Id);
            return new ReservationResponse(
                existing.Id,
                existing.WorkScheduleId,
                existing.TimeSlot,
                existing.PatientId,
                existing.Status.ToString(),
                existing.ExpiresAt,
                existing.IdempotencyKey
            );
        }

        // 2. Transaction with Serializable / RowLock protection
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;

            var schedule = await _dbContext.WorkSchedules
                .FirstOrDefaultAsync(ws => ws.Id == scheduleId && ws.Status == WorkScheduleStatus.Active && !ws.IsDeleted, ct);

            if (schedule == null)
            {
                throw new KeyNotFoundException($"Active work schedule '{scheduleId}' not found.");
            }

            // Validate timeSlot bounds
            if (timeSlot < schedule.ShiftStart || timeSlot.AddMinutes(WorkSchedule.SlotIntervalMinutes) > schedule.ShiftEnd)
            {
                throw new InvalidOperationException($"TimeSlot {timeSlot} is outside shift window [{schedule.ShiftStart} - {schedule.ShiftEnd}].");
            }

            var now = DateTimeOffset.UtcNow;

            // Count valid active reservations
            var activeReservations = await _dbContext.SlotReservations
                .Where(r => r.WorkScheduleId == scheduleId &&
                           (r.Status == ReservationStatus.Confirmed ||
                           (r.Status == ReservationStatus.Reserved && r.ExpiresAt > now)))
                .ToListAsync(ct);

            // Layer 1: Shift capacity threshold check
            if (activeReservations.Count >= schedule.RecommendedCapacity)
            {
                throw new InvalidOperationException("Ca làm việc đã đủ số lượng đặt online (RecommendedCapacity exceeded).");
            }

            // Layer 2: Exact slot conflict check
            if (activeReservations.Any(r => r.TimeSlot == timeSlot))
            {
                throw new InvalidOperationException($"Khung giờ {timeSlot} đã được người khác đặt.");
            }

            // Create reservation (10 minutes timeout)
            var reservation = new SlotReservation
            {
                Id = Guid.NewGuid(),
                WorkScheduleId = scheduleId,
                TimeSlot = timeSlot,
                PatientId = patientId,
                Status = ReservationStatus.Reserved,
                ExpiresAt = now.AddMinutes(10),
                IdempotencyKey = idempotencyKey,
                CreatedAt = now
            };

            _dbContext.SlotReservations.Add(reservation);
            await _dbContext.SaveChangesAsync(ct);
            if (transaction != null)
                await transaction.CommitAsync(ct);

            _logger.LogInformation("Reserved slot {TimeSlot} on schedule {ScheduleId} for patient {PatientId}. Expires at {ExpiresAt}.",
                timeSlot, scheduleId, patientId, reservation.ExpiresAt);

            return new ReservationResponse(
                reservation.Id,
                reservation.WorkScheduleId,
                reservation.TimeSlot,
                reservation.PatientId,
                reservation.Status.ToString(),
                reservation.ExpiresAt,
                reservation.IdempotencyKey
            );
        });
    }

    public async Task<bool> ConfirmReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await _dbContext.SlotReservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.WorkScheduleId == scheduleId, ct);

        if (reservation == null)
            return false;

        reservation.Confirm();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReleaseReservationAsync(Guid scheduleId, Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await _dbContext.SlotReservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.WorkScheduleId == scheduleId, ct);

        if (reservation == null)
            return false;

        reservation.Release();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WorkSchedule> ApproveShiftRequestAsync(Guid shiftRequestId, string? note, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;

            var request = await _dbContext.ShiftRequests
                .FirstOrDefaultAsync(sr => sr.Id == shiftRequestId && !sr.IsDeleted, ct);

            if (request == null)
                throw new KeyNotFoundException($"Shift request '{shiftRequestId}' not found.");

            if (request.Status != ShiftRequestStatus.Pending)
                throw new InvalidOperationException($"Cannot approve shift request in '{request.Status}' status.");

            // Conflict detection: Doctor already has an active schedule on this date with overlapping time
            var hasConflict = await _dbContext.WorkSchedules.AnyAsync(ws =>
                ws.DoctorId == request.DoctorId &&
                ws.Date == request.Date &&
                ws.Status == WorkScheduleStatus.Active &&
                !ws.IsDeleted &&
                ws.ShiftStart < request.ShiftEnd &&
                request.ShiftStart < ws.ShiftEnd, ct);

            if (hasConflict)
            {
                throw new InvalidOperationException("Bác sĩ đã có ca trực khác trùng thời gian trong ngày.");
            }

            var schedule = request.Approve();
            _dbContext.WorkSchedules.Add(schedule);

            // Record Outbox event in same local transaction
            var workScheduleCreated = new WorkScheduleCreated(
                schedule.Id,
                schedule.DoctorId,
                schedule.Date,
                schedule.ShiftStart,
                schedule.ShiftEnd,
                schedule.RecommendedCapacity);

            var envelope = new EventEnvelope<WorkScheduleCreated>
            {
                EventId = Guid.NewGuid(),
                EventType = EventTypes.WorkScheduleCreated,
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
                Producer = "Clinic.Scheduling.Service",
                Payload = workScheduleCreated
            };

            _outboxWriter.Add(envelope, "schedule.created");

            await _dbContext.SaveChangesAsync(ct);
            if (transaction != null)
                await transaction.CommitAsync(ct);

            _logger.LogInformation("Approved shift request {RequestId}, created schedule {ScheduleId}.", shiftRequestId, schedule.Id);
            return schedule;
        });
    }

    public async Task<bool> RejectShiftRequestAsync(Guid shiftRequestId, string? reason, CancellationToken ct = default)
    {
        var request = await _dbContext.ShiftRequests
            .FirstOrDefaultAsync(sr => sr.Id == shiftRequestId && !sr.IsDeleted, ct);

        if (request == null)
            return false;

        request.Reject(reason);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelShiftRequestAsync(Guid shiftRequestId, CancellationToken ct = default)
    {
        var request = await _dbContext.ShiftRequests
            .FirstOrDefaultAsync(sr => sr.Id == shiftRequestId && !sr.IsDeleted, ct);

        if (request == null)
            return false;

        request.Cancel();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WorkSchedule> UpdateScheduleAsync(Guid scheduleId, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int recommendedCapacity, CancellationToken ct = default)
    {
        var schedule = await _dbContext.WorkSchedules
            .FirstOrDefaultAsync(ws => ws.Id == scheduleId && !ws.IsDeleted, ct);

        if (schedule == null)
            throw new KeyNotFoundException($"Work schedule '{scheduleId}' not found.");

        if (schedule.Status == WorkScheduleStatus.Cancelled)
            throw new InvalidOperationException("Cannot update a cancelled schedule.");

        var hasConflict = await _dbContext.WorkSchedules.AnyAsync(ws =>
            ws.DoctorId == schedule.DoctorId &&
            ws.Id != scheduleId &&
            ws.Date == date &&
            ws.Status == WorkScheduleStatus.Active &&
            !ws.IsDeleted &&
            ws.ShiftStart < shiftEnd &&
            shiftStart < ws.ShiftEnd, ct);

        if (hasConflict)
            throw new InvalidOperationException("The updated schedule overlaps with another existing schedule.");

        schedule.UpdateShift(date, shiftStart, shiftEnd, recommendedCapacity);

        var workScheduleUpdated = new WorkScheduleUpdated(
            schedule.Id,
            schedule.DoctorId,
            schedule.Date,
            schedule.ShiftStart,
            schedule.ShiftEnd,
            schedule.RecommendedCapacity);

        var envelope = new EventEnvelope<WorkScheduleUpdated>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.WorkScheduleUpdated,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "Clinic.Scheduling.Service",
            Payload = workScheduleUpdated
        };

        _outboxWriter.Add(envelope, "schedule.updated");

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Updated schedule {ScheduleId} and added WorkScheduleUpdated to outbox.", schedule.Id);
        return schedule;
    }

    public async Task<bool> CancelScheduleAsync(Guid scheduleId, string reason, CancellationToken ct = default)
    {
        var schedule = await _dbContext.WorkSchedules
            .FirstOrDefaultAsync(ws => ws.Id == scheduleId && !ws.IsDeleted, ct);

        if (schedule == null)
            return false;

        if (schedule.Status == WorkScheduleStatus.Cancelled)
            return true;

        schedule.Cancel(reason);

        var workScheduleCancelled = new WorkScheduleCancelled(
            schedule.Id,
            schedule.DoctorId,
            reason,
            DateTimeOffset.UtcNow);

        var envelope = new EventEnvelope<WorkScheduleCancelled>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.WorkScheduleCancelled,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "Clinic.Scheduling.Service",
            Payload = workScheduleCancelled
        };

        _outboxWriter.Add(envelope, "schedule.cancelled");

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Cancelled schedule {ScheduleId} with reason '{Reason}' and added WorkScheduleCancelled to outbox.", schedule.Id, reason);
        return true;
    }

    public async Task<int> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default)
    {
        var employees = await _dbContext.Employees
            .Where(e => e.PersonId == evt.PersonId && !e.IsDeleted)
            .ToListAsync(ct);

        if (employees.Count == 0)
            return 0;

        foreach (var emp in employees)
        {
            emp.FullName = evt.FullName;
            if (evt.PhoneNumber != null) emp.PhoneNumber = evt.PhoneNumber;
            if (evt.Email != null) emp.Email = evt.Email;
            if (evt.Gender != null) emp.Gender = evt.Gender;
            emp.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synchronized profile for PersonId {PersonId} across {Count} employee records.", evt.PersonId, employees.Count);
        return employees.Count;
    }
}

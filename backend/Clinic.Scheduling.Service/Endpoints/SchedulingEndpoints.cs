namespace Clinic.Scheduling.Service.Endpoints;

using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingApi(this IEndpointRouteBuilder app)
    {
        // ==========================================
        // 1. AVAILABILITY & SLOTS APIS
        // ==========================================
        app.MapGet("/schedules/availability", async (
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var query = new AvailabilityQuery(doctorId, specialtyId, fromDate, toDate);
            var slots = await manager.GetAvailabilityAsync(query, ct);
            return Results.Ok(new { isSuccess = true, result = slots, totalCount = slots.Count });
        });

        // Alias for compatibility with Monolith slot queries
        app.MapGet("/slots", async (
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] DateOnly? date,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var actualFrom = fromDate ?? date;
            var actualTo = toDate ?? date;
            var query = new AvailabilityQuery(doctorId, specialtyId, actualFrom, actualTo);
            var slots = await manager.GetAvailabilityAsync(query, ct);
            return Results.Ok(new { isSuccess = true, result = slots, totalCount = slots.Count });
        });

        // ==========================================
        // 2. RESERVATION APIS (HOLD - CONFIRM - RELEASE)
        // ==========================================
        app.MapPost("/schedules/{scheduleId:guid}/reservations", async (
            Guid scheduleId,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] CreateReservationRequest request,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new { error = "Header 'Idempotency-Key' is required." });
            }

            try
            {
                var response = await manager.ReserveSlotAsync(scheduleId, request.TimeSlot, request.PatientId, idempotencyKey, ct);
                return Results.Created($"/schedules/{scheduleId}/reservations/{response.Id}", response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/schedules/{scheduleId:guid}/reservations/{reservationId:guid}/confirm", async (
            Guid scheduleId,
            Guid reservationId,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            try
            {
                var success = await manager.ConfirmReservationAsync(scheduleId, reservationId, ct);
                if (!success)
                    return Results.NotFound(new { error = $"Reservation '{reservationId}' not found on schedule '{scheduleId}'." });

                return Results.Ok(new { isSuccess = true, message = "Reservation confirmed." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        app.MapPost("/schedules/{scheduleId:guid}/reservations/{reservationId:guid}/release", async (
            Guid scheduleId,
            Guid reservationId,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            try
            {
                var success = await manager.ReleaseReservationAsync(scheduleId, reservationId, ct);
                if (!success)
                    return Results.NotFound(new { error = $"Reservation '{reservationId}' not found on schedule '{scheduleId}'." });

                return Results.Ok(new { isSuccess = true, message = "Reservation released." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // ==========================================
        // 3. SHIFT SUGGESTIONS / REQUESTS APIS
        // ==========================================
        app.MapPost("/shifts/suggestions/{id:guid}/approve", async (
            Guid id,
            [FromBody] ShiftSuggestionApprovalRequest? request,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            try
            {
                var schedule = await manager.ApproveShiftRequestAsync(id, request?.Note, ct);
                return Results.Ok(new { isSuccess = true, message = "Shift request approved.", result = schedule });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        app.MapPost("/shifts/suggestions/{id:guid}/reject", async (
            Guid id,
            [FromBody] ShiftSuggestionRejectionRequest? request,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var success = await manager.RejectShiftRequestAsync(id, request?.Reason, ct);
            if (!success)
                return Results.NotFound(new { error = $"Shift request '{id}' not found." });

            return Results.Ok(new { isSuccess = true, message = "Shift request rejected." });
        });

        app.MapPost("/shifts/suggestions/{id:guid}/cancel", async (
            Guid id,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var success = await manager.CancelShiftRequestAsync(id, ct);
            if (!success)
                return Results.NotFound(new { error = $"Shift request '{id}' not found." });

            return Results.Ok(new { isSuccess = true, message = "Shift request cancelled." });
        });

        // ==========================================
        // 4. METADATA & QUERY APIS (DOCTORS, SPECIALTIES, SCHEDULES)
        // ==========================================
        app.MapGet("/doctors", async (
            [FromQuery] Guid? specialtyId,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var query = db.Doctors
                .AsNoTracking()
                .Include(d => d.Employee)
                .Include(d => d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null))
                    .ThenInclude(wh => wh.Specialty)
                .Where(d => !d.IsDeleted);

            if (specialtyId.HasValue)
            {
                query = query.Where(d => d.WorkHistories.Any(wh => wh.SpecialtyId == specialtyId.Value && !wh.IsDeleted && wh.EndDate == null));
            }

            var doctors = await query
                .OrderBy(d => d.Employee.FullName)
                .Select(d => new DoctorDto(
                    d.Id,
                    d.EmployeeId,
                    d.Employee.FullName,
                    d.Employee.PhoneNumber,
                    d.Employee.Email,
                    d.LicenseNumber,
                    d.Qualification,
                    d.ExperienceYears,
                    d.Biography,
                    d.Status.ToString(),
                    d.WorkHistories.Select(wh => wh.Specialty.Name).FirstOrDefault()
                ))
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, result = doctors, totalCount = doctors.Count });
        });

        app.MapGet("/doctors/{id:guid}", async (
            Guid id,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var doctor = await db.Doctors
                .AsNoTracking()
                .Include(d => d.Employee)
                .Include(d => d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null))
                    .ThenInclude(wh => wh.Specialty)
                .Where(d => d.Id == id && !d.IsDeleted)
                .Select(d => new DoctorDto(
                    d.Id,
                    d.EmployeeId,
                    d.Employee.FullName,
                    d.Employee.PhoneNumber,
                    d.Employee.Email,
                    d.LicenseNumber,
                    d.Qualification,
                    d.ExperienceYears,
                    d.Biography,
                    d.Status.ToString(),
                    d.WorkHistories.Select(wh => wh.Specialty.Name).FirstOrDefault()
                ))
                .FirstOrDefaultAsync(ct);

            if (doctor == null)
                return Results.NotFound(new { error = $"Doctor '{id}' not found." });

            return Results.Ok(new { isSuccess = true, result = doctor });
        });

        app.MapGet("/specialties", async (
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var specialties = await db.Specialties
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Name)
                .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description, s.EstablishedDate))
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, result = specialties, totalCount = specialties.Count });
        });

        app.MapGet("/schedules", async (
            [FromQuery] Guid? doctorId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var query = db.WorkSchedules
                .AsNoTracking()
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.Employee)
                .Where(ws => !ws.IsDeleted);

            if (doctorId.HasValue)
                query = query.Where(ws => ws.DoctorId == doctorId.Value);
            if (fromDate.HasValue)
                query = query.Where(ws => ws.Date >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(ws => ws.Date <= toDate.Value);

            var schedules = await query
                .OrderBy(ws => ws.Date)
                .ThenBy(ws => ws.ShiftStart)
                .Select(ws => new WorkScheduleDto(
                    ws.Id,
                    ws.DoctorId,
                    ws.Doctor.Employee.FullName,
                    ws.Date,
                    ws.ShiftStart,
                    ws.ShiftEnd,
                    ws.RecommendedCapacity,
                    ws.Status.ToString(),
                    ws.CancellationReason
                ))
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, result = schedules, totalCount = schedules.Count });
        });

        app.MapGet("/schedules/{id:guid}", async (
            Guid id,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var ws = await db.WorkSchedules
                .AsNoTracking()
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.Employee)
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.WorkHistories)
                    .ThenInclude(wh => wh.Specialty)
                .FirstOrDefaultAsync(ws => ws.Id == id && !ws.IsDeleted, ct);

            if (ws == null)
                return Results.NotFound(new { error = $"Schedule '{id}' not found." });

            var specialty = ws.Doctor.WorkHistories.FirstOrDefault(wh => !wh.IsDeleted && wh.EndDate == null)?.Specialty?.Name;
            var detail = new ScheduleDetailDto(
                ws.Id,
                ws.DoctorId,
                ws.Doctor.Employee.FullName,
                specialty,
                ws.Date,
                ws.ShiftStart,
                ws.ShiftEnd,
                ws.RecommendedCapacity,
                ws.Status.ToString(),
                ws.CancellationReason
            );

            return Results.Ok(new { isSuccess = true, result = detail });
        });

        app.MapPut("/schedules/{id:guid}", async (
            Guid id,
            [FromBody] UpdateScheduleRequest request,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            try
            {
                var schedule = await manager.UpdateScheduleAsync(id, request.Date, request.ShiftStart, request.ShiftEnd, request.RecommendedCapacity, ct);
                return Results.Ok(new { isSuccess = true, message = "Schedule updated.", result = schedule });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        app.MapPost("/schedules/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelScheduleRequest request,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var success = await manager.CancelScheduleAsync(id, request.Reason, ct);
            if (!success)
                return Results.NotFound(new { error = $"Schedule '{id}' not found." });

            return Results.Ok(new { isSuccess = true, message = "Schedule cancelled." });
        });

        // ==========================================
        // 5. INTERNAL EVENTS (Profile Synchronization)
        // ==========================================
        app.MapPost("/internal/events/user-profile-updated", async (
            [FromBody] Clinic.Contracts.Events.UserProfileUpdated evt,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var updatedCount = await manager.HandleUserProfileUpdatedAsync(evt, ct);
            return Results.Ok(new { isSuccess = true, message = $"Synchronized {updatedCount} employee profile(s)." });
        });

        return app;
    }
}


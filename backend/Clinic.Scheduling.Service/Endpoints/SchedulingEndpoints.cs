namespace Clinic.Scheduling.Service.Endpoints;

using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingApi(this IEndpointRouteBuilder app)
    {
        app.MapStaffSchedulingApi();
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
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            try
            {
                var schedule = await manager.ApproveShiftRequestAsync(id, null, ct);
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
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/shifts/suggestions/{id:guid}/reject", async (
            Guid id,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var success = await manager.RejectShiftRequestAsync(id, null, ct);
            if (!success)
                return Results.NotFound(new { error = $"Shift request '{id}' not found." });

            return Results.Ok(new { isSuccess = true, message = "Shift request rejected." });
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/shifts/suggestions/{id:guid}/cancel", async (
            Guid id,
            ClaimsPrincipal principal,
            SchedulingDbContext db,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var doctorId = Guid.TryParse(principal.FindFirstValue("doctorId"), out var parsedDoctorId) ? parsedDoctorId : Guid.Empty;
            if (doctorId == Guid.Empty || !await db.ShiftRequests.AnyAsync(r => r.Id == id && r.DoctorId == doctorId && !r.IsDeleted, ct))
                return Results.NotFound(new { error = $"Shift request '{id}' not found." });
            var success = await manager.CancelShiftRequestAsync(id, ct);
            if (!success)
                return Results.NotFound(new { error = $"Shift request '{id}' not found." });

            return Results.Ok(new { isSuccess = true, message = "Shift request cancelled." });
        }).RequireAuthorization("DoctorOnly");

        // ==========================================
        // 4. METADATA & QUERY APIS (DOCTORS, SPECIALTIES, SCHEDULES)
        // ==========================================
        app.MapGet("/doctors", async (
            [FromQuery] Guid? specialtyId,
            [FromQuery] string? search,
            [FromQuery] string? gender,
            [FromQuery] string? qualification,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? orderBy,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var currentPage = Math.Max(1, pageNumber ?? 1); var currentPageSize = Math.Clamp(pageSize ?? 10, 1, 100);
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

            if (int.TryParse(gender, out var genderValue))
                gender = genderValue switch { 0 => "Male", 1 => "Female", 2 => "Other", _ => "Unspecified" };
            if (!string.IsNullOrWhiteSpace(gender)) query = query.Where(d => d.Employee.Gender == gender);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(d => d.Employee.FullName.Contains(search) || d.LicenseNumber.Contains(search));
            if (!string.IsNullOrWhiteSpace(qualification)) query = query.Where(d => d.Qualification.Contains(qualification));
            if (Enum.TryParse<DoctorStatus>(status, true, out var parsedStatus)) query = query.Where(d => d.Status == parsedStatus);
            var count = await query.CountAsync(ct);
            query = string.Equals(sortBy, "experienceYears", StringComparison.OrdinalIgnoreCase)
                ? (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(d => d.ExperienceYears) : query.OrderBy(d => d.ExperienceYears))
                : string.Equals(sortBy, "qualification", StringComparison.OrdinalIgnoreCase)
                    ? (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(d => d.Qualification) : query.OrderBy(d => d.Qualification))
                    : string.Equals(sortBy, "status", StringComparison.OrdinalIgnoreCase)
                        ? (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(d => d.Status) : query.OrderBy(d => d.Status))
                        : string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(d => d.Employee.FullName) : query.OrderBy(d => d.Employee.FullName);

            var doctors = await query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
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
                    d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null).Select(wh => wh.Specialty.Name).FirstOrDefault(),
                    d.Employee.Gender,
                    d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null).Select(wh => (Guid?)wh.SpecialtyId).FirstOrDefault(),
                    d.Employee.DateOfBirth,
                    d.Employee.Address,
                    d.Employee.HireDate
                ))
                .ToListAsync(ct);

            var result = new { items = doctors, pageNumber = currentPage, pageSize = currentPageSize, totalCount = count,
                totalPages = (int)Math.Ceiling(count / (double)currentPageSize), hasPrevious = currentPage > 1,
                hasNext = currentPage < (int)Math.Ceiling(count / (double)currentPageSize) };
            return Results.Ok(new { isSuccess = true, result });
        }).AllowAnonymous();

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
                    d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null).Select(wh => wh.Specialty.Name).FirstOrDefault(),
                    d.Employee.Gender,
                    d.WorkHistories.Where(wh => !wh.IsDeleted && wh.EndDate == null).Select(wh => (Guid?)wh.SpecialtyId).FirstOrDefault(),
                    d.Employee.DateOfBirth,
                    d.Employee.Address,
                    d.Employee.HireDate
                ))
                .FirstOrDefaultAsync(ct);

            if (doctor == null)
                return Results.NotFound(new { error = $"Doctor '{id}' not found." });

            return Results.Ok(doctor);
        }).AllowAnonymous();

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
        }).RequireAuthorization("DoctorOnly");

        app.MapGet("/schedules/walk-in-candidates", async (
            [FromQuery] DateOnly date,
            [FromQuery] Guid specialtyId,
            [FromQuery] Guid? doctorId,
            SchedulingDbContext db,
            CancellationToken ct) =>
        {
            var query = db.WorkSchedules.AsNoTracking()
                .Include(ws => ws.Doctor).ThenInclude(d => d.Employee)
                .Include(ws => ws.Doctor).ThenInclude(d => d.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null)).ThenInclude(h => h.Specialty)
                .Where(ws => ws.Date == date && ws.Status == WorkScheduleStatus.Active && !ws.IsDeleted
                    && !ws.Doctor.IsDeleted
                    && ws.Doctor.WorkHistories.Any(h => !h.IsDeleted && h.EndDate == null && h.SpecialtyId == specialtyId));
            if (doctorId.HasValue) query = query.Where(ws => ws.DoctorId == doctorId.Value);
            var result = await query.OrderBy(ws => ws.ShiftStart).Select(ws => new WalkInScheduleDto(
                ws.Id, ws.DoctorId, ws.Doctor.Employee.FullName,
                ws.Doctor.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null && h.SpecialtyId == specialtyId).Select(h => h.Specialty.Name).FirstOrDefault(),
                ws.Date, ws.ShiftStart, ws.ShiftEnd, ws.Status.ToString())).ToListAsync(ct);
            return Results.Ok(new { isSuccess = true, result });
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
            [FromHeader(Name = "X-Clinic-Service-Token")] string? serviceToken,
            IConfiguration configuration,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            var expectedToken = configuration["Services:InternalToken"];
            if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(serviceToken) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(serviceToken), Encoding.UTF8.GetBytes(expectedToken)))
                return Results.Unauthorized();
            var updatedCount = await manager.HandleUserProfileUpdatedAsync(evt, ct);
            return Results.Ok(new { isSuccess = true, message = $"Synchronized {updatedCount} employee profile(s)." });
        });

        app.MapPost("/internal/events/appointment-cancelled", async (
            [FromBody] Clinic.Contracts.Events.AppointmentCancelled evt,
            ISchedulingManager manager,
            CancellationToken ct) =>
        {
            if (evt.WorkScheduleId != Guid.Empty)
            {
                var released = await manager.ReleaseSlotByAppointmentAsync(evt.WorkScheduleId, evt.TimeSlot, evt.PatientId, ct);
                return Results.Ok(new { isSuccess = true, released });
            }
            return Results.Ok(new { isSuccess = true, released = false });
        });

        return app;
    }
}


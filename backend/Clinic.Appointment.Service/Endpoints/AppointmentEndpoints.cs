namespace Clinic.Appointment.Service.Endpoints;

using System.Security.Claims;
using Clinic.Appointment.Service.DTOs;
using Clinic.Appointment.Service.Services;
using Clinic.Contracts.Events;
using Microsoft.AspNetCore.Mvc;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentApi(this IEndpointRouteBuilder app)
    {
        // 1. ONLINE BOOKING (Patient Saga: Reserve -> Create -> Confirm)
        app.MapPost("/appointments", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] CreateAppointmentRequest request,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var roles = GetRoles(user);
            var isStaff = roles.Any(r => r is "Admin" or "Receptionist" or "Doctor");
            var patientId = GetPatientId(user);

            var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
                request,
                userId,
                patientId,
                isStaff,
                idempotencyKey,
                ct);

            if (!success)
            {
                return Results.Problem(
                    detail: errorMessage,
                    statusCode: statusCode,
                    title: "Appointment booking failed");
            }

            return Results.Created($"/appointments/{result!.Id}", result);
        }).RequireAuthorization();

        // 2. RECEPTIONIST WALK-IN APPOINTMENT (Direct Confirmed)
        app.MapPost("/receptionist/appointments", CreateReceptionistAppointment).RequireAuthorization("StaffOnly");
        app.MapPost("/receptionist/appointments/walkin", CreateReceptionistAppointment).RequireAuthorization("StaffOnly");

        static async Task<IResult> CreateReceptionistAppointment(
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] ReceptionistCreateAppointmentRequest request,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct)
        {
            var userId = GetUserId(user);
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var mappedRequest = new CreateAppointmentRequest(
                request.WorkScheduleId,
                request.TimeSlot,
                request.Reason,
                request.PatientId,
                request.IsWalkIn);

            var (success, errorMessage, result, statusCode) = await manager.CreateAppointmentAsync(
                mappedRequest,
                userId,
                request.PatientId,
                isStaff: true,
                idempotencyKey,
                ct);

            if (!success)
            {
                return Results.Problem(
                    detail: errorMessage,
                    statusCode: statusCode,
                    title: "Walk-in appointment creation failed");
            }

            return Results.Created($"/appointments/{result!.Id}", result);
        }

        // 3. APPOINTMENT DETAIL
        app.MapGet("/appointments/{appointmentId:guid}", async (
            Guid appointmentId,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var roles = GetRoles(user);
            var patientId = GetPatientId(user);
            var doctorId = GetDoctorId(user);

            var (success, errorMessage, result, statusCode) = await manager.GetAppointmentByIdAsync(
                appointmentId,
                userId,
                roles,
                patientId,
                doctorId,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.Ok(result);
        }).RequireAuthorization();

        // 4. MY APPOINTMENTS (Patient)
        app.MapGet("/me/appointments", async (
            [FromQuery] string? category,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var patientId = GetPatientId(user);
            if (patientId == null || patientId == Guid.Empty)
            {
                // Fallback to sub/userId as patientId
                patientId = GetUserId(user);
            }

            if (patientId == null || patientId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var list = await manager.GetMyAppointmentsAsync(patientId.Value, category, ct);
            return Results.Ok(list);
        }).RequireAuthorization();

        app.MapGet("/me/appointments/{appointmentId:guid}", async (
            Guid appointmentId,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var roles = GetRoles(user);
            var patientId = GetPatientId(user);

            var (success, errorMessage, result, statusCode) = await manager.GetAppointmentByIdAsync(
                appointmentId,
                userId,
                roles,
                patientId,
                null,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.Ok(result);
        }).RequireAuthorization();

        // 5. RECEPTIONIST PENDING LIST
        app.MapGet("/receptionist/appointments/pending", async (
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var pending = await manager.GetPendingAppointmentsAsync(ct);
            return Results.Ok(pending);
        }).RequireAuthorization("StaffOnly");

        // 6. CONFIRM APPOINTMENT
        var confirmHandler = async (
            Guid appointmentId,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var updaterName = GetUserName(user);

            var (success, errorMessage, statusCode) = await manager.ConfirmAppointmentAsync(
                appointmentId,
                userId,
                updaterName,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.NoContent();
        };

        app.MapPost("/appointments/{appointmentId:guid}/confirm", confirmHandler).RequireAuthorization("StaffOnly");
        app.MapPost("/receptionist/appointments/{appointmentId:guid}/confirm", confirmHandler).RequireAuthorization("StaffOnly");

        // 7. CHECK-IN APPOINTMENT (Arrival -> Event QueueTicketCreated)
        var checkinHandler = async (
            Guid appointmentId,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);

            var (success, errorMessage, statusCode) = await manager.CheckInAppointmentAsync(
                appointmentId,
                userId,
                idempotencyKey,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.NoContent();
        };

        app.MapPost("/appointments/{appointmentId:guid}/check-in", checkinHandler).RequireAuthorization("StaffOnly");
        app.MapPost("/appointments/{appointmentId:guid}/checkin", checkinHandler).RequireAuthorization("StaffOnly");
        app.MapPost("/receptionist/appointments/{appointmentId:guid}/checkin", checkinHandler).RequireAuthorization("StaffOnly");

        // 8. CANCEL APPOINTMENT
        app.MapPost("/appointments/{appointmentId:guid}/cancel", async (
            Guid appointmentId,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var roles = GetRoles(user);
            var isAdminOrReceptionist = roles.Any(r => r is "Admin" or "Receptionist");
            var updaterName = GetUserName(user);

            var (success, errorMessage, statusCode) = await manager.CancelAppointmentAsync(
                appointmentId,
                userId,
                isAdminOrReceptionist,
                updaterName,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.NoContent();
        }).RequireAuthorization();

        // 9. RESCHEDULE APPOINTMENT (Saga: Reserve new -> Update appointment & Snapshot -> Confirm new -> Release old)
        var rescheduleHandler = async (
            Guid appointmentId,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] UpdateAppointmentRequest request,
            ClaimsPrincipal user,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var updaterName = GetUserName(user);

            var (success, errorMessage, result, statusCode) = await manager.RescheduleAppointmentAsync(
                appointmentId,
                request,
                userId,
                updaterName,
                idempotencyKey,
                ct);

            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.Ok(result);
        };

        app.MapPatch("/appointments/{appointmentId:guid}", rescheduleHandler).RequireAuthorization();
        app.MapPost("/appointments/{appointmentId:guid}/reschedule", rescheduleHandler).RequireAuthorization();
        app.MapPatch("/receptionist/appointments/{appointmentId:guid}", rescheduleHandler).RequireAuthorization("StaffOnly");

        // 10. ADMIN & DOCTOR APPOINTMENTS LIST BY DATE
        app.MapGet("/appointments", async (
            [FromQuery] DateOnly? date,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var list = await manager.GetAppointmentsByDateAsync(targetDate, ct);
            return Results.Ok(list);
        }).RequireAuthorization("StaffOnly");

        app.MapGet("/admin/appointments", async (
            [FromQuery] DateOnly? date,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var paged = await manager.GetPagedAppointmentsByDateAsync(
                targetDate,
                pageNumber ?? 1,
                pageSize ?? 10,
                ct);
            return Results.Ok(paged);
        }).RequireAuthorization("StaffOnly");

        // 11. SUMMARY STATISTICS
        var summaryHandler = async (
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var summary = await manager.GetSummaryAsync(startDate, endDate, doctorId, specialtyId, ct);
            return Results.Ok(summary);
        };

        app.MapGet("/appointments/summary", summaryHandler).RequireAuthorization("StaffOnly");
        app.MapGet("/admin/appointments/summary", summaryHandler).RequireAuthorization("StaffOnly");

        // 12. AUDIT HISTORY / CHANGELOG TRACEBACK
        var changelogHandler = async (
            Guid appointmentId,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var (success, errorMessage, result, statusCode) = await manager.GetAppointmentChangelogAsync(appointmentId, ct);
            if (!success)
            {
                return Results.Problem(detail: errorMessage, statusCode: statusCode);
            }

            return Results.Ok(result);
        };

        app.MapGet("/appointments/{appointmentId:guid}/history", changelogHandler).RequireAuthorization("StaffOnly");
        app.MapGet("/appointments/{appointmentId:guid}/audit-history", changelogHandler).RequireAuthorization("StaffOnly");
        app.MapGet("/appointments/{appointmentId:guid}/snapshots", changelogHandler).RequireAuthorization("StaffOnly");
        app.MapGet("/admin/appointments/{appointmentId:guid}/history", changelogHandler).RequireAuthorization("StaffOnly");

        // 13. INTERNAL EVENT HOOKS
        app.MapPost("/internal/events/exam-completed", async (
            [FromBody] ExamCompleted evt,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var handled = await manager.ProcessExamCompletedAsync(evt, ct);
            return handled ? Results.Ok(new { handled = true }) : Results.BadRequest(new { handled = false });
        });

        app.MapPost("/internal/events/user-profile-updated", async (
            [FromBody] UserProfileUpdated evt,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var handled = await manager.ProcessUserProfileUpdatedAsync(evt, ct);
            return handled ? Results.Ok(new { handled = true }) : Results.BadRequest(new { handled = false });
        });

        app.MapPost("/internal/appointments/sync-walkin", async (
            [FromBody] SyncWalkInAppointmentRequest request,
            IAppointmentManager manager,
            CancellationToken ct) =>
        {
            var handled = await manager.SyncWalkInAppointmentAsync(request, ct);
            return handled ? Results.Ok(new { synced = true }) : Results.BadRequest(new { synced = false });
        });

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var uid) ? uid : Guid.Empty;
    }

    private static string? GetUserName(ClaimsPrincipal user)
    {
        return user.FindFirstValue("name") ?? user.FindFirstValue("username") ?? user.Identity?.Name;
    }

    private static List<string> GetRoles(ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(r => r.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Guid? GetPatientId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("patientId") ?? user.FindFirstValue("personId");
        return Guid.TryParse(raw, out var pid) ? pid : null;
    }

    private static Guid? GetDoctorId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("doctorId");
        return Guid.TryParse(raw, out var did) ? did : null;
    }
}

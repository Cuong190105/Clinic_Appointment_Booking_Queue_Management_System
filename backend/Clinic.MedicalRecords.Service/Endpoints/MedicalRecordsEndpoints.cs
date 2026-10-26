using System.Security.Claims;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.MedicalRecords.Service.Endpoints;

public static class MedicalRecordsEndpoints
{
    public static IEndpointRouteBuilder MapMedicalRecordsEndpoints(this IEndpointRouteBuilder app)
    {
        // Patient endpoints
        app.MapPost("/patients", CreatePatientHandler)
           .RequireAuthorization("PatientCreateAny");
        app.MapPost("/patients/add", CreatePatientHandler)
           .RequireAuthorization("PatientCreateAny");
        app.MapPost("/patients/walk-in", CreatePatientHandler)
           .RequireAuthorization("PatientCreateAny");

        app.MapGet("/patients", GetPagedPatientsHandler)
           .RequireAuthorization("PatientViewAny");

        app.MapGet("/patients/{id:guid}", GetPatientByIdHandler)
           .RequireAuthorization();

        app.MapGet("/patients/me", GetMyPatientHandler)
           .RequireAuthorization("PatientViewOwn");

        app.MapPut("/patients/me", UpdateMyPatientHandler)
           .RequireAuthorization("PatientEditOwn");

        app.MapPut("/patients/{id:guid}", UpdatePatientByIdHandler)
           .RequireAuthorization("PatientEditAny");

        // Medical Report endpoints
        app.MapPost("/medical-records", SaveReportHandler)
           .RequireAuthorization("ReportCreate");

        app.MapPut("/medical-records/{id:guid}", UpdateDraftReportHandler)
           .RequireAuthorization("ReportCreate");

        app.MapPost("/medical-records/{id:guid}/finalize", FinalizeReportHandler)
           .RequireAuthorization("ReportCreate");

        app.MapPost("/medical-records/appointments/{appointmentId:guid}/amendments", CreateAmendmentHandler)
           .RequireAuthorization("ReportCreate");

        app.MapGet("/medical-records", GetPagedReportsHandler)
           .RequireAuthorization("ReportView");

        app.MapGet("/medical-records/{id:guid}", GetReportByIdHandler)
           .RequireAuthorization("ReportView");

        app.MapGet("/medical-records/appointment/{appointmentId:guid}", GetReportByAppointmentHandler)
           .RequireAuthorization("ReportView");

        app.MapGet("/patients/{patientId:guid}/medical-history", GetPatientMedicalHistoryHandler)
           .RequireAuthorization("PatientHistoryView");

        app.MapGet("/me/medical-history", GetMyMedicalHistoryHandler)
           .RequireAuthorization("PatientViewOwn");

        // Access audit endpoints
        app.MapGet("/medical-records/appointments/{appointmentId:guid}/audits", GetAuditsByAppointmentHandler)
           .RequireAuthorization("AdminOnly");

        // Internal synchronization endpoint
        app.MapPost("/internal/events/user-profile-updated", HandleUserProfileUpdatedHandler)
           .AllowAnonymous();

        return app;
    }

    #region Patient Handlers

    private static async Task<IResult> CreatePatientHandler(
        [FromBody] CreatePatientRequest request,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { message = "FullName is required." });
        }

        var result = await manager.CreatePatientAsync(request, ct);
        return Results.Created($"/patients/{result.Id}", result);
    }

    private static async Task<IResult> GetPagedPatientsHandler(
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? orderBy,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        pageNumber = pageNumber <= 0 ? 1 : pageNumber;
        pageSize = pageSize <= 0 ? 10 : pageSize;
        var result = await manager.GetPagedPatientsAsync(search, sortBy, orderBy, pageNumber, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPatientByIdHandler(
        Guid id,
        ClaimsPrincipal user,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var patient = await manager.GetPatientByIdAsync(id, ct);
        if (patient is null)
        {
            return Results.NotFound(new { message = $"Patient with Id={id} was not found." });
        }

        var isStaff = user.IsInRole("Admin") || user.IsInRole("Receptionist") || user.IsInRole("Doctor") ||
                      user.HasClaim("permission", "patient.view.any");
        if (!isStaff)
        {
            var userPatientId = user.FindFirstValue("patientId");
            var userPersonId = user.FindFirstValue("personId");
            var userSub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            var matches = (userPatientId != null && Guid.TryParse(userPatientId, out var pid) && pid == id) ||
                          (userPersonId != null && Guid.TryParse(userPersonId, out var personId) && personId == id) ||
                          (patient.IdentityUserId != null && userSub != null && Guid.TryParse(userSub, out var subGuid) && subGuid == patient.IdentityUserId);

            if (!matches)
            {
                return Results.Forbid();
            }
        }

        return Results.Ok(patient);
    }

    private static async Task<IResult> GetMyPatientHandler(
        ClaimsPrincipal user,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var patientIdClaim = user.FindFirstValue("patientId");
        if (patientIdClaim != null && Guid.TryParse(patientIdClaim, out var patientId))
        {
            var patient = await manager.GetPatientByIdAsync(patientId, ct);
            if (patient != null) return Results.Ok(patient);
        }

        var personIdClaim = user.FindFirstValue("personId");
        if (personIdClaim != null && Guid.TryParse(personIdClaim, out var personId))
        {
            var patient = await manager.GetPatientByIdAsync(personId, ct);
            if (patient != null) return Results.Ok(patient);
        }

        var subClaim = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subClaim != null && Guid.TryParse(subClaim, out var identityUserId))
        {
            var patient = await manager.GetPatientByIdentityUserIdAsync(identityUserId, ct);
            if (patient != null) return Results.Ok(patient);
        }

        return Results.NotFound(new { message = "Patient profile not found for current user." });
    }

    private static async Task<IResult> UpdateMyPatientHandler(
        ClaimsPrincipal user,
        [FromBody] UpdatePatientRequest request,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        Guid? targetPatientId = null;

        var patientIdClaim = user.FindFirstValue("patientId");
        if (patientIdClaim != null && Guid.TryParse(patientIdClaim, out var pid))
        {
            targetPatientId = pid;
        }
        else
        {
            var personIdClaim = user.FindFirstValue("personId");
            if (personIdClaim != null && Guid.TryParse(personIdClaim, out var personId))
            {
                var patient = await manager.GetPatientByIdAsync(personId, ct);
                if (patient != null) targetPatientId = patient.Id;
            }
        }

        if (targetPatientId == null)
        {
            var subClaim = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (subClaim != null && Guid.TryParse(subClaim, out var identityUserId))
            {
                var patient = await manager.GetPatientByIdentityUserIdAsync(identityUserId, ct);
                if (patient != null) targetPatientId = patient.Id;
            }
        }

        if (targetPatientId == null)
        {
            return Results.NotFound(new { message = "Patient profile not found for current user." });
        }

        var updated = await manager.UpdatePatientAsync(targetPatientId.Value, request, ct);
        return updated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> UpdatePatientByIdHandler(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var updated = await manager.UpdatePatientAsync(id, request, ct);
        return updated ? Results.NoContent() : Results.NotFound(new { message = $"Patient with Id={id} was not found." });
    }

    #endregion

    #region Medical Report Handlers

    private static async Task<IResult> SaveReportHandler(
        [FromBody] SaveMedicalReportRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "Doctor";
        var currentDoctorId = GetDoctorId(user);
        var currentDoctorName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");

        try
        {
            var result = await manager.SaveReportAsync(request, currentUserId, currentRole, currentDoctorId, currentDoctorName, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, currentDoctorId, null,
                result.AppointmentId, result.PatientId, result.Id,
                request.IsFinalize ? "FinalizeReport" : "SaveDraft",
                "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateDraftReportHandler(
        Guid id,
        [FromBody] UpdateMedicalReportRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "Doctor";

        try
        {
            var result = await manager.UpdateReportDraftAsync(id, request, currentUserId, currentRole, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, GetDoctorId(user), null,
                result.AppointmentId, result.PatientId, result.Id,
                request.IsFinalize ? "FinalizeReport" : "UpdateDraft",
                "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Medical report with Id={id} was not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> FinalizeReportHandler(
        Guid id,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "Doctor";

        try
        {
            var result = await manager.FinalizeReportAsync(id, currentUserId, currentRole, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, GetDoctorId(user), null,
                result.AppointmentId, result.PatientId, result.Id,
                "FinalizeReport", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Medical report with Id={id} was not found." });
        }
    }

    private static async Task<IResult> CreateAmendmentHandler(
        Guid appointmentId,
        [FromBody] CreateAmendmentRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "Amendment reason is required." });
        }

        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "Doctor";
        var currentDoctorId = GetDoctorId(user) ?? Guid.Empty;
        var currentDoctorName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? "Attending Doctor";

        try
        {
            var result = await manager.CreateAmendmentAsync(appointmentId, request, currentUserId, currentRole, currentDoctorId, currentDoctorName, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, currentDoctorId, null,
                appointmentId, null, result.MedicalReportId,
                "CreateAmendment", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

            return Results.Created($"/medical-records/appointments/{appointmentId}/amendments/{result.Id}", result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Medical report for AppointmentId={appointmentId} was not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> GetPagedReportsHandler(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        // Receptionist is strictly forbidden from clinical records
        if (IsReceptionistOnly(user))
        {
            await manager.RecordAccessAuditAsync(
                GetUserId(user), "Receptionist", null, null,
                null, null, null, "ViewPagedReports", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
        }

        pageNumber = pageNumber <= 0 ? 1 : pageNumber;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        var result = await manager.GetPagedReportsAsync(search, status, fromDate, toDate, pageNumber, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReportByIdHandler(
        Guid id,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "User";

        // Receptionist is strictly forbidden from clinical records
        if (IsReceptionistOnly(user))
        {
            await manager.RecordAccessAuditAsync(
                currentUserId, "Receptionist", null, null,
                null, null, id, "ViewReport", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
        }

        var result = await manager.GetReportByIdAsync(id, ct);
        if (result is null)
        {
            return Results.NotFound(new { message = $"Medical report with Id={id} was not found." });
        }

        // Patient IDOR protection
        var isPrivilegedStaff = user.IsInRole("Doctor") || user.IsInRole("Admin") ||
                                user.HasClaim("permission", "medical-report.view.any");
        if (!isPrivilegedStaff)
        {
            var userPatientId = GetPatientId(user);
            var userPersonId = GetPersonId(user);

            var matches = (userPatientId.HasValue && userPatientId.Value == result.PatientId) ||
                          (userPersonId.HasValue && userPersonId.Value == result.PatientId);

            if (!matches)
            {
                await manager.RecordAccessAuditAsync(
                    currentUserId, currentRole, null, userPatientId,
                    result.AppointmentId, result.PatientId, id,
                    "ViewReport", "Denied", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
        }

        await manager.RecordAccessAuditAsync(
            currentUserId, currentRole, GetDoctorId(user), GetPatientId(user),
            result.AppointmentId, result.PatientId, id,
            "ViewReport", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetReportByAppointmentHandler(
        Guid appointmentId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "User";

        // Receptionist is strictly forbidden from clinical records
        if (IsReceptionistOnly(user))
        {
            await manager.RecordAccessAuditAsync(
                currentUserId, "Receptionist", null, null,
                appointmentId, null, null, "ViewReportByAppointment", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
        }

        var result = await manager.GetReportByAppointmentIdAsync(appointmentId, ct);
        if (result is null)
        {
            return Results.NotFound(new { message = $"Medical report for AppointmentId={appointmentId} was not found." });
        }

        // Patient IDOR protection
        var isPrivilegedStaff = user.IsInRole("Doctor") || user.IsInRole("Admin") ||
                                user.HasClaim("permission", "medical-report.view.any");
        if (!isPrivilegedStaff)
        {
            var userPatientId = GetPatientId(user);
            var userPersonId = GetPersonId(user);

            var matches = (userPatientId.HasValue && userPatientId.Value == result.PatientId) ||
                          (userPersonId.HasValue && userPersonId.Value == result.PatientId);

            if (!matches)
            {
                await manager.RecordAccessAuditAsync(
                    currentUserId, currentRole, null, userPatientId,
                    appointmentId, result.PatientId, result.Id,
                    "ViewReportByAppointment", "Denied", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
        }

        await manager.RecordAccessAuditAsync(
            currentUserId, currentRole, GetDoctorId(user), GetPatientId(user),
            appointmentId, result.PatientId, result.Id,
            "ViewReportByAppointment", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPatientMedicalHistoryHandler(
        Guid patientId,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "User";

        // Receptionist is strictly forbidden from clinical records
        if (IsReceptionistOnly(user))
        {
            await manager.RecordAccessAuditAsync(
                currentUserId, "Receptionist", null, null,
                null, patientId, null, "ViewPatientMedicalHistory", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
        }

        // Patient IDOR protection
        var isPrivilegedStaff = user.IsInRole("Doctor") || user.IsInRole("Admin") ||
                                user.HasClaim("permission", "patient-history.view.any");
        if (!isPrivilegedStaff)
        {
            var userPatientId = GetPatientId(user);
            var userPersonId = GetPersonId(user);

            var matches = (userPatientId.HasValue && userPatientId.Value == patientId) ||
                          (userPersonId.HasValue && userPersonId.Value == patientId);

            if (!matches)
            {
                await manager.RecordAccessAuditAsync(
                    currentUserId, currentRole, null, userPatientId,
                    null, patientId, null, "ViewPatientMedicalHistory", "Denied",
                    httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
        }

        var result = await manager.GetPatientMedicalHistoryAsync(patientId, ct);
        await manager.RecordAccessAuditAsync(
            currentUserId, currentRole, GetDoctorId(user), GetPatientId(user),
            null, patientId, null, "ViewPatientMedicalHistory", "Success",
            httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetMyMedicalHistoryHandler(
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var currentUserId = GetUserId(user);
        var currentRole = user.FindFirstValue(ClaimTypes.Role) ?? "Patient";

        var patientIdClaim = user.FindFirstValue("patientId");
        if (patientIdClaim != null && Guid.TryParse(patientIdClaim, out var patientId))
        {
            var result = await manager.GetMyMedicalHistoryAsync(patientId, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, null, patientId,
                null, patientId, null, "ViewMyMedicalHistory", "Success",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Ok(result);
        }

        var personIdClaim = user.FindFirstValue("personId");
        if (personIdClaim != null && Guid.TryParse(personIdClaim, out var personId))
        {
            var result = await manager.GetMyMedicalHistoryAsync(personId, ct);
            await manager.RecordAccessAuditAsync(
                currentUserId, currentRole, null, personId,
                null, personId, null, "ViewMyMedicalHistory", "Success",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Ok(result);
        }

        var subClaim = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subClaim != null && Guid.TryParse(subClaim, out var identityUserId))
        {
            var patient = await manager.GetPatientByIdentityUserIdAsync(identityUserId, ct);
            if (patient != null)
            {
                var result = await manager.GetMyMedicalHistoryAsync(patient.Id, ct);
                await manager.RecordAccessAuditAsync(
                    currentUserId, currentRole, null, patient.Id,
                    null, patient.Id, null, "ViewMyMedicalHistory", "Success",
                    httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Ok(result);
            }
        }

        return Results.Ok(new List<MedicalReportDto>());
    }

    private static async Task<IResult> GetAuditsByAppointmentHandler(
        Guid appointmentId,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var audits = await manager.GetAuditsByAppointmentAsync(appointmentId, ct);
        return Results.Ok(audits);
    }

    #endregion

    #region Internal Handlers

    private static async Task<IResult> HandleUserProfileUpdatedHandler(
        [FromBody] UserProfileUpdated evt,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var updated = await manager.HandleUserProfileUpdatedAsync(evt, ct);
        return Results.Ok(new { handled = true, updated });
    }

    #endregion

    private static bool IsReceptionistOnly(ClaimsPrincipal user) =>
        user.IsInRole("Receptionist") && !user.IsInRole("Doctor") && !user.IsInRole("Admin");

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var guid) ? guid : Guid.Empty;
    }

    private static Guid? GetDoctorId(ClaimsPrincipal user)
    {
        var docId = user.FindFirstValue("doctorId");
        return Guid.TryParse(docId, out var guid) ? guid : null;
    }

    private static Guid? GetPatientId(ClaimsPrincipal user)
    {
        var patId = user.FindFirstValue("patientId");
        return Guid.TryParse(patId, out var guid) ? guid : null;
    }

    private static Guid? GetPersonId(ClaimsPrincipal user)
    {
        var perId = user.FindFirstValue("personId");
        return Guid.TryParse(perId, out var guid) ? guid : null;
    }
}

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
        app.MapPost("/internal/patients/provision-identity", ProvisionIdentityPatientHandler)
           .AllowAnonymous();
        app.MapGet("/internal/patients/resolve-registration", ResolveRegistrationPatientHandler)
           .AllowAnonymous();
        app.MapGet("/internal/patients/count", GetPatientCountHandler)
           .AllowAnonymous();

        return app;
    }

    #region Patient Handlers

    private static async Task<IResult> CreatePatientHandler(
        [FromBody] CreatePatientRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.DateOfBirth == default ||
            string.IsNullOrWhiteSpace(request.Gender) || string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["FullName, DateOfBirth, Gender, and Address are required."]
            });
        }

        var result = await manager.CreatePatientAsync(request, GetUserId(user), GetActorRole(user),
            httpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Results.Created($"/patients/{result.Id}", result);
    }

    private static async Task<IResult> GetPagedPatientsHandler(
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? orderBy,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var page = !pageNumber.HasValue || pageNumber.Value <= 0 ? 1 : pageNumber.Value;
        var size = !pageSize.HasValue || pageSize.Value <= 0 ? 10 : pageSize.Value;
        var result = await manager.GetPagedPatientsAsync(search, sortBy, orderBy, page, size, ct);
        await manager.RecordAccessAuditAsync(GetUserId(user), GetActorRole(user), GetDoctorId(user), GetPatientId(user),
            null, null, null, "ViewPagedPatients", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPatientByIdHandler(
        Guid id,
        ClaimsPrincipal user,
        HttpContext httpContext,
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
            var userSub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            var matches = (userPatientId != null && Guid.TryParse(userPatientId, out var pid) && pid == id) ||
                          (patient.IdentityUserId != null && userSub != null && Guid.TryParse(userSub, out var subGuid) && subGuid == patient.IdentityUserId);

            if (!matches)
            {
                await manager.RecordAccessAuditAsync(GetUserId(user), GetActorRole(user), GetDoctorId(user), GetPatientId(user),
                    null, id, null, "ViewPatient", "Denied", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
        }

        await manager.RecordAccessAuditAsync(GetUserId(user), GetActorRole(user), GetDoctorId(user), GetPatientId(user),
            null, id, null, "ViewPatient", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

        return Results.Ok(patient);
    }

    private static async Task<IResult> GetMyPatientHandler(
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var patientIdClaim = user.FindFirstValue("patientId");
        if (patientIdClaim != null && Guid.TryParse(patientIdClaim, out var patientId))
        {
            var patient = await manager.GetPatientByIdAsync(patientId, ct);
            if (patient != null)
            {
                await manager.RecordAccessAuditAsync(GetUserId(user), GetActorRole(user), GetDoctorId(user), patient.Id,
                    null, patient.Id, null, "ViewOwnPatient", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Ok(patient);
            }
        }

        var subClaim = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subClaim != null && Guid.TryParse(subClaim, out var identityUserId))
        {
            var patient = await manager.GetPatientByIdentityUserIdAsync(identityUserId, ct);
            if (patient != null)
            {
                await manager.RecordAccessAuditAsync(GetUserId(user), GetActorRole(user), GetDoctorId(user), patient.Id,
                    null, patient.Id, null, "ViewOwnPatient", "Success", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Ok(patient);
            }
        }

        return Results.NotFound(new { message = "Patient profile not found for current user." });
    }

    private static async Task<IResult> UpdateMyPatientHandler(
        ClaimsPrincipal user,
        [FromBody] UpdatePatientRequest request,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        Guid? targetPatientId = null;

        var patientIdClaim = user.FindFirstValue("patientId");
        if (patientIdClaim != null && Guid.TryParse(patientIdClaim, out var pid))
        {
            targetPatientId = pid;
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

        var updated = await manager.UpdatePatientAsync(targetPatientId.Value, request, GetUserId(user),
            GetActorRole(user), httpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return updated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> UpdatePatientByIdHandler(
        Guid id,
        [FromBody] UpdatePatientRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var updated = await manager.UpdatePatientAsync(id, request, GetUserId(user), GetActorRole(user),
            httpContext.Connection.RemoteIpAddress?.ToString(), ct);
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
        var currentRole = GetActorRole(user);
        var currentDoctorId = GetDoctorId(user);
        var currentDoctorName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");

        try
        {
            var result = await manager.SaveReportAsync(request, currentUserId, currentRole, currentDoctorId,
                currentDoctorName, httpContext.Connection.RemoteIpAddress?.ToString(), ct);

            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            await manager.RecordAccessAuditAsync(currentUserId, currentRole, currentDoctorId, GetPatientId(user),
                request.AppointmentId, request.PatientId, null, request.IsFinalize ? "FinalizeReport" : "SaveDraft",
                "Denied", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
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
        var currentRole = GetActorRole(user);

        try
        {
            var result = await manager.UpdateReportDraftAsync(id, request, currentUserId, currentRole, GetDoctorId(user),
                httpContext.Connection.RemoteIpAddress?.ToString(), ct);

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
        catch (UnauthorizedAccessException)
        {
            await manager.RecordAccessAuditAsync(currentUserId, currentRole, GetDoctorId(user), GetPatientId(user),
                null, null, id, request.IsFinalize ? "FinalizeReport" : "UpdateDraft", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
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
        var currentRole = GetActorRole(user);

        try
        {
            var result = await manager.FinalizeReportAsync(id, currentUserId, currentRole, GetDoctorId(user),
                httpContext.Connection.RemoteIpAddress?.ToString(), ct);

            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Medical report with Id={id} was not found." });
        }
        catch (UnauthorizedAccessException)
        {
            await manager.RecordAccessAuditAsync(currentUserId, currentRole, GetDoctorId(user), GetPatientId(user),
                null, null, id, "FinalizeReport", "Denied", httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
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
        var currentRole = GetActorRole(user);
        var currentDoctorId = GetDoctorId(user) ?? Guid.Empty;
        var currentDoctorName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? "Attending Doctor";

        try
        {
            if (user.IsInRole("Doctor") && !user.IsInRole("Admin") && currentDoctorId == Guid.Empty) return Results.Forbid();
            var result = await manager.CreateAmendmentAsync(appointmentId, request, currentUserId, currentRole,
                currentDoctorId, currentDoctorName, httpContext.Connection.RemoteIpAddress?.ToString(), ct);

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
        catch (UnauthorizedAccessException)
        {
            await manager.RecordAccessAuditAsync(currentUserId, currentRole, currentDoctorId, GetPatientId(user),
                appointmentId, null, null, "CreateAmendment", "Denied",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetPagedReportsHandler(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
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

        var page = !pageNumber.HasValue || pageNumber.Value <= 0 ? 1 : pageNumber.Value;
        var size = !pageSize.HasValue || pageSize.Value <= 0 ? 10 : pageSize.Value;
        var doctorId = user.IsInRole("Doctor") ? GetDoctorId(user) : null;
        if (user.IsInRole("Doctor") && !doctorId.HasValue) return Results.Forbid();
        var result = await manager.GetPagedReportsAsync(search, status, fromDate, toDate, page, size, doctorId, ct);
        await manager.RecordAccessAuditAsync(GetUserId(user), user.FindFirstValue(ClaimTypes.Role) ?? "User",
            doctorId, GetPatientId(user), null, null, null, "ViewPagedReports", "Success",
            httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
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
        var currentRole = GetActorRole(user);

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
        if (!CanViewReport(user, result))
        {
            var userPatientId = GetPatientId(user);
            var matches = userPatientId.HasValue && userPatientId.Value == result.PatientId;

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
        var currentRole = GetActorRole(user);

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
        if (!CanViewReport(user, result))
        {
            var userPatientId = GetPatientId(user);
            var matches = userPatientId.HasValue && userPatientId.Value == result.PatientId;

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
        var currentRole = GetActorRole(user);

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
        var isPrivilegedStaff = user.IsInRole("Admin") || user.HasClaim("permission", "patient-history.view.any");
        if (user.IsInRole("Doctor") && !isPrivilegedStaff)
        {
            var doctorId = GetDoctorId(user);
            if (!doctorId.HasValue)
            {
                await manager.RecordAccessAuditAsync(currentUserId, currentRole, doctorId, null,
                    null, patientId, null, "ViewPatientMedicalHistory", "Denied",
                    httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
            var history = await manager.GetPatientMedicalHistoryAsync(patientId, doctorId, ct);
            await manager.RecordAccessAuditAsync(currentUserId, currentRole, doctorId, null,
                null, patientId, null, "ViewPatientMedicalHistory", "Success",
                httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
            return Results.Ok(history);
        }
        if (!isPrivilegedStaff)
        {
            var userPatientId = GetPatientId(user);
            var matches = userPatientId.HasValue && userPatientId.Value == patientId;

            if (!matches)
            {
                await manager.RecordAccessAuditAsync(
                    currentUserId, currentRole, null, userPatientId,
                    null, patientId, null, "ViewPatientMedicalHistory", "Denied",
                    httpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);
                return Results.Forbid();
            }
        }

        var result = await manager.GetPatientMedicalHistoryAsync(patientId, null, ct);
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
        var currentRole = GetActorRole(user);

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
        HttpRequest request,
        IConfiguration configuration,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (!HasValidServiceToken(request, configuration)) return Results.Unauthorized();
        var updated = await manager.HandleUserProfileUpdatedAsync(evt, ct);
        return Results.Ok(new { handled = true, updated });
    }

    private static async Task<IResult> ProvisionIdentityPatientHandler(
        [FromBody] ProvisionIdentityPatientRequest request,
        HttpRequest httpRequest,
        IConfiguration configuration,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (!HasValidServiceToken(httpRequest, configuration)) return Results.Unauthorized();
        if (request.PatientId == Guid.Empty || request.PersonId == Guid.Empty || request.IdentityUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.FullName)) return Results.BadRequest();
        var patient = await manager.ProvisionIdentityPatientAsync(request, ct);
        return Results.Ok(patient);
    }

    private static async Task<IResult> ResolveRegistrationPatientHandler(
        [FromQuery] string phoneNumber,
        HttpRequest httpRequest,
        IConfiguration configuration,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (!HasValidServiceToken(httpRequest, configuration)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(phoneNumber)) return Results.BadRequest();
        var ids = await manager.ResolveRegistrationPatientAsync(phoneNumber, ct);
        return Results.Ok(ids);
    }

    private static async Task<IResult> GetPatientCountHandler(
        HttpRequest httpRequest,
        IConfiguration configuration,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        if (!HasValidServiceToken(httpRequest, configuration)) return Results.Unauthorized();
        return Results.Ok(new PatientCountResponse(await manager.GetPatientCountAsync(ct)));
    }

    #endregion

    private static bool IsReceptionistOnly(ClaimsPrincipal user) =>
        user.IsInRole("Receptionist") && !user.IsInRole("Doctor") && !user.IsInRole("Admin");

    private static string GetActorRole(ClaimsPrincipal user) =>
        user.IsInRole("Admin") ? "Admin" : user.IsInRole("Doctor") ? "Doctor" :
        user.IsInRole("Receptionist") ? "Receptionist" : user.IsInRole("Patient") ? "Patient" : "User";

    private static bool CanViewReport(ClaimsPrincipal user, MedicalReportDto report)
    {
        if (user.IsInRole("Admin") || user.HasClaim("permission", "medical-report.view.any")) return true;
        if (user.IsInRole("Doctor"))
            return GetDoctorId(user) is Guid doctorId && doctorId != Guid.Empty && report.DoctorId == doctorId;
        return GetPatientId(user) is Guid patientId && patientId == report.PatientId;
    }

    private static bool HasValidServiceToken(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["Identity:ServiceToken"];
        var supplied = request.Headers["X-Clinic-Service-Token"].ToString();
        return !string.IsNullOrWhiteSpace(expected) &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied));
    }

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

public sealed record PatientCountResponse(int Count);

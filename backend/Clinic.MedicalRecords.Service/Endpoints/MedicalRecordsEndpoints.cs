using System.Security.Claims;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Services;
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

        // Internal synchronization endpoint
        app.MapPost("/internal/events/user-profile-updated", HandleUserProfileUpdatedHandler)
           .AllowAnonymous();

        return app;
    }

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

    private static async Task<IResult> HandleUserProfileUpdatedHandler(
        [FromBody] UserProfileUpdated evt,
        IMedicalRecordsManager manager,
        CancellationToken ct)
    {
        var updated = await manager.HandleUserProfileUpdatedAsync(evt, ct);
        return Results.Ok(new { handled = true, updated });
    }
}

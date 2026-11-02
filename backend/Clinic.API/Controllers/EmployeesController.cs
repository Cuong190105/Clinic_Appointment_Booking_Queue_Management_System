using MediatR;
using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/employees")]
    public class EmployeesController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IdentityAccountClient _identityAccounts;
        private readonly MedicalRecordsServiceClient _medicalRecords;
        private readonly SchedulingManagementClient _scheduling;
        private readonly ProfileProjectionSyncClient _profileSync;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(ISender sender, ICurrentUser currentUser, IdentityAccountClient identityAccounts, MedicalRecordsServiceClient medicalRecords, SchedulingManagementClient scheduling, ProfileProjectionSyncClient profileSync, ILogger<EmployeesController> logger)
        {
            _sender = sender;
            _currentUser = currentUser;
            _identityAccounts = identityAccounts;
            _medicalRecords = medicalRecords;
            _scheduling = scheduling;
            _profileSync = profileSync;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Policy = "Permission:employee.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
        {
            var profileIds = await _sender.Send(new EnsureIdentityRegistrationProfileCommand(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address), HttpContext.RequestAborted);
            if (profileIds.DoctorId is not null) return Conflict(new { message = "This account already has a doctor profile." });
            var patientIds = await _medicalRecords.ResolveRegistrationPatientAsync(request.PhoneNumber, profileIds.PersonId, HttpContext.RequestAborted);
            if (patientIds is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Medical Records could not resolve the employee account's Patient profile." });
            var identityUserId = Guid.NewGuid();
            var roles = request.Roles.Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var provisioned = await _identityAccounts.ProvisionAsync(identityUserId, profileIds.PersonId,
                request.Username, request.Password, request.FullName, request.PhoneNumber, request.Email,
                request.DateOfBirth, request.Gender, request.Address, roles, patientIds.PatientId, HttpContext.RequestAborted);
            if (provisioned is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity could not provision the employee account." });
            identityUserId = provisioned.UserId;
            var patientId = provisioned.PatientId ?? patientIds.PatientId;
            if (!await _medicalRecords.ProvisionIdentityPatientAsync(new IdentityPatientProvisionRequest(
                    patientId, profileIds.PersonId, identityUserId, request.FullName, request.PhoneNumber,
                    request.Email, request.DateOfBirth, request.Gender.ToString(), request.Address), HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The Identity account was created, but its Medical Records Patient profile is pending. Retry the operation." });
            var created = await _scheduling.SendAsync(HttpContext, HttpMethod.Post, "employees", new
            {
                id = profileIds.EmployeeId,
                personId = profileIds.PersonId,
                identityUserId,
                fullName = request.FullName,
                phoneNumber = request.PhoneNumber,
                email = request.Email,
                gender = request.Gender.ToString(),
                dateOfBirth = request.DateOfBirth,
                address = request.Address,
                hireDate = request.HireDate,
                status = "Active"
            }, HttpContext.RequestAborted);
            using var createdJson = created.ParseJson();
            var employeeId = profileIds.EmployeeId ?? Guid.Empty;
            if (createdJson is not null && createdJson.RootElement.TryGetProperty("id", out var idElement)) employeeId = idElement.GetGuid();
            if (created.StatusCode is < 200 or >= 300)
                return created.ToActionResult();
            if (employeeId == Guid.Empty || !await _identityAccounts.UpdateRolesAsync(identityUserId, roles, patientId, employeeId, null, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The employee profile was created, but Identity activation is pending." });
            return PublicCreateResult(created, roles);
        }

        [HttpPost("from-user")]
        [Authorize(Policy = "Permission:employee.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFromUser([FromBody] CreateEmployeeFromUserRequest request)
        {
            var current = await _identityAccounts.GetRolesAsync(request.UserId, HttpContext.RequestAborted);
            if (current is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity account could not be loaded." });
            var roles = current.Value.Roles.Concat(request.Roles ?? []).Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!await _identityAccounts.RolesExistAsync(roles, HttpContext.RequestAborted)) return BadRequest(new { message = "One or more roles are not defined in Identity." });
            var profile = await _identityAccounts.GetUserProfileAsync(request.UserId, HttpContext.RequestAborted);
            if (profile is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity profile could not be loaded." });
            var created = await _scheduling.SendAsync(HttpContext, HttpMethod.Post, "employees", new
            {
                id = profile.EmployeeId,
                personId = profile.PersonId,
                identityUserId = profile.UserId,
                fullName = profile.FullName,
                phoneNumber = profile.PhoneNumber,
                email = profile.Email,
                gender = profile.Gender,
                dateOfBirth = profile.DateOfBirth,
                address = profile.Address,
                hireDate = request.HireDate,
                status = "Active"
            }, HttpContext.RequestAborted);
            if (created.StatusCode is < 200 or >= 300) return created.ToActionResult();
            using var employeeJson = created.ParseJson();
            var employeeId = profile.EmployeeId ?? Guid.Empty;
            if (employeeJson is not null && employeeJson.RootElement.TryGetProperty("id", out var idElement)) employeeId = idElement.GetGuid();
            if (employeeId == Guid.Empty || !await _identityAccounts.UpdateRolesAsync(request.UserId, roles, profile.PatientId, employeeId, profile.DoctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The employee profile was created, but Identity role update is pending." });
            return PublicCreateResult(created, roles);
        }

        [HttpPut("{employeeId}")]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = "Permission:employee.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Update([FromRoute] Guid employeeId, [FromBody] AdminUpdateEmployeeRequest request)
        {
            var employeeResponse = await _scheduling.SendAsync(HttpContext, HttpMethod.Get, $"employees/{employeeId}", null, HttpContext.RequestAborted);
            using var employeeJson = employeeResponse.ParseJson();
            if (employeeResponse.StatusCode == StatusCodes.Status404NotFound || employeeJson is null) return NotFound();
            if (!employeeJson.RootElement.TryGetProperty("userId", out var userIdElement) || userIdElement.ValueKind != System.Text.Json.JsonValueKind.String)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "The employee has no linked Identity account." });
            var employeeUserId = userIdElement.GetGuid();
            var identity = await _identityAccounts.GetRolesAsync(employeeUserId, HttpContext.RequestAborted);
            if (identity is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            var roles = (request.Roles ?? identity.Value.Roles.ToList()).Concat(identity.Value.Roles.Where(x => x is "Doctor" or "Patient"))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!await _identityAccounts.RolesExistAsync(roles, HttpContext.RequestAborted)) return BadRequest(new { message = "One or more roles are not defined in Identity." });
            if (!await _identityAccounts.UpdateProfileAsync(employeeUserId, request.FullName, request.PhoneNumber, request.Email,
                    request.DateOfBirth, request.Gender, request.Address, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity profile update failed." });
            if (!await _identityAccounts.UpdateRolesAsync(employeeUserId, roles, identity.Value.PatientId, employeeId, identity.Value.DoctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Profile was saved, but Identity role update is pending." });
            var profile = await _identityAccounts.GetUserProfileAsync(employeeUserId, HttpContext.RequestAborted);
            if (profile is null || !await _profileSync.SyncAsync(new Clinic.Contracts.Events.UserProfileUpdated(profile.PersonId,
                    profile.FullName, profile.PhoneNumber, profile.Email, profile.Gender, profile.DateOfBirth, profile.Address,
                    profile.PatientId, profile.UserId), HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity was updated, but profile projections are still pending." });
            return NoContent();
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:employee.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateEmployeeRequest request)
        {
            var userId = _currentUser.UserId;
            if (userId is null) return Unauthorized();
            if (!await _identityAccounts.UpdateProfileAsync(userId.Value, request.FullName, request.PhoneNumber, request.Email,
                    request.DateOfBirth, request.Gender, request.Address, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity profile update failed." });
            var profile = await _identityAccounts.GetUserProfileAsync(userId.Value, HttpContext.RequestAborted);
            if (profile is null || !await _profileSync.SyncAsync(new Clinic.Contracts.Events.UserProfileUpdated(profile.PersonId,
                    profile.FullName, profile.PhoneNumber, profile.Email, profile.Gender, profile.DateOfBirth, profile.Address,
                    profile.PatientId, profile.UserId), HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity was updated, but profile projections are still pending." });
            return NoContent();
        }

        private static IActionResult PublicCreateResult(SchedulingManagementResponse response, IReadOnlyList<string> roles)
        {
            var json = JsonNode.Parse(response.Content) as JsonObject;
            if (json is null) return response.ToActionResult();
            json.Remove("userId");
            json["roles"] = JsonSerializer.SerializeToNode(roles);
            return new ContentResult { StatusCode = response.StatusCode, ContentType = response.ContentType, Content = json.ToJsonString() };
        }
    }
}

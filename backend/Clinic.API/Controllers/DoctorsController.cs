using MediatR;
using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/doctors")]
    public class DoctorsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IdentityAccountClient _identityAccounts;
        private readonly SchedulingManagementClient _scheduling;
        private readonly ProfileProjectionSyncClient _profileSync;
        private readonly QueueServiceClient _queueClient;

        public DoctorsController(
            ISender sender,
            ICurrentUser currentUser,
            IdentityAccountClient identityAccounts,
            SchedulingManagementClient scheduling,
            ProfileProjectionSyncClient profileSync,
            QueueServiceClient queueClient)
        {
            _sender = sender;
            _currentUser = currentUser;
            _identityAccounts = identityAccounts;
            _scheduling = scheduling;
            _profileSync = profileSync;
            _queueClient = queueClient;
        }

        [HttpPost]
        [Authorize(Policy = "Permission:doctor.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateDoctorRequest request)
        {
            var profileIds = await _sender.Send(new EnsureIdentityRegistrationProfileCommand(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address), HttpContext.RequestAborted);
            var identityUserId = Guid.NewGuid();
            var roles = new[] { "Doctor", "Patient" };
            var provisioned = await _identityAccounts.ProvisionAsync(identityUserId, profileIds.PersonId,
                request.Username, request.Password, request.FullName, request.PhoneNumber, request.Email,
                request.DateOfBirth, request.Gender, request.Address, roles, profileIds.PatientId, HttpContext.RequestAborted);
            if (provisioned is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity could not provision the doctor account." });
            identityUserId = provisioned.UserId;
            var result = await _scheduling.SendAsync(HttpContext, HttpMethod.Post, "doctors", new
            {
                id = profileIds.DoctorId,
                employeeId = profileIds.EmployeeId,
                personId = profileIds.PersonId,
                identityUserId,
                fullName = request.FullName,
                phoneNumber = request.PhoneNumber,
                email = request.Email,
                gender = request.Gender.ToString(),
                dateOfBirth = request.DateOfBirth,
                address = request.Address,
                hireDate = request.HireDate,
                licenseNumber = request.LicenseNumber,
                qualification = request.Qualification,
                biography = request.Biography,
                experienceYears = request.ExperienceYears,
                status = request.Status.ToString(),
                specialtyId = request.SpecialtyId
            }, HttpContext.RequestAborted);
            using var doctorJson = result.ParseJson();
            if (result.StatusCode is < 200 or >= 300) return result.ToActionResult();
            var responseObject = doctorJson?.RootElement;
            var doctorId = profileIds.DoctorId ?? Guid.Empty;
            var employeeId = profileIds.EmployeeId ?? Guid.Empty;
            if (responseObject is not null && responseObject.Value.TryGetProperty("id", out var dId)) doctorId = dId.GetGuid();
            if (responseObject is not null && responseObject.Value.TryGetProperty("employeeId", out var eId)) employeeId = eId.GetGuid();
            if (doctorId == Guid.Empty || employeeId == Guid.Empty || !await _identityAccounts.UpdateRolesAsync(identityUserId, roles, profileIds.PatientId, employeeId, doctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The doctor profile was created, but Identity activation is pending." });
            return PublicCreateResult(result);
        }

        [HttpPost("from-user")]
        [Authorize(Policy = "Permission:doctor.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFromUser([FromBody] CreateDoctorFromUserRequest request)
        {
            var profile = await _identityAccounts.GetUserProfileAsync(request.UserId, HttpContext.RequestAborted);
            if (profile is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity account could not be loaded." });
            var roles = profile.Roles.Append("Doctor").Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!await _identityAccounts.RolesExistAsync(roles, HttpContext.RequestAborted)) return BadRequest(new { message = "Doctor role is not defined in Identity." });
            var result = await _scheduling.SendAsync(HttpContext, HttpMethod.Post, "doctors", new
            {
                id = profile.DoctorId,
                employeeId = profile.EmployeeId,
                personId = profile.PersonId,
                identityUserId = profile.UserId,
                fullName = profile.FullName,
                phoneNumber = profile.PhoneNumber,
                email = profile.Email,
                gender = profile.Gender,
                dateOfBirth = profile.DateOfBirth,
                address = profile.Address,
                hireDate = request.HireDate,
                licenseNumber = request.LicenseNumber,
                qualification = request.Qualification,
                biography = request.Biography,
                experienceYears = request.ExperienceYears,
                status = request.Status.ToString(),
                specialtyId = request.SpecialtyId
            }, HttpContext.RequestAborted);
            if (result.StatusCode is < 200 or >= 300) return result.ToActionResult();
            using var doctorJson = result.ParseJson();
            var responseObject = doctorJson?.RootElement;
            var doctorId = profile.DoctorId ?? Guid.Empty;
            var employeeId = profile.EmployeeId ?? Guid.Empty;
            if (responseObject is not null && responseObject.Value.TryGetProperty("id", out var dId)) doctorId = dId.GetGuid();
            if (responseObject is not null && responseObject.Value.TryGetProperty("employeeId", out var eId)) employeeId = eId.GetGuid();
            if (doctorId == Guid.Empty || employeeId == Guid.Empty || !await _identityAccounts.UpdateRolesAsync(request.UserId, roles, profile.PatientId, employeeId, doctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The doctor profile was created, but Identity role update is pending." });
            return PublicCreateResult(result);
        }

        [HttpPut("{doctorId}")]
        [Authorize(Policy = "Permission:doctor.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Update([FromRoute] Guid doctorId, [FromBody] UpdateDoctorRequest request)
        {
            var linkResponse = await _scheduling.SendAsync(HttpContext, HttpMethod.Get, $"doctors/{doctorId}/staff-link", null, HttpContext.RequestAborted);
            using var linkJson = linkResponse.ParseJson();
            if (linkResponse.StatusCode == StatusCodes.Status404NotFound || linkJson is null) return NotFound();
            if (!linkJson.RootElement.TryGetProperty("identityUserId", out var userIdElement) || userIdElement.ValueKind != System.Text.Json.JsonValueKind.String)
                return StatusCode(StatusCodes.Status409Conflict, new { message = "Doctor has no linked Identity account." });
            var userId = userIdElement.GetGuid();
            if (!await _identityAccounts.UpdateProfileAsync(userId, request.FullName, request.PhoneNumber, request.Email,
                    request.DateOfBirth, request.Gender, request.Address, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity profile update failed." });
            var profile = await _identityAccounts.GetUserProfileAsync(userId, HttpContext.RequestAborted);
            if (profile is null || !await _profileSync.SyncAsync(new Clinic.Contracts.Events.UserProfileUpdated(profile.PersonId,
                    profile.FullName, profile.PhoneNumber, profile.Email, profile.Gender, profile.DateOfBirth, profile.Address,
                    profile.PatientId, profile.UserId), HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity was updated, but profile projections are still pending." });
            var schedulingResponse = await _scheduling.SendAsync(HttpContext, HttpMethod.Put, $"doctors/{doctorId}", request, HttpContext.RequestAborted);
            return schedulingResponse.ToActionResult();
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:doctor.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateDoctorRequest request)
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
            var schedulingResponse = await _scheduling.SendAsync(HttpContext, HttpMethod.Put, "doctors/me", request, HttpContext.RequestAborted);
            return schedulingResponse.ToActionResult();
        }

        [HttpGet("{doctorId}/queue")]
        [Authorize(Policy = "Permission:doctor.queue.view,queue.view")]
        [ProducesResponseType(typeof(List<QueueTicketDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetQueue([FromRoute] Guid doctorId)
        {
            var queryStr = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
            return await _queueClient.ProxyAsync(HttpContext, $"/doctors/{doctorId}/queue{queryStr}", HttpMethod.Get);
        }

        [HttpPost("{doctorId}/queue/next")]
        [Authorize(Policy = "Permission:queue.call-next")]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/doctors/{doctorId}/queue/next", HttpMethod.Post);
        }

        private static IActionResult PublicCreateResult(SchedulingManagementResponse response)
        {
            var json = JsonNode.Parse(response.Content) as JsonObject;
            if (json is null) return response.ToActionResult();
            json.Remove("employeeId");
            return new ContentResult { StatusCode = response.StatusCode, ContentType = response.ContentType, Content = json.ToJsonString() };
        }
    }
}

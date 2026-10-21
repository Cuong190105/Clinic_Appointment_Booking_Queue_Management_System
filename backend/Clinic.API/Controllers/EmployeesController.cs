using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.Employees.Commands;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Features.Employees.Queries;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/employees")]
    public class EmployeesController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IdentityAccountClient _identityAccounts;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(IMapper mapper, ISender sender, ICurrentUser currentUser, IdentityAccountClient identityAccounts, ILogger<EmployeesController> logger)
        {
            _mapper = mapper;
            _sender = sender;
            _currentUser = currentUser;
            _identityAccounts = identityAccounts;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:employee.view.any")]
        [ProducesResponseType(typeof(PaginationResponse<EmployeeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] EmployeesQueryRequest request)
        {
            var query = _mapper.Map<GetEmployeesQuery>(request);
            var result = await _sender.Send(query);
            foreach (var employee in result.Items)
            {
                if (employee.UserId is Guid userId)
                {
                    var identity = await _identityAccounts.GetRolesAsync(userId, HttpContext.RequestAborted);
                    if (identity is not null) employee.Roles = identity.Value.Roles.ToList();
                }
            }
            return Ok(result);
        }

        [HttpGet("{employeeId}")]
        [Authorize(Policy = "Permission:employee.view.any")]
        [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid employeeId)
        {
            var query = _mapper.Map<GetEmployeeByIdQuery>(new { EmployeeId = employeeId });
            var result = await _sender.Send(query);
            if (result == null)
            {
                return NotFound();
            }
            if (result.UserId is Guid userId)
            {
                var identity = await _identityAccounts.GetRolesAsync(userId, HttpContext.RequestAborted);
                if (identity is not null) result.Roles = identity.Value.Roles.ToList();
            }
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:employee.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
        {
            var profileIds = await _sender.Send(new EnsureIdentityRegistrationProfileCommand(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address), HttpContext.RequestAborted);
            var identityUserId = Guid.NewGuid();
            var roles = request.Roles.Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var provisioned = await _identityAccounts.ProvisionAsync(identityUserId, profileIds.PersonId,
                request.Username, request.Password, request.FullName, request.PhoneNumber, request.Email,
                request.DateOfBirth, request.Gender, request.Address, roles, profileIds.PatientId, HttpContext.RequestAborted);
            if (provisioned is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity could not provision the employee account." });
            identityUserId = provisioned.UserId;
            EmployeeSummaryDto result;
            try
            {
                if (profileIds.EmployeeId is Guid existingEmployeeId)
                {
                    if (profileIds.DoctorId is not null) return Conflict(new { message = "This account already has a doctor profile." });
                    var existing = await _sender.Send(new GetEmployeeByIdQuery(existingEmployeeId), HttpContext.RequestAborted);
                    result = new EmployeeSummaryDto
                    {
                        Id = existing.Id, FullName = existing.FullName, PhoneNumber = existing.PhoneNumber,
                        Email = existing.Email, Gender = existing.Gender, DateOfBirth = existing.DateOfBirth,
                        Status = existing.Status, Roles = roles.ToList()
                    };
                }
                else
                {
                    var command = _mapper.Map<CreateEmployeeCommand>(request) with { IdentityUserId = identityUserId, IdentityPatientId = profileIds.PatientId };
                    result = await _sender.Send(command, HttpContext.RequestAborted);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Employee profile provisioning failed after Identity account {IdentityUserId} was created", identityUserId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity account was provisioned, but the employee profile is pending. Retry the request to resume provisioning." });
            }
            if (!await _identityAccounts.UpdateRolesAsync(identityUserId, roles, profileIds.PatientId, result.Id, null, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The employee profile was created, but Identity activation is pending." });
            return CreatedAtAction(nameof(GetById), new { employeeId = result.Id }, result);
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
            var command = _mapper.Map<CreateEmployeeFromUserCommand>(request);
            var result = await _sender.Send(command, HttpContext.RequestAborted);
            if (!await _identityAccounts.UpdateRolesAsync(request.UserId, roles, result.PatientId ?? current.Value.PatientId, result.Id, current.Value.DoctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The employee profile was created, but Identity role update is pending." });
            return CreatedAtAction(nameof(GetById), new { employeeId = result.Id }, result);
        }

        [HttpPut("{employeeId}")]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = "Permission:employee.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Update([FromRoute] Guid employeeId, [FromBody] AdminUpdateEmployeeRequest request)
        {
            var employee = await _sender.Send(new GetEmployeeByIdQuery(employeeId), HttpContext.RequestAborted);
            if (employee.UserId is not Guid employeeUserId) return StatusCode(StatusCodes.Status409Conflict, new { message = "The employee has no linked Identity account." });
            var identity = await _identityAccounts.GetRolesAsync(employeeUserId, HttpContext.RequestAborted);
            if (identity is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            var requestedRoles = request.Roles ?? identity.Value.Roles.ToList();
            var roles = requestedRoles.Concat(identity.Value.Roles.Where(x => x is "Doctor" or "Patient"))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!await _identityAccounts.RolesExistAsync(roles, HttpContext.RequestAborted)) return BadRequest(new { message = "One or more roles are not defined in Identity." });
            var userId = _currentUser.UserId;
            var command = new UpdateEmployeeCommand(userId ?? throw new UnauthorizedAccessException(),
                request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address, request.Roles, employeeId);
            await _sender.Send(command, HttpContext.RequestAborted);
            if (!await _identityAccounts.UpdateRolesAsync(employeeUserId, roles, identity.Value.PatientId, employeeId, identity.Value.DoctorId, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Employee details were saved, but Identity role update is pending." });
            return NoContent();
        }

        [HttpPost("{employeeId}/status")]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = "Permission:employee.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateStatus([FromRoute] Guid employeeId, [FromBody] EmployeeStatus status)
        {
            var command = new UpdateEmployeeStatusCommand(employeeId, status);
            await _sender.Send(command, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:employee.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateEmployeeRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new UpdateEmployeeCommand(userId ?? throw new UnauthorizedAccessException(),
                request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address, null);
            await _sender.Send(command, HttpContext.RequestAborted);
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize(Policy = "Permission:employee.view.own")]
        [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOwnProfile()
        {
            var employeeId = _currentUser.EmployeeId;
            var query = _mapper.Map<GetEmployeeByIdQuery>(new { EmployeeId = employeeId });
            var result = await _sender.Send(query);
            if (result == null)
            {
                return Unauthorized();
            }
            return Ok(result);
        }
    }
}

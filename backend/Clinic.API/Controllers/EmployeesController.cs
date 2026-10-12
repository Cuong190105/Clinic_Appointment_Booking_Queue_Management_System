using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.Employees.Commands;
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

        public EmployeesController(IMapper mapper, ISender sender, ICurrentUser currentUser)
        {
            _mapper = mapper;
            _sender = sender;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:employee.view.any")]
        [ProducesResponseType(typeof(PaginationResponse<EmployeeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] EmployeesQueryRequest request)
        {
            var query = _mapper.Map<GetEmployeesQuery>(request);
            var result = await _sender.Send(query);
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
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:employee.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
        {
            var command = _mapper.Map<CreateEmployeeCommand>(request);
            var result = await _sender.Send(command);
            return CreatedAtAction(nameof(GetById), new { employeeId = result.Id }, result);
        }

        [HttpPost("from-user")]
        [Authorize(Policy = "Permission:employee.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFromUser([FromBody] CreateEmployeeFromUserRequest request)
        {
            var command = _mapper.Map<CreateEmployeeFromUserCommand>(request);
            var result = await _sender.Send(command);
            return CreatedAtAction(nameof(GetById), new { employeeId = result.Id }, result);
        }

        [HttpPut("{employeeId}")]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = "Permission:employee.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Update([FromRoute] Guid employeeId, [FromBody] AdminUpdateEmployeeRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new UpdateEmployeeCommand(userId ?? throw new UnauthorizedAccessException(),
                request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address, request.Roles, employeeId);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("{employeeId}/status")]
        [Authorize(Roles = "Admin")]
        [Authorize(Policy = "Permission:employee.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateStatus([FromRoute] Guid employeeId, [FromBody] EmployeeStatus status)
        {
            var command = new UpdateEmployeeStatusCommand(employeeId, status);
            await _sender.Send(command);
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
            await _sender.Send(command);
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

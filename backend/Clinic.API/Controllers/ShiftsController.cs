using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/shifts")]
    public class ShiftsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public ShiftsController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:shift.self-manage")]
        [ProducesResponseType(typeof(DoctorScheduleDto<WorkScheduleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAllShifts([FromQuery] GetShiftsQuery query)
        {
            var doctorId = _currentUser.DoctorId;
            if (doctorId == null)
            {
                return Forbid();
            }
            var command = new GetDoctorSchedulesQuery(doctorId, query.StartDate, query.EndDate);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpPut("{shiftId}")]
        [Authorize(Policy = "Permission:shift.manage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Update([FromRoute] Guid shiftId, [FromBody] UpdateShiftRequest request)
        {
            var command = new UpdateWorkScheduleCommand(shiftId, request.Date, request.StartTime, request.EndTime, request.PatientLimitPerSlot);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("{shiftId}/cancel")]
        [Authorize(Policy = "Permission:shift.manage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Cancel([FromRoute] Guid shiftId, [FromBody] CancelShiftRequest request)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            var command = new CancelWorkScheduleCommand(userId, shiftId, request.Reason);
            await _sender.Send(command);
            return NoContent();
        }

        // Pls add a policy to allow doctor only for this endpoint
        [HttpPost("suggestions")]
        [Authorize(Policy = "Permission:shift.suggestion.self-manage")]
        [ProducesResponseType(typeof(RequestedShiftDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSuggestion([FromBody] CreateShiftSuggestionRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new AddDoctorShiftRequestCommand(userId, request.Date, request.StartTime, request.EndTime, request.PatientLimit, request.Reason);
            var result = await _sender.Send(command);
            return Created((string?)null, result);
        }

        [HttpPatch("suggestions/{suggestionId}")]
        [Authorize(Policy = "Permission:shift.suggestion.self-manage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PatchSuggestion([FromRoute] Guid suggestionId, [FromBody] UpdateShiftSuggestionRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new UpdateShiftRequestCommand(suggestionId, userId, request.Date, request.StartTime, request.EndTime, request.PatientLimitPerSlot, request.Reason);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("suggestions/{suggestionId}/cancel")]
        [Authorize(Policy = "Permission:shift.suggestion.self-manage")]
        public async Task<IActionResult> CancelSuggestion([FromRoute] Guid suggestionId)
        {
            var userId = _currentUser.UserId;
            var command = new CancelRequestCommand(suggestionId, userId);
            await _sender.Send(command);
            return NoContent();
        }


        [HttpPost("suggestions/{suggestionId}/approve")]
        [Authorize(Policy = "Permission:shift.suggestion.manage")]
        public async Task<IActionResult> ApproveSuggestion([FromRoute] Guid suggestionId)
        {
            var command = new ApproveRequestCommand(suggestionId);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("suggestions/{suggestionId}/reject")]
        [Authorize(Policy = "Permission:shift.suggestion.manage")]
        public async Task<IActionResult> RejectSuggestion([FromRoute] Guid suggestionId)
        {
            var command = new RejectRequestCommand(suggestionId);
            await _sender.Send(command);
            return NoContent();
        }

        // Pls add a policy to allow doctor only for this endpoint
        [HttpGet("suggestions")]
        [Authorize(Policy = "Permission:shift.suggestion.self-manage")]
        public async Task<IActionResult> GetAllSuggestions([FromQuery] GetShiftsQuery query)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not authenticated.");
            var command = new GetDoctorRequestedShiftsQuery(userId, query.StartDate, query.EndDate);
            var schedules = await _sender.Send(command);
            return Ok(schedules);
        }
    }
}
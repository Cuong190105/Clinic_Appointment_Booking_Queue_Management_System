using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Interfaces;
using MapsterMapper;
using Clinic.Application.Features.Queue.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public AppointmentsController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        [HttpGet("{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.view")]
        [ProducesResponseType(typeof(AppointmentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAppointment([FromRoute] Guid appointmentId)
        {
            var userId = _currentUser.UserId;
            if (userId == null)
                return Unauthorized();

            var command = new GetAppointmentByIdQuery(appointmentId, (Guid)userId);
            var appointment = await _sender.Send(command);
            return Ok(appointment);
        }


        // For patient only
        [HttpPost]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new CreateAppointmentCommand(userId, request.WorkScheduleId, request.TimeSlot, request.Reason, false);
            var result = await _sender.Send(command);
            return CreatedAtAction(nameof(GetAppointment), new { appointmentId = result.Id }, result);
        }

        // For patient only
        [HttpPatch("{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Patch([FromRoute] Guid appointmentId, [FromBody] UpdateAppointmentRequest request)
        {
            var userId = _currentUser.UserId;
            if (userId == null)
                return Unauthorized();
            var command = new UpdateAppointmentCommand((Guid)userId, appointmentId, request.NewWorkScheduleId, request.TimeSlot, request.Reason);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("{appointmentId}/confirm")]
        [Authorize(Policy = "Permission:appointment.confirm")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Confirm([FromRoute] Guid appointmentId)
        {
            var userId = _currentUser.UserId;
            if (userId == null)
                return Unauthorized();
            var command = new ConfirmAppointmentCommand(appointmentId, (Guid)userId);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPost("{appointmentId}/check-in")]
        [Authorize(Policy = "Permission:queue.check-in")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckIn([FromRoute] Guid appointmentId)
        {
            await _sender.Send(new CheckInCommand(appointmentId));
            return NoContent();
        }

        [HttpPost("{appointmentId}/cancel")]
        [Authorize(Policy = "Permission:appointment.edit.own,appointment.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Cancel([FromRoute] Guid appointmentId)
        {
            await _sender.Send(new CancelAppointmentCommand(appointmentId));
            return NoContent();
        }
    }

    [ApiController]
    [Route("/me/appointments")]
    public class MeAppointmentsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public MeAppointmentsController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:appointment.view")]
        [ProducesResponseType(typeof(IEnumerable<AppointmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine([FromQuery] string? category)
        {
            var userId = _currentUser.UserId;
            if (userId == null)
                return Unauthorized();

            var command = new GetPatientAppointmentsQuery((Guid)userId, category);
            var appointments = await _sender.Send(command);
            return Ok(appointments);
        }
    }
}
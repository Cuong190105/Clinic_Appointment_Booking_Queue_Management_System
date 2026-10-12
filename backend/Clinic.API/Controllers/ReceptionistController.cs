using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Patients.Commands;
using Clinic.Application.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/receptionist")]
    public class ReceptionistController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public ReceptionistController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        // For receptionist only
        [HttpPost("appointments")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateAppointmentForPatient([FromBody] ReceptionistCreateAppointmentRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new CreateAppointmentCommand(userId, request.WorkScheduleId, request.TimeSlot, request.Reason, request.IsWalkIn, request.PatientId);
            await _sender.Send(command);
            return StatusCode(StatusCodes.Status201Created);
        }

        [HttpGet("appointments/pending")]
        [Authorize(Roles = "Receptionist")]
        [ProducesResponseType(typeof(List<AppointmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPendingAppointments([FromQuery] PendingAppointmentRequest request)
        {
            var userId = _currentUser.UserId;
            if (userId == null)
                return Unauthorized();
            var command = _mapper.Map<GetPendingAppointmentsQuery>(request);
            var appointments = await _sender.Send(command);
            return Ok(appointments);
        }

        // For receptionist only
        [HttpPatch("appointments/{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.update")]
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

        [HttpPost("/patients/add")]
        [Authorize(Policy = "Permission:patient.create.any")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreatePatient([FromBody] ReceptionistCreatePatientRequest request)
        {
            var command = _mapper.Map<CreatePatientCommand>(request);
            var result = await _sender.Send(command);
            return CreatedAtAction(nameof(PatientsController.GetPatient), new { id = result.Id }, result);
        }

        [HttpPut("/patients/{patientId}")]
        [Authorize(Policy = "Permission:patient.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePatient([FromRoute] Guid patientId, [FromBody] ReceptionistUpdatePatientRequest request)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not found.");
            var command = new UpdatePatientCommand((Guid)userId, request.Email, request.Gender, request.Address, request.InsuranceNumber, request.EmergencyContact, patientId);
            await _sender.Send(command);
            return NoContent();
        }

        // Later can fill queue management, patient management, etc. for receptionist below
    }
}
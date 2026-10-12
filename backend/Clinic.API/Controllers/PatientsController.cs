using Clinic.API.Models;
using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Patients.Commands;
using Clinic.Application.Features.Patients.Queries;
using Clinic.Application.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/patients")]
    public class PatientsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public PatientsController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:patient.view.any")]
        [ProducesResponseType(typeof(PaginationResponse<PatientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPaged([FromQuery] GetPagedPatientsRequest request)
        {
            var command = _mapper.Map<GetPagedPatientsQuery>(request);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("{patientId}")]
        [Authorize(Policy = "Permission:patient.view.any")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPatient([FromRoute] Guid patientId)
        {
            var command = new GetPatientByIdQuery(patientId);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("{patientId}/appointments")]
        [HttpGet("{patientId}/appointments/upcoming")]
        [Authorize(Policy = "Permission:patient.view.any")]
        [ProducesResponseType(typeof(List<AppointmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPatientAppointments([FromRoute] Guid patientId)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            var command = new GetPatientAppointmentsQuery(userId, "upcoming", patientId);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("me")]
        [Authorize(Policy = "Permission:patient.view.own")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOwnPatient()
        {
            var patient = _currentUser.PatientId ?? throw new UnauthorizedAccessException();
            var command = new GetPatientByIdQuery(patient);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:patient.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdatePatient([FromBody] UpdatePatientRequest request)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            var command = new UpdatePatientCommand(userId, request.Email, request.Gender, request.Address, request.InsuranceNumber, request.EmergencyContact);
            await _sender.Send(command);
            return NoContent();
        }
    }
}

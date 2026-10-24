using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Patients.Commands;
using Clinic.Application.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/receptionist")]
    public class ReceptionistController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;
        private readonly AppointmentServiceClient _appointmentClient;
        private readonly IAppointmentRepository _appointmentRepository;

        public ReceptionistController(
            ISender sender,
            IMapper mapper,
            ICurrentUser currentUser,
            AppointmentServiceClient appointmentClient,
            IAppointmentRepository appointmentRepository)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
            _appointmentClient = appointmentClient;
            _appointmentRepository = appointmentRepository;
        }

        // Lấy danh sách bác sĩ đang có ca trực hôm nay (theo chuyên khoa)
        [HttpGet("working-doctors")]
        [Authorize(Roles = "Receptionist,Admin")]
        [ProducesResponseType(typeof(List<WorkingDoctorDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkingDoctors([FromQuery] Guid? specialtyId)
        {
            var result = await _sender.Send(new GetWorkingDoctorsForTodayQuery(specialtyId));
            return Ok(result);
        }

        // Đăng ký khám ngay (Walk-in) cho bệnh nhân tại quầy: tự động chọn bác sĩ ít tải nhất nếu không chọn, xác nhận, tự động check-in và sinh số thứ tự
        [HttpPost("appointments/walk-in")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(ReceptionistWalkInResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateWalkInAppointment([FromBody] ReceptionistWalkInRequest request)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            var command = new CreateWalkInAppointmentCommand(
                userId,
                request.PatientId,
                request.SpecialtyId,
                request.DoctorId,
                request.Reason,
                request.IsUrgent
            );

            var result = await _sender.Send(command);

            // Sync walk-in appointment to Clinic.Appointment.Service so ClinicAppointmentDB tracks it and publishes AppointmentCheckedIn
            var synced = await _appointmentClient.PostInternalAsync("internal/appointments/sync-walkin", new
            {
                appointmentId = result.AppointmentId,
                patientId = result.PatientId,
                patientName = result.PatientName,
                patientPhone = (string?)null,
                patientGender = (string?)null,
                patientDateOfBirth = (DateOnly?)null,
                workScheduleId = result.WorkScheduleId,
                doctorId = result.DoctorId,
                doctorName = result.DoctorName,
                specialtyName = result.SpecialtyName,
                scheduleDate = result.ScheduleDate,
                timeSlot = result.TimeSlot,
                reason = result.Reason,
                createdByUserId = userId,
                isUrgent = result.IsUrgent
            }, HttpContext.RequestAborted);

            if (!synced)
            {
                // The appointment is durable in ClinicDB. Keep its ID so the receptionist can retry
                // this idempotent projection without creating a second walk-in appointment.
                return Accepted(result);
            }

            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPost("appointments/walk-in/{appointmentId:guid}/sync")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RetryWalkInAppointmentSync([FromRoute] Guid appointmentId, [FromQuery] bool isUrgent = false)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null) return NotFound();
            if (!appointment.IsWalkIn || appointment.Status != Clinic.Domain.Enums.AppointmentStatus.CheckedIn)
            {
                return Conflict(new { error = "Only checked-in walk-in appointments can be synchronized." });
            }

            var person = appointment.Patient?.Person;
            var doctorName = appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? appointment.DoctorName;
            var synced = await _appointmentClient.PostInternalAsync("internal/appointments/sync-walkin", new
            {
                appointmentId = appointment.Id,
                patientId = appointment.PatientId,
                patientName = person?.FullName ?? string.Empty,
                patientPhone = person?.PhoneNumber,
                patientGender = person?.Gender.ToString(),
                patientDateOfBirth = person?.DateOfBirth,
                workScheduleId = appointment.WorkScheduleId,
                doctorId = appointment.DoctorId,
                doctorName,
                specialtyName = appointment.SpecialtyName,
                scheduleDate = appointment.ScheduleDate,
                timeSlot = appointment.TimeSlot,
                reason = appointment.Reason,
                createdByUserId = appointment.UpdatedByUserId,
                isUrgent
            }, HttpContext.RequestAborted);

            return synced
                ? Accepted(new { appointmentId, synced = true })
                : StatusCode(StatusCodes.Status503ServiceUnavailable, new { appointmentId, synced = false, retryable = true });
        }

        // For receptionist only
        [HttpPost("appointments")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateAppointmentForPatient([FromBody] ReceptionistCreateAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/appointments", HttpMethod.Post, request);
        }

        [HttpGet("appointments/pending")]
        [Authorize(Roles = "Receptionist")]
        [ProducesResponseType(typeof(List<AppointmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPendingAppointments([FromQuery] PendingAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/appointments/pending", HttpMethod.Get);
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
            return await _appointmentClient.ProxyAsync(HttpContext, $"receptionist/appointments/{appointmentId}", new HttpMethod("PATCH"), request);
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

            // Sync newly created patient profile into Clinic.Appointment.Service PatientProjections
            _ = await _appointmentClient.PostInternalAsync("internal/events/user-profile-updated", new
            {
                userId = result.Id,
                personId = result.Id,
                fullName = result.FullName ?? string.Empty,
                phoneNumber = result.PhoneNumber,
                email = result.Email,
                gender = result.Gender.ToString(),
                updatedAt = DateTimeOffset.UtcNow
            }, HttpContext.RequestAborted);

            return Created($"/patients/{result.Id}", result);
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
    }
}

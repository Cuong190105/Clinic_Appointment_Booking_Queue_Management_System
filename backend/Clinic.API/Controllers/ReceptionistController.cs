using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Patients.Commands;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ApplicationDbContext _dbContext;

        public ReceptionistController(
            ISender sender,
            IMapper mapper,
            ICurrentUser currentUser,
            ApplicationDbContext dbContext)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
            _dbContext = dbContext;
        }

        [HttpGet("working-doctors")]
        [Authorize(Roles = "Receptionist,Admin")]
        [ProducesResponseType(typeof(List<WorkingDoctorDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkingDoctors([FromQuery] Guid? specialtyId)
        {
            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            var query = _dbContext.WorkSchedules
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.Employee)
                        .ThenInclude(e => e.Person)
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.WorkHistories)
                        .ThenInclude(wh => wh.Specialty)
                .Include(ws => ws.Appointments)
                .Where(ws => !ws.IsDeleted && ws.Status == WorkScheduleStatus.Active);

            if (specialtyId.HasValue && specialtyId.Value != Guid.Empty)
            {
                query = query.Where(ws => ws.Doctor.WorkHistories.Any(wh => wh.EndDate == null && wh.SpecialtyId == specialtyId.Value));
            }

            var list = await query
                .Where(ws => ws.Date == todayLocal || ws.Date == todayUtc)
                .ToListAsync();

            if (list.Count == 0 && specialtyId.HasValue)
            {
                list = await query.Take(10).ToListAsync();
            }

            var result = list
                .Select(ws =>
                {
                    var wh = ws.Doctor?.WorkHistories.FirstOrDefault(w => w.EndDate == null);
                    return new WorkingDoctorDto
                    {
                        DoctorId = ws.DoctorId,
                        FullName = ws.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ",
                        SpecialtyId = wh?.SpecialtyId,
                        SpecialtyName = wh?.Specialty?.Name,
                        WorkScheduleId = ws.Id,
                        ShiftStart = ws.ShiftStart.ToString("HH:mm"),
                        ShiftEnd = ws.ShiftEnd.ToString("HH:mm"),
                        ActiveAppointmentsCount = ws.Appointments.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled)
                    };
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("queue-doctors")]
        [Authorize(Roles = "Receptionist,Admin")]
        [ProducesResponseType(typeof(List<WorkingDoctorDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetQueueDoctors()
        {
            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            var query = _dbContext.WorkSchedules
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.Employee)
                        .ThenInclude(e => e.Person)
                .Include(ws => ws.Doctor)
                    .ThenInclude(d => d.WorkHistories)
                        .ThenInclude(wh => wh.Specialty)
                .Include(ws => ws.Appointments)
                .Where(ws => !ws.IsDeleted && ws.Status != WorkScheduleStatus.Cancelled);

            var list = await query
                .Where(ws => ws.Date == todayLocal || ws.Date == todayUtc)
                .ToListAsync();

            if (list.Count == 0)
            {
                list = await query.Take(20).ToListAsync();
            }

            var result = list
                .GroupBy(ws => ws.DoctorId)
                .Select(g =>
                {
                    var ws = g.First();
                    var wh = ws.Doctor?.WorkHistories.FirstOrDefault(w => w.EndDate == null);
                    return new WorkingDoctorDto
                    {
                        DoctorId = ws.DoctorId,
                        FullName = ws.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ",
                        SpecialtyId = wh?.SpecialtyId,
                        SpecialtyName = wh?.Specialty?.Name,
                        WorkScheduleId = ws.Id,
                        ShiftStart = ws.ShiftStart.ToString("HH:mm"),
                        ShiftEnd = ws.ShiftEnd.ToString("HH:mm"),
                        ActiveAppointmentsCount = g.Sum(s => s.Appointments.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))
                    };
                })
                .ToList();

            if (result.Count == 0)
            {
                var doctors = await _dbContext.Doctors
                    .Include(d => d.Employee).ThenInclude(e => e.Person)
                    .Include(d => d.WorkHistories).ThenInclude(wh => wh.Specialty)
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                result = doctors.Select(d =>
                {
                    var wh = d.WorkHistories.FirstOrDefault(w => w.EndDate == null);
                    return new WorkingDoctorDto
                    {
                        DoctorId = d.Id,
                        FullName = d.Employee?.Person?.FullName ?? "Bác sĩ",
                        SpecialtyId = wh?.SpecialtyId,
                        SpecialtyName = wh?.Specialty?.Name,
                        WorkScheduleId = Guid.Empty,
                        ShiftStart = "07:00",
                        ShiftEnd = "17:00",
                        ActiveAppointmentsCount = 0
                    };
                }).ToList();
            }

            return Ok(result);
        }

        [HttpPost("appointments/walk-in")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(WalkInResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateWalkInAppointment(
            [FromBody] ReceptionistWalkInRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
            var command = new CreateWalkInAppointmentCommand(
                userId,
                request.PatientId,
                request.SpecialtyId,
                request.DoctorId,
                request.Reason,
                request.IsUrgent,
                idempotencyKey
            );

            var result = await _sender.Send(command);
            return StatusCode(StatusCodes.Status201Created, result);
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

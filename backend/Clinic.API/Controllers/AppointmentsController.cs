using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppointmentServiceClient _appointmentClient;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AppointmentsController(
            AppointmentServiceClient appointmentClient,
            IAppointmentRepository appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _appointmentClient = appointmentClient;
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.view")]
        [ProducesResponseType(typeof(AppointmentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAppointment([FromRoute] Guid appointmentId)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}", HttpMethod.Get);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(AppointmentDetailDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromBody] CreateAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "appointments", HttpMethod.Post, request);
        }

        [HttpPatch("{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.edit.own,appointment.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Patch([FromRoute] Guid appointmentId, [FromBody] UpdateAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}", new HttpMethod("PATCH"), request);
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
            return await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}/confirm", HttpMethod.Post);
        }

        [HttpPost("{appointmentId}/check-in")]
        [Authorize(Policy = "Permission:queue.check-in")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckIn(
            [FromRoute] Guid appointmentId,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromQuery] bool isUrgent = false)
        {
            var proxyResult = await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}/check-in", HttpMethod.Post);
            var statusCode = proxyResult switch
            {
                NoContentResult => StatusCodes.Status204NoContent,
                ContentResult cr => cr.StatusCode ?? StatusCodes.Status200OK,
                _ => StatusCodes.Status200OK
            };

            if (statusCode is >= 200 and < 300)
            {
                await EnsureQueueTicketInClinicDbAsync(appointmentId, isUrgent, HttpContext.RequestAborted);
            }

            return proxyResult;
        }

        private async Task EnsureQueueTicketInClinicDbAsync(Guid appointmentId, bool isUrgent, CancellationToken ct)
        {
            try
            {
                var existing = await _appointmentRepository.GetByIdAsync(appointmentId);
                if (existing != null)
                {
                    if (existing.QueueTicket == null)
                    {
                        var doctorId = existing.DoctorId != Guid.Empty ? existing.DoctorId : (existing.WorkSchedule?.DoctorId ?? Guid.Empty);
                        var checkInTime = DateTime.UtcNow;
                        var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(doctorId, checkInTime.Date, ct);
                        var ticket = new QueueTicket
                        {
                            AppointmentId = existing.Id,
                            QueueNumber = queueNumber,
                            CheckInTime = checkInTime,
                            Appointment = existing
                        };
                        if (isUrgent) ticket.SetPriority(true);
                        await _queueTicketRepository.AddAsync(ticket);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                    return;
                }

                using var doc = await _appointmentClient.GetJsonAsync(HttpContext, $"appointments/{appointmentId}", ct);
                if (doc == null) return;

                var root = doc.RootElement;
                var patientId = root.GetProperty("patientId").GetGuid();
                var doctorIdVal = root.GetProperty("doctorId").GetGuid();
                var workScheduleId = root.TryGetProperty("workScheduleId", out var wsEl) && wsEl.ValueKind != JsonValueKind.Null && wsEl.TryGetGuid(out var wsGuid)
                    ? wsGuid
                    : Guid.Empty;
                var doctorName = root.TryGetProperty("doctorName", out var dnEl) ? dnEl.GetString() ?? string.Empty : string.Empty;
                var specialtyName = root.TryGetProperty("specialtyName", out var snEl) ? snEl.GetString() : null;
                var dateStr = root.GetProperty("date").GetString()!;
                var timeStr = root.GetProperty("timeSlot").GetString()!;
                var reason = root.TryGetProperty("reason", out var rEl) ? rEl.GetString() ?? string.Empty : string.Empty;
                var isWalkIn = root.TryGetProperty("isWalkIn", out var wEl) && wEl.ValueKind == JsonValueKind.True;

                var appStub = new Appointment(
                    id: appointmentId,
                    patientId: patientId,
                    workScheduleId: workScheduleId,
                    doctorId: doctorIdVal,
                    doctorName: doctorName,
                    specialtyName: specialtyName,
                    scheduleDate: DateOnly.Parse(dateStr),
                    timeSlot: TimeOnly.Parse(timeStr),
                    reason: reason,
                    status: AppointmentStatus.CheckedIn,
                    isWalkIn: isWalkIn,
                    updatedByUserId: patientId,
                    createdAt: DateTime.UtcNow,
                    updatedAt: DateTime.UtcNow,
                    isDeleted: false);

                await _appointmentRepository.AddAsync(appStub);

                var now = DateTime.UtcNow;
                var qNum = await _queueTicketRepository.GetNextQueueNumberAsync(doctorIdVal, now.Date, ct);
                var qTicket = new QueueTicket
                {
                    AppointmentId = appStub.Id,
                    QueueNumber = qNum,
                    CheckInTime = now,
                    Appointment = appStub
                };
                if (isUrgent) qTicket.SetPriority(true);
                await _queueTicketRepository.AddAsync(qTicket);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch
            {
                // Best-effort transitional sync for Queue in ClinicDB
            }
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
            return await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}/cancel", HttpMethod.Post);
        }
    }

    [ApiController]
    [Route("/me/appointments")]
    public class MeAppointmentsController : ControllerBase
    {
        private readonly AppointmentServiceClient _appointmentClient;

        public MeAppointmentsController(AppointmentServiceClient appointmentClient)
        {
            _appointmentClient = appointmentClient;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:appointment.view")]
        [ProducesResponseType(typeof(IEnumerable<AppointmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine([FromQuery] string? category)
        {
            var query = string.IsNullOrWhiteSpace(category) ? string.Empty : $"?category={Uri.EscapeDataString(category)}";
            return await _appointmentClient.ProxyAsync(HttpContext, $"me/appointments{query}", HttpMethod.Get);
        }
    }
}

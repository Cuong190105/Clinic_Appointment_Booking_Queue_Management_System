using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Contracts;
using Clinic.Contracts.Events;
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
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxWriter? _outboxWriter;

        public AppointmentsController(
            AppointmentServiceClient appointmentClient,
            IAppointmentRepository appointmentRepository,
            IWorkScheduleRepository workScheduleRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork,
            IOutboxWriter? outboxWriter = null)
        {
            _appointmentClient = appointmentClient;
            _appointmentRepository = appointmentRepository;
            _workScheduleRepository = workScheduleRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
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
            var proxyResult = await _appointmentClient.ProxyAsync(HttpContext, "appointments", HttpMethod.Post, request);
            await EnsureAppointmentInClinicDbAsync(proxyResult, request, HttpContext.RequestAborted);
            return proxyResult;
        }

        private async Task EnsureAppointmentInClinicDbAsync(IActionResult proxyResult, CreateAppointmentRequest request, CancellationToken ct)
        {
            try
            {
                if (proxyResult is ContentResult cr && (cr.StatusCode == StatusCodes.Status201Created || cr.StatusCode == StatusCodes.Status200OK) && !string.IsNullOrEmpty(cr.Content))
                {
                    using var doc = JsonDocument.Parse(cr.Content);
                    var root = doc.RootElement.TryGetProperty("result", out var resEl) ? resEl : doc.RootElement;
                    if (root.TryGetProperty("id", out var idEl) && idEl.TryGetGuid(out var appId))
                    {
                        var existing = await _appointmentRepository.GetByIdAsync(appId);
                        if (existing == null)
                        {
                            var schedId = root.TryGetProperty("workScheduleId", out var wsEl) && wsEl.TryGetGuid(out var ws) ? ws : request.WorkScheduleId;
                            var patientId = root.TryGetProperty("patientId", out var pEl) && pEl.TryGetGuid(out var p) ? p : (_currentUser.PatientId ?? Guid.Empty);
                            var doctorId = root.TryGetProperty("doctorId", out var dEl) && dEl.TryGetGuid(out var d) ? d : Guid.Empty;
                            var dateStr = root.TryGetProperty("date", out var dtEl) ? dtEl.GetString() : null;
                            var timeStr = root.TryGetProperty("timeSlot", out var tmEl) ? tmEl.GetString() : null;
                            var reason = root.TryGetProperty("reason", out var rEl) ? rEl.GetString() ?? string.Empty : (request.Reason ?? string.Empty);
                            var docName = root.TryGetProperty("doctorName", out var dnEl) ? dnEl.GetString() ?? string.Empty : string.Empty;
                            var specName = root.TryGetProperty("specialtyName", out var snEl) ? snEl.GetString() : null;

                            var schedule = await _workScheduleRepository.GetByIdAsync(schedId);
                            if (schedule != null)
                            {
                                if (doctorId == Guid.Empty) doctorId = schedule.DoctorId;
                                if (string.IsNullOrEmpty(dateStr)) dateStr = schedule.Date.ToString("yyyy-MM-dd");
                            }

                            var app = new Appointment(
                                id: appId,
                                patientId: patientId,
                                workScheduleId: schedId,
                                doctorId: doctorId,
                                doctorName: docName,
                                specialtyName: specName,
                                scheduleDate: !string.IsNullOrEmpty(dateStr) ? DateOnly.Parse(dateStr) : DateOnly.FromDateTime(DateTime.UtcNow),
                                timeSlot: !string.IsNullOrEmpty(timeStr) ? TimeOnly.Parse(timeStr) : request.TimeSlot,
                                reason: reason,
                                status: AppointmentStatus.Pending,
                                isWalkIn: false,
                                updatedByUserId: _currentUser.UserId ?? patientId,
                                createdAt: DateTime.UtcNow,
                                updatedAt: DateTime.UtcNow,
                                isDeleted: false);

                            await _appointmentRepository.AddAsync(app);
                            if (schedule != null)
                            {
                                schedule.AddAppointment(app);
                            }
                            await _unitOfWork.SaveChangesAsync(ct);
                        }
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB for monolith consistency
            }
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
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing Idempotency-Key",
                    Detail = "Idempotency-Key header is required.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return await _appointmentClient.ProxyAsync(
                HttpContext,
                $"appointments/{appointmentId}/check-in?isUrgent={isUrgent.ToString().ToLowerInvariant()}",
                HttpMethod.Post);
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

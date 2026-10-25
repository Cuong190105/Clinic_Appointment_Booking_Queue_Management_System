using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppointmentServiceClient _appointmentClient;

        public AppointmentsController(AppointmentServiceClient appointmentClient)
        {
            _appointmentClient = appointmentClient;
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
            return await _appointmentClient.ProxyAsync(HttpContext, "appointments", HttpMethod.Post, request, HttpContext.RequestAborted);
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

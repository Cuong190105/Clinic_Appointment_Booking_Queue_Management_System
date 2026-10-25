using System.Net.Http.Json;
using System.Text.Json;
using Clinic.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/schedules/{scheduleId:guid}/reservations")]
public sealed class ReservationsController : ControllerBase
{
    private readonly HttpClient _schedulingClient;

    public ReservationsController(IHttpClientFactory httpClientFactory)
    {
        _schedulingClient = httpClientFactory.CreateClient("Scheduling");
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(
        [FromRoute] Guid scheduleId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] ReserveSlotRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"schedules/{scheduleId}/reservations")
        {
            Content = JsonContent.Create(new { timeSlot = request.TimeSlot, patientId = request.PatientId })
        };
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await _schedulingClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }

    [HttpPost("{reservationId:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(ReservationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm([FromRoute] Guid scheduleId, [FromRoute] Guid reservationId)
    {
        var response = await _schedulingClient.PostAsync($"schedules/{scheduleId}/reservations/{reservationId}/confirm", null);
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }

    [HttpPost("{reservationId:guid}/release")]
    [Authorize]
    [ProducesResponseType(typeof(ReservationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release([FromRoute] Guid scheduleId, [FromRoute] Guid reservationId)
    {
        var response = await _schedulingClient.PostAsync($"schedules/{scheduleId}/reservations/{reservationId}/release", null);
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }
}

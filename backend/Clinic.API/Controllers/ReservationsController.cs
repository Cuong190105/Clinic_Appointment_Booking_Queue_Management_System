using Clinic.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/schedules/{scheduleId:guid}/reservations")]
public sealed class ReservationsController : ControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Reserve(
        [FromRoute] Guid scheduleId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] ReserveSlotRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, new ReservationResponse(
            Guid.Empty, scheduleId, request.TimeSlot, DateTime.UtcNow.AddMinutes(10), "Reserved"));
    }

    [HttpPost("{reservationId:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(ReservationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Confirm([FromRoute] Guid scheduleId, [FromRoute] Guid reservationId)
    {
        return Ok(new ReservationActionResponse(reservationId, scheduleId, "Confirmed", DateTime.UtcNow));
    }

    [HttpPost("{reservationId:guid}/release")]
    [Authorize]
    [ProducesResponseType(typeof(ReservationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Release([FromRoute] Guid scheduleId, [FromRoute] Guid reservationId)
    {
        return Ok(new ReservationActionResponse(reservationId, scheduleId, "Released", DateTime.UtcNow));
    }
}

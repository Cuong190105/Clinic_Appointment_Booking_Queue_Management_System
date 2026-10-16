using Clinic.API.Models;
using Clinic.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/queue/tickets")]
public sealed class QueueContractController : ControllerBase
{
    [HttpPost("check-in")]
    [Authorize]
    [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult CheckIn(
        [FromQuery] Guid appointmentId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        return StatusCode(StatusCodes.Status201Created, new QueueTicketDto
        {
            Id = Guid.Empty,
            AppointmentId = appointmentId,
            QueueNumber = 0,
            Status = "Waiting",
            CheckInTime = DateTime.UtcNow
        });
    }

    [HttpPost("{ticketId:guid}/complete-exam")]
    [Authorize]
    [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult CompleteExam([FromRoute] Guid ticketId)
    {
        return Ok(new QueueTicketDto { Id = ticketId, Status = "Completed" });
    }
}

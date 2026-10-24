using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/queue")]
    public class QueueController : ControllerBase
    {
        private readonly QueueServiceClient _queueClient;

        public QueueController(QueueServiceClient queueClient)
        {
            _queueClient = queueClient;
        }

        [HttpPost("call-next/{doctorId}")]
        [Authorize(Policy = "Permission:queue.call-next")]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/call-next/{doctorId}", HttpMethod.Post);
        }

        [HttpPost("{queueTicketId}/start-exam")]
        [Authorize(Policy = "Permission:queue.start-exam.own,queue.start-exam.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> StartExam([FromRoute] Guid queueTicketId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/start-exam", HttpMethod.Post);
        }

        [HttpPost("{queueTicketId}/complete-exam")]
        [Authorize(Policy = "Permission:queue.complete-exam.own,queue.complete-exam.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> CompleteExam([FromRoute] Guid queueTicketId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/complete-exam", HttpMethod.Post);
        }

        [HttpPost("{queueTicketId}/skip")]
        [Authorize(Policy = "Permission:queue.skip")]
        [ProducesResponseType(typeof(SkipQueueResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Skip([FromRoute] Guid queueTicketId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/skip", HttpMethod.Post);
        }

        [HttpPatch("{queueTicketId}/priority")]
        [Authorize(Policy = "Permission:queue.priority")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SetPriority([FromRoute] Guid queueTicketId, [FromBody] SetQueuePriorityRequest request)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/priority", new HttpMethod("PATCH"), request);
        }

        [HttpGet("{queueTicketId}")]
        [Authorize]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid queueTicketId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}", HttpMethod.Get);
        }

        [HttpGet("appointment/{appointmentId}")]
        [Authorize]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByAppointmentId([FromRoute] Guid appointmentId)
        {
            return await _queueClient.ProxyAsync(HttpContext, $"/queue/appointment/{appointmentId}", HttpMethod.Get);
        }
    }

    public record SetQueuePriorityRequest(bool Priority);
}

using Clinic.Application.Features.Queue.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/queue")]
    public class QueueController : ControllerBase
    {
        private readonly ISender _sender;

        public QueueController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost("call-next/{doctorId}")]
        [Authorize(Policy = "Permission:queue.call-next")]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            var result = await _sender.Send(new CallNextQueueCommand(doctorId));
            return Ok(result);
        }

        [HttpPost("{queueTicketId}/start-exam")]
        [Authorize(Policy = "Permission:queue.start-exam.own,queue.start-exam.any")]
        public async Task<IActionResult> StartExam([FromRoute] Guid queueTicketId)
        {
            await _sender.Send(new StartExamCommand(queueTicketId));
            return NoContent();
        }

        [HttpPost("{queueTicketId}/complete-exam")]
        [Authorize(Policy = "Permission:queue.complete-exam.own,queue.complete-exam.any")]
        public async Task<IActionResult> CompleteExam([FromRoute] Guid queueTicketId)
        {
            await _sender.Send(new CompleteExamCommand(queueTicketId));
            return NoContent();
        }

        [HttpPost("{queueTicketId}/skip")]
        [Authorize(Policy = "Permission:queue.skip")]
        public async Task<IActionResult> Skip([FromRoute] Guid queueTicketId)
        {
            // Trả về 200 kèm SkipQueueResult (thay vì 204) vì FE cần biết bệnh nhân tiếp theo
            // có được tự động gọi thay cho lễ tân hay không, để cập nhật màn hình "đang gọi số".
            var result = await _sender.Send(new SkipQueueCommand(queueTicketId));
            return Ok(result);
        }

        [HttpPatch("{queueTicketId}/priority")]
        [Authorize(Policy = "Permission:queue.priority")]
        public async Task<IActionResult> SetPriority([FromRoute] Guid queueTicketId, [FromBody] SetQueuePriorityRequest request)
        {
            await _sender.Send(new SetQueuePriorityCommand(queueTicketId, request.Priority));
            return NoContent();
        }
    }

    public record SetQueuePriorityRequest(bool Priority);
}
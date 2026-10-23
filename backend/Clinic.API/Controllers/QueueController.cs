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
        private readonly IQueueTicketRepository? _queueTicketRepository;
        private readonly IUnitOfWork? _unitOfWork;

        public QueueController(
            QueueServiceClient queueClient,
            IQueueTicketRepository? queueTicketRepository = null,
            IUnitOfWork? unitOfWork = null)
        {
            _queueClient = queueClient;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("call-next/{doctorId}")]
        [Authorize(Policy = "Permission:queue.call-next")]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            var result = await _queueClient.ProxyAsync(HttpContext, $"/queue/call-next/{doctorId}", HttpMethod.Post);
            try
            {
                if (_queueTicketRepository != null && _unitOfWork != null)
                {
                    var nextTicket = await _queueTicketRepository.GetNextWaitingAsync(doctorId, DateTime.UtcNow);
                    if (nextTicket != null && nextTicket.Status == QueueStatus.Waiting)
                    {
                        nextTicket.Call();
                        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB
            }

            return result;
        }

        [HttpPost("{queueTicketId}/start-exam")]
        [Authorize(Policy = "Permission:queue.start-exam.own,queue.start-exam.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> StartExam([FromRoute] Guid queueTicketId)
        {
            var result = await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/start-exam", HttpMethod.Post);
            try
            {
                if (_queueTicketRepository != null && _unitOfWork != null)
                {
                    var ticket = await _queueTicketRepository.GetByIdAsync(queueTicketId);
                    if (ticket != null && ticket.Status != QueueStatus.InProgress)
                    {
                        if (ticket.Status == QueueStatus.Waiting)
                        {
                            ticket.Call();
                        }
                        if (ticket.Status == QueueStatus.Called)
                        {
                            ticket.StartExam();
                        }
                        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB
            }

            return result;
        }

        [HttpPost("{queueTicketId}/complete-exam")]
        [Authorize(Policy = "Permission:queue.complete-exam.own,queue.complete-exam.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> CompleteExam([FromRoute] Guid queueTicketId)
        {
            var result = await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/complete-exam", HttpMethod.Post);
            try
            {
                if (_queueTicketRepository != null && _unitOfWork != null)
                {
                    var ticket = await _queueTicketRepository.GetByIdAsync(queueTicketId);
                    if (ticket != null && ticket.Status != QueueStatus.Completed)
                    {
                        ticket.Complete();
                        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB
            }

            return result;
        }

        [HttpPost("{queueTicketId}/skip")]
        [Authorize(Policy = "Permission:queue.skip")]
        [ProducesResponseType(typeof(SkipQueueResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Skip([FromRoute] Guid queueTicketId)
        {
            var result = await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/skip", HttpMethod.Post);
            try
            {
                if (_queueTicketRepository != null && _unitOfWork != null)
                {
                    var ticket = await _queueTicketRepository.GetByIdAsync(queueTicketId);
                    if (ticket != null && ticket.Status != QueueStatus.Skipped)
                    {
                        ticket.Skip();
                        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB
            }

            return result;
        }

        [HttpPatch("{queueTicketId}/priority")]
        [Authorize(Policy = "Permission:queue.priority")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SetPriority([FromRoute] Guid queueTicketId, [FromBody] SetQueuePriorityRequest request)
        {
            var result = await _queueClient.ProxyAsync(HttpContext, $"/queue/{queueTicketId}/priority", new HttpMethod("PATCH"), request);
            try
            {
                if (_queueTicketRepository != null && _unitOfWork != null)
                {
                    var ticket = await _queueTicketRepository.GetByIdAsync(queueTicketId);
                    if (ticket != null)
                    {
                        ticket.SetPriority(request.Priority);
                        await _unitOfWork.SaveChangesAsync(HttpContext.RequestAborted);
                    }
                }
            }
            catch
            {
                // Best-effort cache in ClinicDB
            }

            return result;
        }
    }

    public record SetQueuePriorityRequest(bool Priority);
}

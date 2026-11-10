using Clinic.Application.Contracts;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/queue")]
    public class QueueController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IQueueTicketRepository _queueTicketRepository;

        public QueueController(ISender sender, IQueueTicketRepository queueTicketRepository)
        {
            _sender = sender;
            _queueTicketRepository = queueTicketRepository;
        }

        [HttpPost("call-next/{doctorId}")]
        [HttpPost("/doctors/{doctorId}/queue/next")]
        [Authorize(Policy = "Permission:queue.call-next")]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            var result = await _sender.Send(new CallNextQueueCommand(doctorId));
            return Ok(result);
        }

        [HttpPost("{queueTicketId}/call")]
        [Authorize(Policy = "Permission:queue.call-next")]
        public async Task<IActionResult> Call([FromRoute] Guid queueTicketId)
        {
            var result = await _sender.Send(new CallQueueTicketCommand(queueTicketId));
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
        [Authorize(Policy = "Permission:queue.skip,doctor.queue.view")]
        public async Task<IActionResult> Skip([FromRoute] Guid queueTicketId)
        {
            var result = await _sender.Send(new SkipQueueCommand(queueTicketId));
            return Ok(result);
        }

        [HttpPost("{queueTicketId}/retry")]
        [Authorize(Policy = "Permission:queue.skip")]
        public async Task<IActionResult> Retry([FromRoute] Guid queueTicketId)
        {
            var result = await _sender.Send(new RetrySkippedQueueCommand(queueTicketId));
            return Ok(result);
        }

        [HttpPatch("{queueTicketId}/priority")]
        [HttpPost("{queueTicketId}/priority")]
        [Authorize(Policy = "Permission:queue.priority")]
        public async Task<IActionResult> SetPriority([FromRoute] Guid queueTicketId, [FromBody] SetQueuePriorityRequest request)
        {
            await _sender.Send(new SetQueuePriorityCommand(queueTicketId, request.Priority));
            return NoContent();
        }

        [HttpGet("{queueTicketId}")]
        [Authorize]
        public async Task<IActionResult> GetById([FromRoute] Guid queueTicketId)
        {
            var ticket = await _queueTicketRepository.GetByIdAsync(queueTicketId);
            if (ticket == null) return NotFound();
            return Ok(MapToDto(ticket));
        }

        [HttpGet("appointment/{appointmentId}")]
        [Authorize]
        public async Task<IActionResult> GetByAppointmentId([FromRoute] Guid appointmentId)
        {
            var ticket = await _queueTicketRepository.GetByAppointmentIdAsync(appointmentId);
            if (ticket == null) return NotFound();
            return Ok(MapToDto(ticket));
        }

        private static QueueTicketDto MapToDto(Clinic.Domain.Entities.QueueTicket ticket)
        {
            return new QueueTicketDto
            {
                Id = ticket.Id,
                AppointmentId = ticket.AppointmentId,
                DoctorId = ticket.Appointment?.WorkSchedule?.DoctorId,
                QueueNumber = ticket.QueueNumber,
                Priority = ticket.Priority,
                Status = ticket.Status.ToString(),
                CheckInTime = ticket.CheckInTime,
                CalledAt = ticket.CalledAt,
                PatientName = ticket.PatientName ?? ticket.Appointment?.Patient?.Person?.FullName,
                DoctorName = ticket.DoctorName ?? ticket.Appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName
            };
        }
    }

    public record SetQueuePriorityRequest(bool Priority);
}

using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CallQueueTicketCommand(Guid QueueTicketId) : IRequest<QueueTicketDto>;

    public class CallQueueTicketHandler : IRequestHandler<CallQueueTicketCommand, QueueTicketDto>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CallQueueTicketHandler(
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<QueueTicketDto> Handle(CallQueueTicketCommand command, CancellationToken cancellationToken)
        {
            var ticket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            if (ticket.Status != QueueStatus.Waiting)
            {
                throw new ConflictException($"Cannot call patient from status '{ticket.Status}'. Must be Waiting.");
            }

            ticket.Call();
            await _queueTicketRepository.UpdateAsync(ticket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}

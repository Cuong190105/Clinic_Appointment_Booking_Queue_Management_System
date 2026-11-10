using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Queue.Commands
{
    public record RetrySkippedQueueCommand(Guid QueueTicketId) : IRequest<QueueTicketDto>;

    public class RetrySkippedQueueHandler : IRequestHandler<RetrySkippedQueueCommand, QueueTicketDto>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public RetrySkippedQueueHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<QueueTicketDto> Handle(RetrySkippedQueueCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            // Lễ tân hoặc Admin được retry ticket Skipped
            if (!_currentUser.IsInRole("Receptionist") && !_currentUser.IsInRole("Admin"))
            {
                throw new ForbiddenException("Chỉ lễ tân hoặc quản trị viên mới có quyền thử lại lượt khám đã bị bỏ qua.");
            }

            queueTicket.RetrySkipped();

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new QueueTicketDto
            {
                Id = queueTicket.Id,
                AppointmentId = queueTicket.AppointmentId,
                QueueNumber = queueTicket.QueueNumber,
                Priority = queueTicket.Priority,
                Status = queueTicket.Status.ToString(),
                CheckInTime = queueTicket.CheckInTime,
                CalledAt = queueTicket.CalledAt,
                PatientName = queueTicket.PatientName ?? queueTicket.Appointment?.Patient?.Person?.FullName
            };
        }
    }
}

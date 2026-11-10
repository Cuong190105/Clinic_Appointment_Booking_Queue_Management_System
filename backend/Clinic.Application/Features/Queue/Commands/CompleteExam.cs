using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CompleteExamCommand(Guid QueueTicketId) : IRequest;

    public class CompleteExamHandler : IRequestHandler<CompleteExamCommand>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteExamHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(CompleteExamCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            // Lễ tân không được bắt đầu hoặc hoàn thành lượt khám
            if (_currentUser.IsInRole("Receptionist") && !_currentUser.IsInRole("Admin"))
            {
                throw new ForbiddenException("Lễ tân không được bắt đầu hoặc hoàn thành lượt khám.");
            }

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.IsInRole("Admin");
            if (!hasAnyScope && queueTicket.Appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to complete exam for another doctor's queue.");
            }

            queueTicket.Complete();

            // Đồng bộ trạng thái Appointment: khi QueueTicket khám xong thì Appointment chuyển sang Completed
            var userId = _currentUser.UserId ?? queueTicket.Appointment.UpdatedByUserId;
            queueTicket.Appointment.Complete(userId);

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

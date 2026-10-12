using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record StartExamCommand(Guid QueueTicketId) : IRequest;

    /// <summary>
    /// Chuyển vé từ Called -> InProgress khi bệnh nhân đã vào phòng khám và bác sĩ
    /// bắt đầu khám. Sau bước này, GetActiveTicketAsync vẫn coi vé là "active"
    /// (bác sĩ vẫn đang bận), nên CallNextQueue vẫn bị chặn cho tới khi Complete/Skip.
    ///
    /// "any" scope (Admin/Receptionist): được bắt đầu khám cho vé thuộc hàng đợi của
    /// bất kỳ bác sĩ nào. Nếu không có "any", user chỉ được thao tác trên hàng đợi của
    /// chính mình ("own" scope, Doctor) - so DoctorId của vé với DoctorId của user hiện tại.
    /// </summary>
    public class StartExamHandler : IRequestHandler<StartExamCommand>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public StartExamHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(StartExamCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            var hasAnyScope = _currentUser.HasPermission("queue.start-exam.any");
            if (!hasAnyScope && queueTicket.Appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to start exam for another doctor's queue.");
            }

            queueTicket.StartExam();

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CompleteExamCommand(Guid QueueTicketId) : IRequest;

    /// <summary>
    /// Chuyển vé từ InProgress -> Completed khi bác sĩ khám xong. Giải phóng "chỗ" active
    /// của bác sĩ, cho phép CallNextQueue gọi vé tiếp theo.
    ///
    /// "any" scope (Admin/Receptionist): được hoàn tất khám cho vé thuộc hàng đợi của
    /// bất kỳ bác sĩ nào. Nếu không có "any", user chỉ được thao tác trên hàng đợi của
    /// chính mình ("own" scope, Doctor) - so DoctorId của vé với DoctorId của user hiện tại.
    /// </summary>
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

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any");
            if (!hasAnyScope && queueTicket.Appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to complete exam for another doctor's queue.");
            }

            queueTicket.Complete();
            // Đồng bộ trạng thái Appointment: khi QueueTicket khám xong thì Appointment
            // cũng phải chuyển CheckedIn -> Completed, nếu không lịch hẹn sẽ kẹt ở CheckedIn
            // mãi mãi dù đã khám xong (sai lịch sử khám bệnh + sai số liệu dashboard).
            queueTicket.Appointment.Complete(_currentUser.UserId ?? throw new UnauthorizedAccessException());

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
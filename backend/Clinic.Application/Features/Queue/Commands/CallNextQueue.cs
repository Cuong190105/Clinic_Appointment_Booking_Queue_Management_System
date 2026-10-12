using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CallNextQueueCommand(Guid DoctorId) : IRequest<QueueTicketDto>;

    public class CallNextQueueHandler : IRequestHandler<CallNextQueueCommand, QueueTicketDto>
    {
        private const int MaxRetries = 5;

        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CallNextQueueHandler(
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<QueueTicketDto> Handle(CallNextQueueCommand command, CancellationToken cancellationToken)
        {
            // Ràng buộc nghiệp vụ: mỗi bác sĩ chỉ được có tối đa 1 vé Called/InProgress
            // tại 1 thời điểm. Bấm "gọi số tiếp theo" liên tục khi bệnh nhân trước chưa
            // StartExam/Complete phải bị chặn ngay ở đây, không cho tạo thêm vé Called thứ 2.
            var activeTicket = await _queueTicketRepository.GetActiveTicketAsync(command.DoctorId, DateTime.UtcNow);
            if (activeTicket != null)
            {
                throw new ConflictException(
                    $"Bác sĩ đang có bệnh nhân số {activeTicket.QueueNumber} ở trạng thái " +
                    $"'{activeTicket.Status}'. Cần hoàn tất (Complete) hoặc bỏ qua (Skip) vé này " +
                    "trước khi gọi số tiếp theo.");
            }

            for (var attempt = 0; attempt < MaxRetries; attempt++)
            {
                var queueTicket = await _queueTicketRepository.GetNextWaitingAsync(command.DoctorId, DateTime.UtcNow)
                    ?? throw new ConflictException("There are no patients waiting in the queue for this doctor.");

                queueTicket.Call();

                await _queueTicketRepository.UpdateAsync(queueTicket);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConcurrencyConflictException)
                {
                    continue;
                }

                return new QueueTicketDto
                {
                    Id = queueTicket.Id,
                    AppointmentId = queueTicket.AppointmentId,
                    QueueNumber = queueTicket.QueueNumber,
                    Priority = queueTicket.Priority,
                    Status = queueTicket.Status.ToString(),
                    CheckInTime = queueTicket.CheckInTime,
                    CalledAt = queueTicket.CalledAt,
                    PatientName = queueTicket.Appointment.Patient?.Person?.FullName
                };
            }

            throw new ConflictException(
                "Không thể gọi số do xung đột dữ liệu liên tục, vui lòng thử lại.");
        }
    }
}
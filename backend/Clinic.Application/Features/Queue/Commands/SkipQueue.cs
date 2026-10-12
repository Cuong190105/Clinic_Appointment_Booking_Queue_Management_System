using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record SkipQueueCommand(Guid QueueTicketId) : IRequest<SkipQueueResult>;

    /// <summary>
    /// Kết quả bỏ qua 1 lượt khám. <see cref="NextCalledTicket"/> khác null khi vé bị bỏ qua
    /// đang giữ chỗ bác sĩ (Status = Called) và hệ thống đã tự động gọi số tiếp theo để
    /// "giải phóng bác sĩ" ngay sau đó; null nếu vé bị bỏ qua đang Waiting (bác sĩ chưa hề bận
    /// với vé này) hoặc hàng đợi hiện không còn ai đang chờ.
    /// </summary>
    public record SkipQueueResult(QueueTicketDto SkippedTicket, QueueTicketDto? NextCalledTicket);

    public class SkipQueueHandler : IRequestHandler<SkipQueueCommand, SkipQueueResult>
    {
        private const int MaxRetries = 5;

        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SkipQueueHandler(
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SkipQueueResult> Handle(SkipQueueCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            // Chỉ vé đang ở Called mới thực sự đang "giữ chỗ" bác sĩ (đã StartExam thì Skip() đã bị
            // chặn ở tầng Domain rồi). Vé Waiting bị skip thì bác sĩ chưa hề bận với vé này -> không
            // cần/không nên tự động gọi số tiếp theo thay cho lễ tân.
            var wasHoldingDoctor = queueTicket.Status == QueueStatus.Called;
            var doctorId = queueTicket.Appointment.WorkSchedule.DoctorId;
            var checkInDate = queueTicket.CheckInTime.Date;

            queueTicket.Skip();

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var skippedDto = ToDto(queueTicket);

            if (!wasHoldingDoctor)
            {
                return new SkipQueueResult(skippedDto, null);
            }

            // Bác sĩ vừa được giải phóng khỏi vé bị bỏ qua -> tự động gọi bệnh nhân tiếp theo (nếu còn).
            // Đây là hành động best-effort: nếu bước này thất bại (hết hàng đợi, hoặc xung đột concurrency
            // liên tục), việc Skip() ở trên vẫn được coi là thành công - lễ tân luôn có thể tự bấm
            // "Call next" lại sau. Không throw ra ngoài để tránh làm rollback thao tác Skip đã commit.
            for (var attempt = 0; attempt < MaxRetries; attempt++)
            {
                var nextTicket = await _queueTicketRepository.GetNextWaitingAsync(doctorId, checkInDate);
                if (nextTicket == null)
                {
                    return new SkipQueueResult(skippedDto, null);
                }

                nextTicket.Call();

                await _queueTicketRepository.UpdateAsync(nextTicket);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConcurrencyConflictException)
                {
                    continue;
                }

                return new SkipQueueResult(skippedDto, ToDto(nextTicket));
            }

            return new SkipQueueResult(skippedDto, null);
        }

        private static QueueTicketDto ToDto(Domain.Entities.QueueTicket queueTicket) => new()
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
}
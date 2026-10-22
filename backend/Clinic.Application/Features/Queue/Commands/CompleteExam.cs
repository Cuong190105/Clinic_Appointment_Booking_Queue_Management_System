using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
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
        private readonly IOutboxWriter _outboxWriter;
        private readonly IEventContext? _eventContext;

        public CompleteExamHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IEventContext? eventContext = null)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }

        public async Task Handle(CompleteExamCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            var doctorId = queueTicket.Appointment.DoctorId != Guid.Empty
                ? queueTicket.Appointment.DoctorId
                : (queueTicket.Appointment.WorkSchedule?.DoctorId ?? Guid.Empty);

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any");
            if (!hasAnyScope && doctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to complete exam for another doctor's queue.");
            }

            queueTicket.Complete();
            // Đồng bộ trạng thái Appointment: khi QueueTicket khám xong thì Appointment
            // cũng phải chuyển CheckedIn -> Completed, nếu không lịch hẹn sẽ kẹt ở CheckedIn
            // mãi mãi dù đã khám xong (sai lịch sử khám bệnh + sai số liệu dashboard).
            queueTicket.Appointment.Complete(_currentUser.UserId ?? throw new UnauthorizedAccessException());

            await _queueTicketRepository.UpdateAsync(queueTicket);

            var completedAt = DateTimeOffset.UtcNow;
            _outboxWriter.Add(
                new EventEnvelope<ExamCompleted>
                {
                    EventId = Guid.NewGuid(),
                    EventType = EventTypes.ExamCompleted,
                    SchemaVersion = 1,
                    OccurredAt = completedAt,
                    CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                    CausationId = _eventContext?.CausationId,
                    Producer = "queue-service",
                    Payload = new ExamCompleted(
                        // Transitional monolith mapping: AppointmentId is the stable visit key
                        // until the Encounter/Visit aggregate is introduced.
                        queueTicket.AppointmentId,
                        queueTicket.Id,
                        queueTicket.AppointmentId,
                        doctorId,
                        completedAt)
                },
                "queue.exam-completed");

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

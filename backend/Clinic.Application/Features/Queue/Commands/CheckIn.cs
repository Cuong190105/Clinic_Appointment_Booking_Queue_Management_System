using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CheckInCommand(Guid AppointmentId, Guid IdempotencyKey, bool IsUrgent = false) : IRequest;

    public class CheckInHandler : IRequestHandler<CheckInCommand>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IInboxStore _inboxStore;
        private readonly IEventContext? _eventContext;

        private const string IdempotencyConsumerName = "appointment-api.check-in";

        public CheckInHandler(
            IAppointmentRepository appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IInboxStore inboxStore,
            IEventContext? eventContext = null)
        {
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _inboxStore = inboxStore;
            _eventContext = eventContext;
        }

        public async Task Handle(CheckInCommand command, CancellationToken cancellationToken)
        {
            if (command.IdempotencyKey == Guid.Empty)
            {
                throw new ArgumentException("A valid Idempotency-Key is required.", nameof(command.IdempotencyKey));
            }

            await _unitOfWork.InitializeTransactionLockAsync(cancellationToken);
            try
            {
                var idempotencyMarker = CreateIdempotencyMarker(command);
                var idempotencyDecision = await _inboxStore.TryBeginProcessingAsync(
                    idempotencyMarker,
                    IdempotencyConsumerName,
                    cancellationToken);

                if (idempotencyDecision == InboxDecision.AlreadyProcessed)
                {
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);
                    return;
                }

                if (idempotencyDecision == InboxDecision.CurrentlyProcessing)
                {
                    throw new ConflictException("This check-in request is already being processed.");
                }

                var appointment = await _appointmentRepository.GetByIdAsync(command.AppointmentId)
                    ?? throw new NotFoundException($"Appointment '{command.AppointmentId}' not found.");

                var doctorId = appointment.DoctorId != Guid.Empty ? appointment.DoctorId : (appointment.WorkSchedule?.DoctorId ?? Guid.Empty);
                var checkInTime = DateTime.UtcNow;
                var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(doctorId, checkInTime.Date, cancellationToken);
                var queueTicket = appointment.CheckIn(checkInTime, queueNumber, command.IsUrgent);

                await _queueTicketRepository.AddAsync(queueTicket);

                _outboxWriter.Add(
                    new EventEnvelope<AppointmentCheckedIn>
                    {
                        EventId = Guid.NewGuid(),
                        EventType = EventTypes.AppointmentCheckedIn,
                        SchemaVersion = 1,
                        OccurredAt = checkInTime,
                        CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                        CausationId = _eventContext?.CausationId,
                        Producer = "appointment-service",
                        Payload = new AppointmentCheckedIn(
                            appointment.Id,
                            appointment.PatientId,
                            doctorId,
                            // Transitional monolith mapping: AppointmentId is the stable visit key
                            // until the Encounter/Visit aggregate is introduced.
                            appointment.Id,
                            checkInTime)
                    },
                    "appointment.checked-in");

                _inboxStore.MarkProcessed(
                    idempotencyMarker.EventId,
                    IdempotencyConsumerName,
                    DateTimeOffset.UtcNow);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        private static EventEnvelope<CheckInIdempotencyMarker> CreateIdempotencyMarker(CheckInCommand command)
        {
            var input = Encoding.UTF8.GetBytes($"{command.AppointmentId:N}:{command.IdempotencyKey:N}");
            var hash = SHA256.HashData(input);
            var eventId = new Guid(hash[..16]);

            return new EventEnvelope<CheckInIdempotencyMarker>
            {
                EventId = eventId,
                EventType = "appointment.check-in",
                SchemaVersion = 1,
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = eventId,
                Producer = "clinic-api",
                Payload = new CheckInIdempotencyMarker(command.AppointmentId)
            };
        }

        private sealed record CheckInIdempotencyMarker(Guid AppointmentId);
    }
}

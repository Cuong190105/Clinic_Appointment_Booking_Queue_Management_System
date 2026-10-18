using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record CheckInCommand(Guid AppointmentId) : IRequest;

    public class CheckInHandler : IRequestHandler<CheckInCommand>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IEventContext? _eventContext;

        public CheckInHandler(
            IAppointmentRepository appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IEventContext? eventContext = null)
        {
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }

        public async Task Handle(CheckInCommand command, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(command.AppointmentId)
                ?? throw new NotFoundException($"Appointment '{command.AppointmentId}' not found.");


            var doctorId = appointment.WorkSchedule.DoctorId;
            var checkInTime = DateTime.UtcNow;
            var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(doctorId, checkInTime.Date, cancellationToken);
            var queueTicket = appointment.CheckIn(checkInTime, queueNumber);

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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

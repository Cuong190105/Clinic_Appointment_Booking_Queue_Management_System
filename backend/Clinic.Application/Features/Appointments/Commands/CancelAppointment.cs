using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Commands
{
    // Use-case: Patient or Receptionist cancels an appointment
    public record CancelAppointmentCommand(
        Guid AppointmentId
    ) : IRequest<Guid>;
    public class CancelAppointmentCommandHandler : IRequestHandler<CancelAppointmentCommand, Guid>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IEventContext? _eventContext;

        public CancelAppointmentCommandHandler(IAppointmentRepository appointmentRepository, ICurrentUser currentUser, IUnitOfWork unitOfWork, IOutboxWriter outboxWriter, IEventContext? eventContext = null)
        {
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }
        public async Task<Guid> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId == null)
            {
                throw new UnauthorizedAccessException();
            }
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }
            if (!(_currentUser.HasPermission("appointment.edit.any") || _currentUser.PatientId == appointment.PatientId))
            {
                throw new ForbiddenException("You are not authorized to cancel this appointment.");
            }
            appointment.Cancel((Guid)_currentUser.UserId);

            _outboxWriter.Add(
                new EventEnvelope<AppointmentCancelled>
                {
                    EventId = Guid.NewGuid(),
                    EventType = EventTypes.AppointmentCancelled,
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                    CausationId = _eventContext?.CausationId,
                    Producer = "appointment-service",
                    Payload = new AppointmentCancelled(
                        appointment.Id,
                        appointment.PatientId,
                        _currentUser.UserId,
                        DateTimeOffset.UtcNow)
                },
                "appointment.cancelled");

            await _unitOfWork.SaveChangesAsync();
            return request.AppointmentId;
        }
    }
}

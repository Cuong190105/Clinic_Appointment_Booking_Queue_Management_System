using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin cancels a work schedule for a doctor, with a reason for cancellation to notify the doctor and patients
    public record CancelWorkScheduleCommand(
        Guid UserId,
        Guid Id,
        string Reason
    ) : IRequest<Guid>;
    public class CancelWorkScheduleCommandHandler : IRequestHandler<CancelWorkScheduleCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationQueue _notificationQueue;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IEventContext? _eventContext;
        public CancelWorkScheduleCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork, INotificationQueue notificationQueue, IOutboxWriter outboxWriter, IEventContext? eventContext = null)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
            _notificationQueue = notificationQueue;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }

        public async Task<Guid> Handle(CancelWorkScheduleCommand request, CancellationToken cancellationToken)
        {
            var workSchedule = await _workScheduleRepository.GetWorkScheduleByIdAsync(request.Id);
            if (workSchedule == null) throw new NotFoundException("Work schedule not found.");

            workSchedule.Cancel(request.Reason);
            _outboxWriter.Add(
                new EventEnvelope<WorkScheduleCancelled>
                {
                    EventId = Guid.NewGuid(),
                    EventType = EventTypes.WorkScheduleCancelled,
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                    CausationId = _eventContext?.CausationId,
                    Producer = "scheduling-service",
                    Payload = new WorkScheduleCancelled(workSchedule.Id, workSchedule.DoctorId, request.Reason, DateTimeOffset.UtcNow)
                },
                "schedule.cancelled");
            foreach (var appointment in workSchedule.Appointments)
            {
                appointment.AdminCancel(request.UserId);
                await _notificationQueue.EnqueueAsync(new NotificationJob<Appointment>(
                    appointment.Patient.Person,
                    appointment,
                    NotificationType.AppointmentCancellation,
                    true,
                    true,
                    true));
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return workSchedule.Id;
        }
    }
}

using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin updates an existing work schedule for a doctor
    public record UpdateWorkScheduleCommand(
        Guid Id,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        int PatientLimit
    ) : IRequest<Guid>;
    public class UpdateWorkScheduleCommandHandler : IRequestHandler<UpdateWorkScheduleCommand, Guid>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IEventContext? _eventContext;
        public UpdateWorkScheduleCommandHandler(IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork, IOutboxWriter outboxWriter, IEventContext? eventContext = null)
        {
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }

        public async Task<Guid> Handle(UpdateWorkScheduleCommand request, CancellationToken cancellationToken)
        {
            var workSchedule = await _workScheduleRepository.GetWorkScheduleByIdAsync(request.Id);
            if (workSchedule == null)
            {
                throw new NotFoundException("Work schedule not found");
            }
            var hasOverlappingSchedules = await _workScheduleRepository.HasOverlappingWorkSchedule(workSchedule.DoctorId, request.Date, request.StartTime, request.EndTime, workSchedule.Id);
            if (hasOverlappingSchedules)
            {
                throw new InvalidOperationException("The updated schedule overlaps with another existing schedule.");
            }
            workSchedule.UpdateShift(request.Date, request.StartTime, request.EndTime, request.PatientLimit);
            _outboxWriter.Add(
                new EventEnvelope<WorkScheduleUpdated>
                {
                    EventId = Guid.NewGuid(),
                    EventType = EventTypes.WorkScheduleUpdated,
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                    CausationId = _eventContext?.CausationId,
                    Producer = "scheduling-service",
                    Payload = new WorkScheduleUpdated(workSchedule.Id, workSchedule.DoctorId, request.Date, request.StartTime, request.EndTime, request.PatientLimit)
                },
                "schedule.updated");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return workSchedule.Id;
        }
    }
}

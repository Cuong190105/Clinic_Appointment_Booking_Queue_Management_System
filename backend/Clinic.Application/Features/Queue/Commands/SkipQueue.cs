using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Queue.Commands
{
    public record SkipQueueCommand(Guid QueueTicketId) : IRequest<SkipQueueResult>;

    public record SkipQueueResult(QueueTicketDto SkippedTicket, QueueTicketDto? NextCalledTicket);

    public class SkipQueueHandler : IRequestHandler<SkipQueueCommand, SkipQueueResult>
    {
        private const int MaxRetries = 5;

        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser? _currentUser;
        private readonly IUnitOfWork? _unitOfWork;

        public SkipQueueHandler(
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
            : this(queueTicketRepository, null, unitOfWork)
        {
        }

        public SkipQueueHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser? currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<SkipQueueResult> Handle(SkipQueueCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            // Bác sĩ chỉ được bỏ qua ticket đã Called và chưa bắt đầu khám; không được bỏ qua ticket chưa gọi (Waiting).
            if (_currentUser != null && _currentUser.IsInRole("Doctor") && !_currentUser.IsInRole("Admin") && !_currentUser.IsInRole("Receptionist"))
            {
                if (queueTicket.Status != QueueStatus.Called)
                {
                    throw new ConflictException("Bác sĩ chỉ được bỏ qua lượt khám đã được gọi (Called) và chưa bắt đầu khám.");
                }

                if (queueTicket.Appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
                {
                    throw new ForbiddenException("Bác sĩ không thể bỏ qua lượt khám của bác sĩ khác.");
                }
            }

            var wasHoldingDoctor = queueTicket.Status == QueueStatus.Called;
            var doctorId = queueTicket.Appointment.WorkSchedule.DoctorId;
            var checkInDate = queueTicket.CheckInTime.Date;

            queueTicket.Skip();

            await _queueTicketRepository.UpdateAsync(queueTicket);
            if (_unitOfWork != null)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var skippedDto = ToDto(queueTicket);

            if (!wasHoldingDoctor)
            {
                return new SkipQueueResult(skippedDto, null);
            }

            for (var attempt = 0; attempt < MaxRetries; attempt++)
            {
                var nextTicket = await _queueTicketRepository.GetNextWaitingAsync(doctorId, checkInDate);
                if (nextTicket == null)
                {
                    return new SkipQueueResult(skippedDto, null);
                }

                nextTicket.Call();

                await _queueTicketRepository.UpdateAsync(nextTicket);

                if (_unitOfWork != null)
                {
                    try
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (ConcurrencyConflictException)
                    {
                        continue;
                    }
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
            PatientName = queueTicket.PatientName ?? queueTicket.Appointment.Patient?.Person?.FullName,
            DoctorName = queueTicket.DoctorName ?? queueTicket.Appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName
        };
    }
}

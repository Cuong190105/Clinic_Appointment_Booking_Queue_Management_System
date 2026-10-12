using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
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

        public CheckInHandler(
            IAppointmentRepository appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
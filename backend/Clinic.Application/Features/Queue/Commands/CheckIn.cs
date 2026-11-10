using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            // Mỗi appointment chỉ tạo tối đa một queue ticket, kể cả khi request được retry.
            if (appointment.Status == AppointmentStatus.CheckedIn || appointment.QueueTicket != null)
            {
                throw new ArgumentException("Lịch hẹn đã được check-in trước đó.");
            }

            // Chỉ cho phép check-in đúng ngày và đúng ca đã đặt
            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            if (appointment.WorkSchedule.Date != todayUtc && appointment.WorkSchedule.Date != todayLocal)
            {
                throw new ConflictException(
                    $"Không thể check-in: Lịch hẹn được đặt cho ngày {appointment.WorkSchedule.Date:yyyy-MM-dd}, không phải hôm nay.");
            }

            var doctorId = appointment.WorkSchedule.DoctorId;
            var checkInTime = DateTime.UtcNow;
            var queueDate = appointment.WorkSchedule.Date;
            var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(doctorId, queueDate.ToDateTime(TimeOnly.MinValue), cancellationToken);

            var queueTicket = appointment.CheckIn(checkInTime, queueNumber);
            queueTicket.QueueDate = queueDate;
            queueTicket.PatientName = appointment.Patient?.Person?.FullName;
            queueTicket.DoctorName = appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName;

            try
            {
                await _queueTicketRepository.AddAsync(queueTicket);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex.InnerException?.Message.Contains("IX_QueueTickets_AppointmentId") == true ||
                                      ex.Message.Contains("IX_QueueTickets_AppointmentId"))
            {
                throw new ConflictException("Lịch hẹn đã được check-in trước đó.");
            }
        }
    }
}

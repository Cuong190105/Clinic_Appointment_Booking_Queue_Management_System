using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Commands
{
    public record ConfirmAppointmentCommand(Guid AppointmentId, Guid ConfirmedByUserId) : IRequest<Guid>;
    public class ConfirmAppointmentCommandHandler : IRequestHandler<ConfirmAppointmentCommand, Guid>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly INotificationQueue _notificationQueue;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;

        public ConfirmAppointmentCommandHandler(IAppointmentRepository appointmentRepository, INotificationQueue notificationQueue, INotificationService notificationService, IUnitOfWork unitOfWork)
        {
            _appointmentRepository = appointmentRepository;
            _notificationService = notificationService;
            _notificationQueue = notificationQueue;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(ConfirmAppointmentCommand request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }
            appointment.Confirm(request.ConfirmedByUserId);
            await _unitOfWork.SaveChangesAsync();
            await _notificationQueue.EnqueueAsync(new NotificationJob<Appointment>(
                appointment.Patient.Person,
                appointment,
                NotificationType.AppointmentConfirmation,
                SendEmail: true,
                SendInApp: true,
                SendSms: false
            ));

            // Remind the patient about the appointment one day before at 8 AM (UTC+7)
            var notificationScheduledAt = new DateTimeOffset(
                appointment.WorkSchedule.Date.AddDays(-1),
                new TimeOnly(8, 0, 0),
                TimeSpan.FromHours(7));

            await _notificationService.ScheduleAsync(new NotificationJob<Appointment>(
                appointment.Patient.Person,
                appointment,
                NotificationType.AppointmentReminder,
                SendEmail: true,
                SendInApp: true,
                SendSms: false
            ), notificationScheduledAt);
            return request.AppointmentId;
        }
    }
}

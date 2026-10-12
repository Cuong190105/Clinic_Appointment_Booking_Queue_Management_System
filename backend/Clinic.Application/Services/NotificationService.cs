using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Interfaces;
using Clinic.Domain.Entities;

namespace Clinic.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAppointmentNotificationHandler _appointmentHandler;
        private readonly IDueNotificationHandler _dueHandler ;
        private readonly IAccountNotificationHandler _accountNotificationHandler;
        private readonly ICustomNotificationHandler _customNotificationHandler;

        public NotificationService(
            IAppointmentNotificationHandler appointmentHandler,
            IDueNotificationHandler dueHandler,
            IAccountNotificationHandler accountNotificationHandler,
            ICustomNotificationHandler customNotificationHandler,
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _dueHandler = dueHandler;
            _appointmentHandler = appointmentHandler;
            _accountNotificationHandler = accountNotificationHandler;
            _customNotificationHandler = customNotificationHandler;
        }

        public async Task SendAsync(
            INotificationJob job,
            CancellationToken cancellationToken = default)
        {
            switch (job)
            {
                case NotificationJob<Appointment> appointmentJob:
                    await _appointmentHandler.HandleAsync(appointmentJob, cancellationToken);
                    break;
                case NotificationJob<Account> accountJob:
                    await _accountNotificationHandler.HandleAsync(accountJob, cancellationToken);
                    break;
                case NotificationJob<CustomNotificationData> customNotificationJob:
                    await _customNotificationHandler.HandleAsync(customNotificationJob, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Notification job type '{job.GetType().Name}' is not supported.");
            }

            // Each dispatcher will handle the adding of the notification to the repository
            // Save the notification to the repository
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ScheduleAsync(
            INotificationJob job,
            DateTimeOffset scheduledTime,
            CancellationToken cancellationToken = default)
        {
            switch (job)
            {
                case NotificationJob<Appointment> appointmentJob:
                    await _appointmentHandler.HandleAsync(appointmentJob, cancellationToken, scheduledTime);
                    break;
                default:
                    throw new NotSupportedException($"Notification job type '{job.GetType().Name}' is not supported.");
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ProcessDueNotificationsAsync(CancellationToken stoppingToken)
        {
            await _dueHandler.ProcessAsync(stoppingToken);
            await _unitOfWork.SaveChangesAsync(stoppingToken);
        }
    }
}

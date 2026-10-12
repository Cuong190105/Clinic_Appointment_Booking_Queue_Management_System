using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Interfaces;
using Clinic.Application.Notifications.Templates;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Notifications.Dispatchers
{
    public class AppointmentNotificationHandler : IAppointmentNotificationHandler
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly IInAppSender _inAppSender;

        public AppointmentNotificationHandler(
            INotificationRepository notificationRepository,
            IEmailSender emailSender,
            ISmsSender smsSender,
            IInAppSender inAppSender)
        {
            _notificationRepository = notificationRepository;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _inAppSender = inAppSender;
        }

        public async Task HandleAsync(NotificationJob<Appointment> appointmentJob, CancellationToken cancellationToken = default, DateTimeOffset? scheduledTime = null)
        {
            if (appointmentJob.SendInApp && appointmentJob.Person.User != null)
            {
                InAppContent content = appointmentJob.NotificationType switch
                {
                    NotificationType.AppointmentConfirmation => new AppointmentConfirmationTemplate().RenderInApp(appointmentJob.Data),
                    NotificationType.AppointmentReminder => new AppointmentReminderTemplate().RenderInApp(appointmentJob.Data),
                    NotificationType.AppointmentCancellation => new AppointmentCancelledTemplate().RenderInApp(appointmentJob.Data),
                    _ => throw new ArgumentOutOfRangeException()
                };
                var notification = new Notification(
                        appointmentJob.Person.Id,
                        appointmentJob.NotificationType,
                        content.Title,
                        content.Message,
                        NotificationChannel.InApp,
                        scheduledTime
                    );
                await _notificationRepository.AddAsync(notification, cancellationToken);

                // For scheduling notifications, don't send the notification immediately if the scheduled time is in the future
                if (scheduledTime.HasValue && scheduledTime.Value > DateTimeOffset.Now)
                {
                    return;
                }
                await _inAppSender.SendAsync(appointmentJob.Person.User.Id, content.Title, content.Message, cancellationToken);
                notification.MarkAsSent();
            }
            if (appointmentJob.SendEmail && appointmentJob.Person.Email != null)
            {
                EmailContent content = appointmentJob.NotificationType switch
                {
                    NotificationType.AppointmentConfirmation => new AppointmentConfirmationTemplate().RenderEmail(appointmentJob.Data),
                    NotificationType.AppointmentReminder => new AppointmentReminderTemplate().RenderEmail(appointmentJob.Data),
                    NotificationType.AppointmentCancellation => new AppointmentCancelledTemplate().RenderEmail(appointmentJob.Data),
                    _ => throw new ArgumentOutOfRangeException()
                };
                var notification = new Notification(
                        appointmentJob.Person.Id,
                        appointmentJob.NotificationType,
                        content.Subject,
                        content.Body,
                        NotificationChannel.Email,
                        scheduledTime
                    );
                await _notificationRepository.AddAsync(notification, cancellationToken);
                try
                {
                    // For scheduling notifications, don't send the notification immediately if the scheduled time is in the future
                    if (scheduledTime.HasValue && scheduledTime.Value > DateTimeOffset.Now)
                    {
                        return;
                    }
                    await _emailSender.SendAsync(appointmentJob.Person.Email, content.Subject, content.Body, cancellationToken);
                    notification.MarkAsSent();
                }
                catch
                {
                    notification.MarkAsFailed();
                }
            }
            if (appointmentJob.SendSms && appointmentJob.Person.PhoneNumber != null)
            {
                SmsContent content = appointmentJob.NotificationType switch
                {
                    NotificationType.AppointmentConfirmation => new AppointmentConfirmationTemplate().RenderSms(appointmentJob.Data),
                    NotificationType.AppointmentReminder => new AppointmentReminderTemplate().RenderSms(appointmentJob.Data),
                    NotificationType.AppointmentCancellation => new AppointmentCancelledTemplate().RenderSms(appointmentJob.Data),
                    _ => throw new ArgumentOutOfRangeException()
                };
                var notification = new Notification(
                        appointmentJob.Person.Id,
                        appointmentJob.NotificationType,
                        "<NO TITLE>",
                        content.Message,
                        NotificationChannel.Sms,
                        scheduledTime
                    );
                await _notificationRepository.AddAsync(notification, cancellationToken);
                try
                {
                    // For scheduling notifications, don't send the notification immediately if the scheduled time is in the future
                    if (scheduledTime.HasValue && scheduledTime.Value > DateTimeOffset.Now)
                    {
                        return;
                    }
                    await _smsSender.SendAsync(appointmentJob.Person.PhoneNumber, content.Message, cancellationToken);
                    notification.MarkAsSent();
                }
                catch
                {
                    notification.MarkAsFailed();
                }
            }
        }
    }
}

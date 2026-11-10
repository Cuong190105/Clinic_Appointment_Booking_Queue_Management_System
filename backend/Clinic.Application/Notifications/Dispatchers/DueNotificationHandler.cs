using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Interfaces;
using Clinic.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Notifications.Dispatchers
{
    public class DueNotificationHandler : IDueNotificationHandler
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly IInAppSender _inAppSender;

        public DueNotificationHandler(
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

        public async Task ProcessAsync(CancellationToken stoppingToken)
        {
            var now = DateTimeOffset.UtcNow;
            var scheduledNotifications = await _notificationRepository.GetAllScheduledAndFailedByAsync(now, stoppingToken);

            foreach (var notification in scheduledNotifications)
            {
                if (notification.Status == NotificationStatus.Sent)
                {
                    continue; // Skip deduplicated sent notification
                }

                if (notification.DeliveryAttempts >= 3)
                {
                    continue; // Max retries exceeded
                }

                switch (notification.Channel)
                {
                    case NotificationChannel.Email:
                        if (string.IsNullOrWhiteSpace(notification.Person.Email))
                        {
                            notification.IncrementDeliveryAttempts("Missing recipient email.");
                            continue;
                        }
                        try
                        {
                            await _emailSender.SendAsync(notification.Person.Email, notification.Title, notification.Message, stoppingToken);
                            notification.MarkAsSent();
                        }
                        catch (Exception ex)
                        {
                            notification.IncrementDeliveryAttempts(ex.Message);
                        }
                        break;

                    case NotificationChannel.Sms:
                        if (string.IsNullOrWhiteSpace(notification.Person.PhoneNumber))
                        {
                            notification.IncrementDeliveryAttempts("Missing recipient phone number.");
                            continue;
                        }
                        try
                        {
                            await _smsSender.SendAsync(notification.Person.PhoneNumber, notification.Message, stoppingToken);
                            notification.MarkAsSent();
                        }
                        catch (Exception ex)
                        {
                            notification.IncrementDeliveryAttempts(ex.Message);
                        }
                        break;

                    case NotificationChannel.InApp:
                        if (notification.Person.User == null)
                        {
                            notification.IncrementDeliveryAttempts("Person has no registered user account.");
                            continue;
                        }
                        try
                        {
                            await _inAppSender.SendAsync(notification.Person.User.Id, notification.Title, notification.Message, stoppingToken);
                            notification.MarkAsSent();
                        }
                        catch (Exception ex)
                        {
                            notification.IncrementDeliveryAttempts(ex.Message);
                        }
                        break;
                }
            }
        }
    }
}

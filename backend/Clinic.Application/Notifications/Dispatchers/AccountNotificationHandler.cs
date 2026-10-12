using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Interfaces;
using Clinic.Application.Notifications.Templates;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Dispatchers
{
    public class AccountNotificationHandler : IAccountNotificationHandler
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly IInAppSender _inAppSender;

        public AccountNotificationHandler(
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
        public async Task HandleAsync(NotificationJob<Account> notificationJob, CancellationToken cancellationToken = default, DateTimeOffset? scheduledTime = null)
        {
            if (notificationJob.SendInApp && notificationJob.Person.User != null)
            {
                //InAppContent content = new AccountNotificationTemplate().RenderInApp(notificationJob.Data);
                //var notification = new Notification(
                //        notificationJob.Person,
                //        notificationJob.NotificationType,
                //        content.Title,
                //        content.Message,
                //        NotificationChannel.InApp,
                //        scheduledTime
                //    );

                //await _notificationRepository.AddAsync(notification, cancellationToken);

                //// For scheduling notifications, don't send the notification immediately if the scheduled time is in the future
                //if (scheduledTime.HasValue && scheduledTime.Value > DateTimeOffset.Now)
                //{
                //    return;
                //}

                //await _inAppSender.SendAsync(notificationJob.Person.User.Id, content.Title, content.Message, cancellationToken);
                //notification.MarkAsSent();
            }
            if (notificationJob.SendEmail && notificationJob.Person.Email != null)
            {
                EmailContent content = notificationJob.NotificationType switch
                {
                    NotificationType.ResetPassword => new ResetPasswordTemplate().RenderEmail(notificationJob.Data),
                    _ => throw new InvalidOperationException()
                };
                var notification = new Notification(
                        notificationJob.Person.Id,
                        notificationJob.NotificationType,
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
                    await _emailSender.SendAsync(notificationJob.Person.Email, content.Subject, content.Body, cancellationToken);
                    notification.MarkAsSent();
                }
                catch
                {
                    notification.MarkAsFailed();
                }
            }
            if (notificationJob.SendSms && notificationJob.Person.PhoneNumber != null)
            {
                SmsContent content = notificationJob.NotificationType switch
                {
                    NotificationType.ResetPassword => new ResetPasswordTemplate().RenderSms(notificationJob.Data),
                    _ => throw new InvalidOperationException()
                };
                var notification = new Notification(
                        notificationJob.Person.Id,
                        notificationJob.NotificationType,
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
                    await _smsSender.SendAsync(notificationJob.Person.PhoneNumber, content.Message, cancellationToken);
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

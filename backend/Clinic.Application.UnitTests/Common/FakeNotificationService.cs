using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Common
{
    internal class FakeNotificationService : INotificationService
    {
        private readonly List<Notification> _notifications = new List<Notification>();
        public Task ProcessDueNotificationsAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }

        public async Task ScheduleAsync(INotificationJob job, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default)
        {
            var channel = job.SendEmail ? Domain.Enums.NotificationChannel.Email : (job.SendSms ? Domain.Enums.NotificationChannel.Sms : Domain.Enums.NotificationChannel.InApp);
            _notifications.Add(new Notification(job.Person.Id, job.NotificationType, "title", "message", channel));
            await Task.CompletedTask;
        }

        public Task SendAsync(INotificationJob job, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}

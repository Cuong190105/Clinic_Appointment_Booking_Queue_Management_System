using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Interfaces
{
    public interface ICustomNotificationHandler
    {
        /// <summary>
        /// Send custom notifications to users based on the provided notification type and data.
        /// </summary>
        Task HandleAsync(NotificationJob<CustomNotificationData> notificationJob, CancellationToken stoppingToken = default, DateTimeOffset? scheduledTime = null);
    }
}

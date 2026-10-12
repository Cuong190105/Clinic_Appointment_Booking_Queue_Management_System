using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Interfaces
{
    public interface IAccountNotificationHandler
    {
        /// <summary>
        /// Handles the notification job for an account, sending notifications based on the specified notification type and channels (email, in-app, SMS).
        /// </summary>
        /// <returns></returns>
        Task HandleAsync(NotificationJob<Account> accountJob, CancellationToken cancellationToken = default, DateTimeOffset? scheduledTime = null);
    }
}

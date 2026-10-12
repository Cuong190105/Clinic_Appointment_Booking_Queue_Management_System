using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Interfaces
{
    public interface IDueNotificationHandler
    {
        /// <summary>
        /// Scans and send notifications
        /// </summary>
        Task ProcessAsync(CancellationToken stoppingToken);
    }
}

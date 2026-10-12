using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Interfaces
{
    public interface IAppointmentNotificationHandler
    {
        /// <summary>
        /// Handles the notification job for an appointment, sending notifications based on the specified notification type and channels (email, in-app, SMS).
        /// </summary>
        /// <returns></returns>
        Task HandleAsync(NotificationJob<Appointment> appointmentJob, CancellationToken cancellationToken = default, DateTimeOffset? scheduledTime = null);
    }
}

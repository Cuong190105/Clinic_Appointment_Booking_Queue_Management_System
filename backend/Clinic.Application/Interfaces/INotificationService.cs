namespace Clinic.Application.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Scans and sends all due & failed notifications that are scheduled to be sent at the current time or earlier. This method is typically called by a background worker to process pending notifications.
        /// </summary>
        Task ProcessDueNotificationsAsync(CancellationToken stoppingToken);

        /// <summary>
        /// Sends a notification job to the appropriate channels (email, in-app, SMS) based on the job's configuration.
        /// Shouldn't be called directly by the user; instead, use the INotificationQueue.Enqueue method to schedule notifications for sending.
        /// </summary>
        Task SendAsync(
            INotificationJob job,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedules a notification job to be sent at a specific time in the future.
        /// </summary>
        Task ScheduleAsync(
            INotificationJob job,
            DateTimeOffset scheduledTime,
            CancellationToken cancellationToken = default);
    }
}

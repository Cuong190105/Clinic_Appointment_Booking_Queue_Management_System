using Clinic.Application.Common.Models;
using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface INotificationRepository
    {
        /// <summary>
        /// Adds a new notification to the repository.
        /// </summary>
        Task AddAsync(Notification notification, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves all notifications that are scheduled to be sent at or before the specified time, as well as any notifications that have failed to send.
        /// Tracks all the notifications for updating their status after processing.
        /// </summary>
        Task<IEnumerable<Notification>> GetAllScheduledAndFailedByAsync(DateTimeOffset time, CancellationToken stoppingToken);

        /// <summary>
        /// Retrieves a notification by its unique identifier, including the associated person and user information for lookup and resend.
        /// </summary>
        Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves a list of notifications for a specific person that are created older than the specified date and time, limited to the specified number of results.
        /// </summary>
        Task<PagedResult<Notification>> GetNotificationsForPerson(Guid? personId, DateTime createdBefore, int limit);
    }
}

using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Interfaces
{
    public interface INotificationJob
    {
        Person Person { get; }
        NotificationType NotificationType { get; }
        bool SendEmail { get; }
        bool SendInApp { get; }
        bool SendSms { get; }
    }
    public record NotificationJob<T>(
        Person Person,
        T Data,
        NotificationType NotificationType,
        bool SendEmail,
        bool SendInApp,
        bool SendSms
    ) : INotificationJob;

    public record CustomNotificationData(
        string FullName,
        string Title,
        string Message
    );

    public interface INotificationQueue
    {
        /// <summary>
        /// Enqueues a notification job to the queue asynchronously.
        /// </summary>
        /// <param name="job"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask EnqueueAsync(
            INotificationJob job,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads all notification jobs from the queue asynchronously.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<INotificationJob> ReadAllAsync(CancellationToken cancellationToken = default);
    }
}

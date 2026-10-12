using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Gắn theo PersonId (không phải UserId) để gửi được cho cả khách vãng lai
    /// chưa từng tạo tài khoản đăng nhập.
    /// </summary>
    public class Notification : BaseEntity
    {
        public Guid PersonId { get; protected set; }
        public Person Person { get; protected set; } = null!;

        public NotificationType Type { get; protected set; }

        public string Title { get; protected set; } = string.Empty;
        public string Message { get; protected set; } = string.Empty;

        public bool? IsRead { get; protected set; } = null;

        public NotificationChannel Channel { get; protected set; }

        public DateTime? SendTime { get; protected set; }

        public NotificationStatus Status { get; protected set; } = NotificationStatus.Pending;

        public DateTimeOffset? ScheduledAt { get; protected set; }

        public Notification(Guid personId, NotificationType type, string title, string message, NotificationChannel channel, DateTimeOffset? scheduledTime = null)
        {
            PersonId = personId;
            Type = type;
            Title = title;
            Message = message;
            Channel = channel;
            ScheduledAt = scheduledTime;
        }

        /// Private constructor for EF Core
        private Notification() { }

        public void MarkAsRead()
        {
            IsRead = true;
        }

        public void MarkAsSent()
        {
            Status = NotificationStatus.Sent;
            SendTime = DateTime.UtcNow;
        }

        public void MarkAsFailed()
        {
            Status = NotificationStatus.Failed;
        }

        public void CancelSchedule()
        {
            if (Status == NotificationStatus.Pending && ScheduledAt.HasValue)
            {
                Status = NotificationStatus.Cancelled;
                ScheduledAt = null;
            }
        }
    }
}

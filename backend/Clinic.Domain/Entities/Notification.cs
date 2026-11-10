using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Gáº¯n theo PersonId (khÃ´ng pháº£i UserId) Ä‘á»ƒ gá»­i Ä‘Æ°á»£c cho cáº£ khÃ¡ch vÃ£ng lai
    /// chÆ°a tá»«ng táº¡o tÃ i khoáº£n Ä‘Äƒng nháº­p.
    /// </summary>
    public class Notification : BaseEntity
    {
        public Guid PersonId { get; protected set; }
        public Person Person { get; set; } = null!;

        public NotificationType Type { get; protected set; }

        public string Title { get; protected set; } = string.Empty;
        public string Message { get; protected set; } = string.Empty;

        public bool? IsRead { get; protected set; } = null;

        public NotificationChannel Channel { get; protected set; }

        public DateTime? SendTime { get; protected set; }

        public NotificationStatus Status { get; protected set; } = NotificationStatus.Pending;

        public DateTimeOffset? ScheduledAt { get; protected set; }

        public int DeliveryAttempts { get; protected set; } = 0;

        public string? FailureReason { get; protected set; }

        public string? IdempotencyKey { get; set; }

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

        public void MarkAsFailed(string? failureReason = null)
        {
            Status = NotificationStatus.Failed;
            FailureReason = failureReason;
            MarkUpdated();
        }

        public void IncrementDeliveryAttempts(string? failureReason = null)
        {
            DeliveryAttempts++;
            FailureReason = failureReason;
            if (DeliveryAttempts >= 3)
            {
                Status = NotificationStatus.Failed;
            }
            MarkUpdated();
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

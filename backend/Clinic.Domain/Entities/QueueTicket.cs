using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Sá»‘ thá»© tá»± hÃ ng Ä‘á»£i khÃ¡m bá»‡nh.
    /// </summary>
    public class QueueTicket : BaseEntity
    {
        /// <summary>FK, NOT NULL, UNIQUE - 1 Appointment chá»‰ sinh tá»‘i Ä‘a 1 QueueTicket.</summary>
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public int QueueNumber { get; set; }

        public bool Priority { get; private set; } = false;

        public QueueStatus Status { get; private set; } = QueueStatus.Waiting;

        public DateOnly QueueDate { get; set; }

        public string? PatientName { get; set; }

        public string? DoctorName { get; set; }

        /// <summary>Thá»i Ä‘iá»ƒm bá»‡nh nhÃ¢n check-in, sinh sá»‘ thá»© tá»±.</summary>
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

        public DateTime? CalledAt { get; set; }

        /// <summary>
        /// Concurrency token (SQL Server rowversion) - EF Core tá»± Ä‘á»™ng kiá»ƒm tra giÃ¡ trá»‹ nÃ y
        /// chÆ°a Ä‘á»•i trÆ°á»›c khi UPDATE. Náº¿u request khÃ¡c Ä‘Ã£ ghi Ä‘Ã¨ báº£n ghi nÃ y trÆ°á»›c,
        /// SaveChangesAsync sáº½ nÃ©m DbUpdateConcurrencyException thay vÃ¬ ghi Ä‘Ã¨ Ã¢m tháº§m.
        /// </summary>
        public byte[] RowVersion { get; set; } = null!;

        /// <summary>0..1 - chá»‰ cÃ³ sau khi bÃ¡c sÄ© khÃ¡m xong.</summary>
        public MedicalReport? MedicalReport { get; set; }

        public QueueTicket() { }

        public QueueTicket(
            Guid id,
            Guid appointmentId,
            int queueNumber,
            bool priority,
            QueueStatus status,
            DateTime checkInTime,
            DateTime? calledAt,
            DateTime createdAt,
            DateTime? updatedAt,
            bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            AppointmentId = appointmentId;
            QueueNumber = queueNumber;
            Priority = priority;
            Status = status;
            CheckInTime = checkInTime;
            CalledAt = calledAt;
        }

        /// <summary>
        /// Gá»i bá»‡nh nhÃ¢n vÃ o khÃ¡m. Chá»‰ cho phÃ©p gá»i khi Ä‘ang á»Ÿ tráº¡ng thÃ¡i Waiting
        /// (chÆ°a Ä‘Æ°á»£c gá»i láº§n nÃ o, hoáº·c Ä‘Ã£ reset láº¡i tá»« 1 lÆ°á»£t gá»i trÆ°á»›c).
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void Call()
        {
            if (Status != QueueStatus.Waiting)
            {
                throw new ArgumentException($"Cannot call a queue ticket with status '{Status}'.");
            }

            Status = QueueStatus.Called;
            CalledAt = DateTime.UtcNow;
            MarkUpdated();
        }

        /// <summary>
        /// Bá» qua lÆ°á»£t khÃ¡m (bá»‡nh nhÃ¢n khÃ´ng cÃ³ máº·t khi Ä‘Æ°á»£c gá»i, hoáº·c lá»… tÃ¢n chá»§ Ä‘á»™ng bá» qua
        /// trong khi váº«n Ä‘ang chá»). Chá»‰ cho phÃ©p khi vÃ© Ä‘ang á»Ÿ Waiting hoáº·c Called.
        /// KhÃ´ng cho phÃ©p bá» qua khi Ä‘ang khÃ¡m (InProgress) - trÆ°á»ng há»£p nÃ y pháº£i dÃ¹ng Complete();
        /// cÅ©ng khÃ´ng cho phÃ©p khi Ä‘Ã£ khÃ¡m xong (Completed) hoáº·c Ä‘Ã£ bá»‹ bá» qua trÆ°á»›c Ä‘Ã³ (Skipped).
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void Skip()
        {
            if (Status is not (QueueStatus.Waiting or QueueStatus.Called))
            {
                throw new ArgumentException($"Cannot skip a queue ticket with status '{Status}'.");
            }

            Status = QueueStatus.Skipped;
            MarkUpdated();
        }

        public void RetrySkipped()
        {
            if (Status != QueueStatus.Skipped)
            {
                throw new InvalidOperationException($"Cannot retry a ticket with status '{Status}'. Only skipped tickets can be retried.");
            }

            Status = QueueStatus.Waiting;
            MarkUpdated();
        }

        /// <summary>
        /// Báº¯t Ä‘áº§u khÃ¡m cho bá»‡nh nhÃ¢n vá»«a Ä‘Æ°á»£c gá»i. Chá»‰ há»£p lá»‡ khi vÃ© Ä‘ang á»Ÿ tráº¡ng thÃ¡i Called
        /// (Ä‘Ã£ gá»i sá»‘ nhÆ°ng chÆ°a báº¯t Ä‘áº§u khÃ¡m).
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void StartExam()
        {
            if (Status != QueueStatus.Called)
            {
                throw new ArgumentException($"Cannot start exam for a queue ticket with status '{Status}'.");
            }

            Status = QueueStatus.InProgress;
            MarkUpdated();
        }

        /// <summary>
        /// HoÃ n táº¥t lÆ°á»£t khÃ¡m. Chá»‰ há»£p lá»‡ khi vÃ© Ä‘ang á»Ÿ tráº¡ng thÃ¡i InProgress (Ä‘ang khÃ¡m).
        /// Sau bÆ°á»›c nÃ y, bÃ¡c sÄ© Ä‘Æ°á»£c coi lÃ  "ráº£nh" vÃ  cÃ³ thá»ƒ gá»i sá»‘ tiáº¿p theo.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void Complete()
        {
            if (Status != QueueStatus.InProgress)
            {
                throw new ArgumentException($"Cannot complete a queue ticket with status '{Status}'.");
            }

            Status = QueueStatus.Completed;
            MarkUpdated();
        }

        /// <summary>
        /// Há»§y vÃ© hÃ ng Ä‘á»£i vÃ¬ Appointment tÆ°Æ¡ng á»©ng bá»‹ há»§y sau khi bá»‡nh nhÃ¢n Ä‘Ã£ check-in.
        /// Chá»‰ cho phÃ©p khi vÃ© cÃ²n Waiting (chÆ°a tá»«ng Ä‘Æ°á»£c gá»i) hoáº·c Called (Ä‘Ã£ gá»i nhÆ°ng
        /// bá»‡nh nhÃ¢n chÆ°a vÃ o phÃ²ng khÃ¡m) - Ä‘Ã¢y lÃ  2 tráº¡ng thÃ¡i mÃ  viá»‡c há»§y cÃ²n Ã½ nghÄ©a vÃ 
        /// khÃ´ng lÃ m giÃ¡n Ä‘oáº¡n 1 lÆ°á»£t khÃ¡m Ä‘ang diá»…n ra.
        /// KhÃ´ng cho phÃ©p há»§y khi InProgress (bÃ¡c sÄ© Ä‘ang khÃ¡m dá»Ÿ dang - há»§y lÃºc nÃ y vÃ´ nghÄ©a
        /// vÃ  cÃ³ thá»ƒ lÃ m máº¥t dá»¯ liá»‡u khÃ¡m Ä‘ang thá»±c hiá»‡n) hoáº·c khi vÃ© Ä‘Ã£ á»Ÿ tráº¡ng thÃ¡i káº¿t thÃºc
        /// (Completed/Skipped/Cancelled).
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void Cancel()
        {
            if (Status is not (QueueStatus.Waiting or QueueStatus.Called))
            {
                throw new ArgumentException($"Cannot cancel a queue ticket with status '{Status}'.");
            }

            Status = QueueStatus.Cancelled;
            MarkUpdated();
        }

        /// <summary>
        /// ÄÃ¡nh dáº¥u (hoáº·c gá»¡ Ä‘Ã¡nh dáº¥u) Æ°u tiÃªn kháº©n cáº¥p. Chá»‰ Ã¡p dá»¥ng khi vÃ© cÃ²n Ä‘ang chá» (Waiting) -
        /// Æ°u tiÃªn chá»‰ cÃ³ Ã½ nghÄ©a thay Ä‘á»•i thá»© tá»± cá»§a lÆ°á»£t gá»i tiáº¿p theo, khÃ´ng cÃ²n tÃ¡c dá»¥ng
        /// khi Ä‘Ã£ Ä‘Æ°á»£c gá»i/Ä‘ang khÃ¡m/khÃ¡m xong/bá»‹ bá» qua.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void SetPriority(bool priority)
        {
            if (Status != QueueStatus.Waiting)
            {
                throw new ArgumentException($"Cannot change priority of a queue ticket with status '{Status}'.");
            }

            Priority = priority;
            MarkUpdated();
        }
    }
}
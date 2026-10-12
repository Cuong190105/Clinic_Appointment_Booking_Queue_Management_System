using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Số thứ tự hàng đợi khám bệnh.
    /// </summary>
    public class QueueTicket : BaseEntity
    {
        /// <summary>FK, NOT NULL, UNIQUE - 1 Appointment chỉ sinh tối đa 1 QueueTicket.</summary>
        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public int QueueNumber { get; set; }

        public bool Priority { get; private set; } = false;

        public QueueStatus Status { get; private set; } = QueueStatus.Waiting;

        /// <summary>Thời điểm bệnh nhân check-in, sinh số thứ tự.</summary>
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

        public DateTime? CalledAt { get; set; }

        /// <summary>
        /// Concurrency token (SQL Server rowversion) - EF Core tự động kiểm tra giá trị này
        /// chưa đổi trước khi UPDATE. Nếu request khác đã ghi đè bản ghi này trước,
        /// SaveChangesAsync sẽ ném DbUpdateConcurrencyException thay vì ghi đè âm thầm.
        /// </summary>
        public byte[] RowVersion { get; set; } = null!;

        /// <summary>0..1 - chỉ có sau khi bác sĩ khám xong.</summary>
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
        /// Gọi bệnh nhân vào khám. Chỉ cho phép gọi khi đang ở trạng thái Waiting
        /// (chưa được gọi lần nào, hoặc đã reset lại từ 1 lượt gọi trước).
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
        /// Bỏ qua lượt khám (bệnh nhân không có mặt khi được gọi, hoặc lễ tân chủ động bỏ qua
        /// trong khi vẫn đang chờ). Chỉ cho phép khi vé đang ở Waiting hoặc Called.
        /// Không cho phép bỏ qua khi đang khám (InProgress) - trường hợp này phải dùng Complete();
        /// cũng không cho phép khi đã khám xong (Completed) hoặc đã bị bỏ qua trước đó (Skipped).
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

        /// <summary>
        /// Bắt đầu khám cho bệnh nhân vừa được gọi. Chỉ hợp lệ khi vé đang ở trạng thái Called
        /// (đã gọi số nhưng chưa bắt đầu khám).
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
        /// Hoàn tất lượt khám. Chỉ hợp lệ khi vé đang ở trạng thái InProgress (đang khám).
        /// Sau bước này, bác sĩ được coi là "rảnh" và có thể gọi số tiếp theo.
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
        /// Hủy vé hàng đợi vì Appointment tương ứng bị hủy sau khi bệnh nhân đã check-in.
        /// Chỉ cho phép khi vé còn Waiting (chưa từng được gọi) hoặc Called (đã gọi nhưng
        /// bệnh nhân chưa vào phòng khám) - đây là 2 trạng thái mà việc hủy còn ý nghĩa và
        /// không làm gián đoạn 1 lượt khám đang diễn ra.
        /// Không cho phép hủy khi InProgress (bác sĩ đang khám dở dang - hủy lúc này vô nghĩa
        /// và có thể làm mất dữ liệu khám đang thực hiện) hoặc khi vé đã ở trạng thái kết thúc
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
        /// Đánh dấu (hoặc gỡ đánh dấu) ưu tiên khẩn cấp. Chỉ áp dụng khi vé còn đang chờ (Waiting) -
        /// ưu tiên chỉ có ý nghĩa thay đổi thứ tự của lượt gọi tiếp theo, không còn tác dụng
        /// khi đã được gọi/đang khám/khám xong/bị bỏ qua.
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
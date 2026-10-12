using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Bộ đếm số thứ tự hàng đợi, theo từng bác sĩ và từng ngày.
    /// Dùng để sinh QueueNumber tăng dần một cách atomic bằng câu lệnh
    /// MERGE trong QueueTicketRepository.GetNextQueueNumberAsync, tránh
    /// trùng số khi nhiều bệnh nhân check-in đồng thời cho cùng 1 bác sĩ/ngày.
    /// Khóa chính composite (DoctorId, Date) - mỗi bác sĩ/ngày chỉ có đúng 1 dòng.
    /// </summary>
    public class QueueCounter
    {
        public Guid DoctorId { get; set; }

        public DateOnly Date { get; set; }

        /// <summary>Số thứ tự lớn nhất đã cấp cho bác sĩ này trong ngày này.</summary>
        public int CurrentNumber { get; set; }
    }
}
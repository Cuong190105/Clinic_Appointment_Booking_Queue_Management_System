using Clinic.Domain.Entities;
using System;

namespace Clinic.Application.Interfaces
{
    public interface IQueueTicketRepository
    {
        /// <summary>
        /// Tính số thứ tự hàng đợi tiếp theo cho 1 bác sĩ trong 1 ngày cụ thể một cách
        /// ATOMIC (dùng MERGE + HOLDLOCK trên bảng QueueCounters ở tầng Infrastructure),
        /// đảm bảo không phát sinh số trùng khi nhiều check-in xảy ra đồng thời.
        /// Trả về 1 nếu bác sĩ chưa có bệnh nhân nào check-in trong ngày.
        /// </summary>
        Task<int> GetNextQueueNumberAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Thêm mới 1 số thứ tự hàng đợi, sinh ra khi bệnh nhân check-in
        /// (tại quầy hoặc online).
        /// </summary>
        Task AddAsync(QueueTicket queueTicket);

        /// <summary>
        /// Lấy 1 QueueTicket theo Id, kèm Appointment -> WorkSchedule (Doctor) và Appointment -> Patient -> Person
        /// để phục vụ authorization theo scope và hiển thị tên bệnh nhân.
        /// </summary>
        Task<QueueTicket?> GetByIdAsync(Guid id);

        /// <summary>
        /// Lấy toàn bộ hàng đợi của 1 bác sĩ trong 1 ngày cụ thể, sắp theo Priority (ưu tiên trước)
        /// rồi theo QueueNumber tăng dần - dùng cho dashboard xem hàng đợi (queue.view/doctor.queue.view).
        /// </summary>
        Task<List<QueueTicket>> GetByDoctorAsync(Guid doctorId, DateTime date);

        /// <summary>
        /// Lấy vé đang chờ (Waiting) tiếp theo cần gọi của 1 bác sĩ trong ngày, ưu tiên vé Priority = true,
        /// sau đó theo QueueNumber tăng dần. Trả về null nếu không còn ai đang chờ.
        /// </summary>
        Task<QueueTicket?> GetNextWaitingAsync(Guid doctorId, DateTime date);

        /// <summary>
        /// Cập nhật 1 QueueTicket đã tồn tại (gọi số, bỏ qua lượt, đổi ưu tiên).
        /// </summary>
        Task UpdateAsync(QueueTicket queueTicket);

        /// <summary>
        /// Lấy TOÀN BỘ vé hàng đợi đang "sống" (Waiting/Called/InProgress) của 1 bệnh nhân trong
        /// 1 ngày cụ thể, kèm Appointment -> WorkSchedule -> Doctor -> Employee -> Person, sắp theo
        /// CheckInTime tăng dần. Trả về danh sách rỗng nếu bệnh nhân chưa check-in ngày hôm đó hoặc
        /// đã được khám xong/bỏ qua. Dùng cho "theo dõi vị trí hàng đợi" của Patient.
        ///
        /// LƯU Ý: 1 bệnh nhân có thể có nhiều Appointment (khác bác sĩ hoặc khác khung giờ) được
        /// Confirm và check-in trong cùng 1 ngày -> có thể có NHIỀU vé active song song. Không được
        /// rút gọn về 1 vé (kiểu FirstOrDefault theo CheckInTime mới nhất) vì sẽ làm "biến mất" các
        /// vé còn lại trong khi chúng vẫn đang chiếm chỗ thật trong hàng đợi của bác sĩ tương ứng.
        /// </summary>
        Task<List<QueueTicket>> GetActiveTicketsByPatientAsync(Guid patientId, DateTime date);

        /// <summary>
        /// Lấy vé đang "active" (Called hoặc InProgress) của 1 bác sĩ trong ngày - dùng để
        /// ràng buộc mỗi bác sĩ chỉ có tối đa 1 vé đang được gọi/khám tại một thời điểm.
        /// Trả về null nếu bác sĩ hiện không có vé nào đang active (đang rảnh, sẵn sàng gọi tiếp).
        /// </summary>
        Task<QueueTicket?> GetActiveTicketAsync(Guid doctorId, DateTime date);
    }
}
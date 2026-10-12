using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Clinic.Application.Interfaces
{
    public interface IWorkScheduleRepository
    {
        /// <summary>
        /// Lấy 1 WorkSchedule theo Id, kèm Appointments (để tính số chỗ còn trống) và
        /// Doctor -> Employee -> Person (để hiển thị/kiểm tra khi cần).
        /// </summary>
        Task<WorkSchedule?> GetByIdAsync(Guid id);

        /// <summary>
        /// Khóa ghi (UPDLOCK, HOLDLOCK) đúng 1 dòng WorkSchedule, giữ tới khi transaction hiện
        /// tại COMMIT/ROLLBACK. Dùng NGAY TRƯỚC khi đếm số lịch hẹn đang có + ghi lịch hẹn mới
        /// cho slot này (xem CreateAppointmentHandler), để 2 request đặt lịch đồng thời cho
        /// cùng 1 slot bắt buộc phải chạy tuần tự thay vì cùng đọc thấy "còn chỗ" rồi cùng
        /// insert thành công (vi phạm PatientLimitPerSlot).
        /// Bắt buộc gọi bên trong 1 transaction đã mở qua IUnitOfWork.BeginTransactionAsync,
        /// nếu không lock sẽ được nhả ngay sau câu lệnh và không có tác dụng.
        /// </summary>
        Task LockAsync(Guid workScheduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy các WorkSchedule (khung giờ) còn chỗ trống, chưa kết thúc, có thể lọc theo bác sĩ,
        /// chuyên khoa hiện tại của bác sĩ, và khoảng ngày. Dùng cho màn "chọn khung giờ trống để đặt lịch".
        /// </summary>
        Task<List<WorkSchedule>> GetAvailableSlotsAsync(Guid? doctorId, Guid? specialtyId, DateTime? fromDate, DateTime? toDate);

        // ===== Nhóm chức năng: quản lý ca trực bác sĩ (shift request - của dev/cuong) =====

        /// <summary>
        /// Retrieves work schedules for a doctor in the specified time range.
        /// The range should be less than 1 month.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the time range exceeds 1 month.</exception>
        Task<IEnumerable<WorkSchedule>> GetPlannedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Retrieves requested shifts for a doctor in the specified time range.
        /// The range should be less than 1 month.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the time range exceeds 1 month.</exception>
        Task<IEnumerable<ShiftRequest>> GetRequestedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Retrieves a work schedule by its unique identifier asynchronously, including its associated doctor-person info.
        /// </summary>
        Task<WorkSchedule?> GetWorkScheduleByIdAsync(Guid scheduleId);

        /// <summary>
        /// Retrieves a requested shift by its unique identifier asynchronously, including its associated doctor and active appointments.
        /// </summary>
        Task<ShiftRequest?> GetShiftRequestByIdAsync(Guid scheduleId);

        /// <summary>
        /// Adds a new work schedule to the repository asynchronously.
        /// </summary>
        Task AddWorkScheduleAsync(WorkSchedule workSchedule);

        /// <summary>
        /// Adds a new shift request to the repository asynchronously.
        /// </summary>
        Task AddShiftRequestAsync(ShiftRequest shiftRequest);

        /// <summary>
        /// Checks if a doctor has overlapping work schedules within the specified time range asynchronously.
        /// If currentWSId is provided, it will be excluded from the check (useful for updates).
        /// </summary>
        Task<bool> HasOverlappingWorkSchedule(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentWSId = null);

        /// <summary>
        /// Checks if a doctor has already requested a shift that has exact same time range asynchronously.
        /// If currentSRId is provided, it will be excluded from the check (useful for updates).
        /// </summary>
        Task<bool> HasDuplicateShiftRequest(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentSRId = null);

        Task<IEnumerable<WorkSchedule>> GetDoctorSchedulesWithAppointmentByDateAsync(Guid? doctorId, DateOnly date);
    }
}
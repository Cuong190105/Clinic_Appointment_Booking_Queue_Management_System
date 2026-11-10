using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Clinic.Application.Interfaces
{
    public interface IWorkScheduleRepository
    {
        /// <summary>
        /// L?y 1 WorkSchedule theo Id, kèm Appointments (d? tính s? ch? còn tr?ng) và
        /// Doctor -> Employee -> Person (d? hi?n th?/ki?m tra khi c?n).
        /// </summary>
        Task<WorkSchedule?> GetByIdAsync(Guid id);

        /// <summary>
        /// Khóa ghi (UPDLOCK, HOLDLOCK) dúng 1 dòng WorkSchedule, gi? t?i khi transaction hi?n
        /// t?i COMMIT/ROLLBACK. Dùng NGAY TRU?C khi d?m s? l?ch h?n dang có + ghi l?ch h?n m?i
        /// cho slot này (xem CreateAppointmentHandler), d? 2 request d?t l?ch d?ng th?i cho
        /// cùng 1 slot b?t bu?c ph?i ch?y tu?n t? thay vì cùng d?c th?y "còn ch?" r?i cùng
        /// insert thành công (vi ph?m PatientLimitPerSlot).
        /// B?t bu?c g?i bên trong 1 transaction dã m? qua IUnitOfWork.BeginTransactionAsync,
        /// n?u không lock s? du?c nh? ngay sau câu l?nh và không có tác d?ng.
        /// </summary>
        Task LockAsync(Guid workScheduleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// L?y các WorkSchedule (khung gi?) còn ch? tr?ng, chua k?t thúc, có th? l?c theo bác si,
        /// chuyên khoa hi?n t?i c?a bác si, và kho?ng ngày. Dùng cho màn "ch?n khung gi? tr?ng d? d?t l?ch".
        /// </summary>
        Task<List<WorkSchedule>> GetAvailableSlotsAsync(Guid? doctorId, Guid? specialtyId, DateTime? fromDate, DateTime? toDate);


        // ===== Nhóm ch?c nang: qu?n lý ca tr?c bác si (shift request - c?a dev/cuong) =====

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
        Task<PagedResult<Doctor>> GetWeeklyWorkSchedules(DateOnly startDate, DateOnly endDate, int pageNumber, int pageSize, Guid? specialtyId, string? searchTerm);
    }
}
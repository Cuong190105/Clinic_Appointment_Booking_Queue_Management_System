using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Interfaces
{
    public interface IMedicalReportRepository
    {
        /// <summary>
        /// Thêm mới 1 hồ sơ khám bệnh vào DB context.
        /// </summary>
        Task AddAsync(MedicalReport report);

        /// <summary>
        /// Lấy hồ sơ khám cùng patient profile trong Clinic để phục vụ resource-based authorization.
        /// Appointment snapshot được lưu trên report; trả về null nếu không tìm thấy.
        /// </summary>
        Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id);

        /// <summary>
        /// Lấy hồ sơ khám theo loose AppointmentId reference.
        /// </summary>
        Task<MedicalReport?> GetByAppointmentIdAsync(Guid appointmentId);

        /// <summary>
        /// Lấy toàn bộ lịch sử khám bệnh (các MedicalReport) của 1 bệnh nhân, sắp xếp mới nhất trước.
        /// </summary>
        Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId);

        Task<bool> ExistsForDoctorAndPatientAsync(Guid doctorId, Guid patientId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tìm kiếm và phân trang hồ sơ khám bệnh.
        /// </summary>
        Task<(List<MedicalReport> Items, int TotalCount)> SearchAsync(
            Guid? doctorId,
            string? search,
            MedicalReportStatus? status,
            DateOnly? fromDate,
            DateOnly? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}

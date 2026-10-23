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
        /// Lấy 1 hồ sơ khám bệnh, kèm Appointment -> QueueTicket/WorkSchedule (Doctor) và Patient
        /// để phục vụ resource-based authorization (own/related/any). Trả về null nếu không tìm thấy.
        /// </summary>
        Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id);

        /// <summary>
        /// Lấy 1 hồ sơ khám bệnh theo AppointmentId, kèm đầy đủ quan hệ Doctor và Patient.
        /// </summary>
        Task<MedicalReport?> GetByAppointmentIdAsync(Guid appointmentId);

        /// <summary>
        /// Lấy toàn bộ lịch sử khám bệnh (các MedicalReport) của 1 bệnh nhân, sắp xếp mới nhất trước.
        /// </summary>
        Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId);

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

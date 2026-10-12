using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface IMedicalReportRepository
    {
        /// <summary>
        /// Thêm mới 1 hồ sơ khám bệnh vào DB context.
        /// </summary>
        Task AddAsync(MedicalReport report);

        /// <summary>
        /// Lấy 1 hồ sơ khám bệnh, kèm QueueTicket -> Appointment -> WorkSchedule (Doctor) và Appointment.Patient
        /// để phục vụ resource-based authorization (own/related/any). Trả về null nếu không tìm thấy.
        /// </summary>
        Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id);

        /// <summary>
        /// Lấy 1 hồ sơ khám bệnh theo QueueTicketId, kèm đầy đủ quan hệ Doctor và Patient.
        /// </summary>
        Task<MedicalReport?> GetByQueueTicketIdAsync(Guid queueTicketId);

        /// <summary>
        /// Lấy toàn bộ lịch sử khám bệnh (các MedicalReport) của 1 bệnh nhân, sắp xếp mới nhất trước.
        /// </summary>
        Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId);
    }
}
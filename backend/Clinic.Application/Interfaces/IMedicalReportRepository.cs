using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Interfaces
{
    public interface IMedicalReportRepository
    {
        Task AddAsync(MedicalReport report);
        Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id);
        Task<MedicalReport?> GetByAppointmentIdAsync(Guid appointmentId);
        Task<MedicalReport?> GetByQueueTicketIdAsync(Guid queueTicketId);
        Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId);
        Task<PaginationResponse<MedicalReportDto>> GetPagedReportsAsync(
            Guid? doctorId,
            bool isDoctorScoped,
            string? search,
            int? status,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default);
        Task AddAuditAsync(MedicalAccessAudit audit);
        Task AddAmendmentAsync(MedicalReportAmendment amendment);
        Task<List<MedicalReportAmendment>> GetAmendmentsByReportIdAsync(Guid medicalReportId);
    }
}

using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeMedicalReportRepository : IMedicalReportRepository
    {
        private readonly List<MedicalReport> _reports = [];
        private readonly List<MedicalAccessAudit> _audits = [];
        private readonly List<MedicalReportAmendment> _amendments = [];

        public IReadOnlyList<MedicalAccessAudit> Audits => _audits;
        public IReadOnlyList<MedicalReportAmendment> Amendments => _amendments;

        public void Add(MedicalReport report)
        {
            _reports.Add(report);
        }

        public Task AddAsync(MedicalReport report)
        {
            _reports.Add(report);
            return Task.CompletedTask;
        }

        public Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id)
        {
            var report = _reports.FirstOrDefault(r => r.Id == id && !r.IsDeleted);
            return Task.FromResult(report);
        }

        public Task<MedicalReport?> GetByAppointmentIdAsync(Guid appointmentId)
        {
            var report = _reports.FirstOrDefault(r => r.AppointmentId == appointmentId && !r.IsDeleted);
            return Task.FromResult(report);
        }

        public Task<MedicalReport?> GetByQueueTicketIdAsync(Guid queueTicketId)
        {
            var report = _reports.FirstOrDefault(r => (r.QueueTicketId == queueTicketId || r.Appointment?.QueueTicket?.Id == queueTicketId || r.AppointmentId == queueTicketId) && !r.IsDeleted);
            return Task.FromResult(report);
        }

        public Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId)
        {
            var history = _reports
                .Where(r => !r.IsDeleted && (r.Appointment?.PatientId == patientId || r.QueueTicket?.Appointment?.PatientId == patientId))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return Task.FromResult(history);
        }

        public Task<Clinic.Application.Contracts.PaginationResponse<Clinic.Application.Contracts.MedicalReportDto>> GetPagedReportsAsync(
            Guid? doctorId,
            bool isDoctorScoped,
            string? search,
            int? status,
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var filtered = _reports.Where(r => !r.IsDeleted);
            if (isDoctorScoped && doctorId.HasValue)
            {
                filtered = filtered.Where(r => r.Appointment?.WorkSchedule?.DoctorId == doctorId.Value || r.QueueTicket?.Appointment?.WorkSchedule?.DoctorId == doctorId.Value);
            }
            var list = filtered.OrderByDescending(r => r.CreatedAt).ToList();
            var dtos = list.Select(r => new Clinic.Application.Contracts.MedicalReportDto
            {
                Id = r.Id,
                AppointmentId = r.AppointmentId,
                Symptoms = r.Symptoms,
                Diagnosis = r.Diagnosis,
                Prescription = r.Prescription,
                Notes = r.Notes,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult(new Clinic.Application.Contracts.PaginationResponse<Clinic.Application.Contracts.MedicalReportDto>(dtos, list.Count, pageNumber, pageSize));
        }

        public Task AddAuditAsync(MedicalAccessAudit audit)
        {
            _audits.Add(audit);
            return Task.CompletedTask;
        }

        public Task AddAmendmentAsync(MedicalReportAmendment amendment)
        {
            _amendments.Add(amendment);
            return Task.CompletedTask;
        }

        public Task<List<MedicalReportAmendment>> GetAmendmentsByReportIdAsync(Guid medicalReportId)
        {
            var items = _amendments
                .Where(a => a.MedicalReportId == medicalReportId && !a.IsDeleted)
                .OrderBy(a => a.CreatedAt)
                .ToList();
            return Task.FromResult(items);
        }
    }
}

using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeMedicalReportRepository : IMedicalReportRepository
    {
        private readonly List<MedicalReport> _reports = [];

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

        public Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId)
        {
            var history = _reports
                .Where(r => !r.IsDeleted && r.PatientId == patientId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return Task.FromResult(history);
        }

        public Task<(List<MedicalReport> Items, int TotalCount)> SearchAsync(
            Guid? doctorId,
            string? search,
            MedicalReportStatus? status,
            DateOnly? fromDate,
            DateOnly? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<MedicalReport> query = _reports.Where(r => !r.IsDeleted);

            if (doctorId.HasValue)
            {
                query = query.Where(r => r.Appointment != null && r.Appointment.DoctorId == doctorId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(r => r.Appointment != null && r.Appointment.ScheduleDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(r => r.Appointment != null && r.Appointment.ScheduleDate <= toDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r =>
                    (r.Patient != null && r.Patient.Person != null && r.Patient.Person.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.Diagnosis != null && r.Diagnosis.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (r.Symptoms != null && r.Symptoms.Contains(term, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult((items, totalCount));
        }
    }
}

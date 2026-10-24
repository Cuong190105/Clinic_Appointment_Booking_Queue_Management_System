using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class MedicalReportRepository : IMedicalReportRepository
    {
        private readonly ApplicationDbContext _context;

        public MedicalReportRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(MedicalReport report)
        {
            await _context.MedicalReports.AddAsync(report);
        }

        /// <summary>
        /// Lấy 1 hồ sơ khám bệnh, kèm Appointment -> QueueTicket/WorkSchedule (Doctor) và Patient
        /// để phục vụ resource-based authorization (own/related/any).
        /// </summary>
        public async Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)

                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.QueueTicket)
                .Include(mr => mr.Patient)
                    .ThenInclude(p => p.Person)
                .FirstOrDefaultAsync(mr => mr.Id == id && !mr.IsDeleted);
        }

        public async Task<MedicalReport?> GetByAppointmentIdAsync(Guid appointmentId)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)

                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.QueueTicket)
                .Include(mr => mr.Patient)
                    .ThenInclude(p => p.Person)
                .FirstOrDefaultAsync(mr => mr.AppointmentId == appointmentId && !mr.IsDeleted);
        }

        /// <summary>
        /// Lấy toàn bộ lịch sử khám bệnh (các MedicalReport) của 1 bệnh nhân, sắp xếp mới nhất trước.
        /// </summary>
        public async Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)

                .Include(mr => mr.Patient)
                    .ThenInclude(p => p.Person)
                .Where(mr => mr.PatientId == patientId && !mr.IsDeleted)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<MedicalReport> Items, int TotalCount)> SearchAsync(
            Guid? doctorId,
            string? search,
            MedicalReportStatus? status,
            DateOnly? fromDate,
            DateOnly? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.MedicalReports
                .Include(mr => mr.Appointment)

                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.QueueTicket)
                .Include(mr => mr.Patient)
                    .ThenInclude(p => p.Person)
                .Where(mr => !mr.IsDeleted);

            if (doctorId.HasValue)
            {
                query = query.Where(mr => mr.Appointment != null && mr.Appointment.DoctorId == doctorId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(mr => mr.Status == status.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(mr => mr.Appointment != null && mr.Appointment.ScheduleDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(mr => mr.Appointment != null && mr.Appointment.ScheduleDate <= toDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(mr =>
                    (mr.Patient != null && mr.Patient.Person != null && mr.Patient.Person.FullName.Contains(term)) ||
                    (mr.Patient != null && mr.Patient.Person != null && mr.Patient.Person.PhoneNumber != null && mr.Patient.Person.PhoneNumber.Contains(term)) ||
                    (mr.Diagnosis != null && mr.Diagnosis.Contains(term)) ||
                    (mr.Symptoms != null && mr.Symptoms.Contains(term)) ||
                    (mr.Notes != null && mr.Notes.Contains(term)) ||
                    (mr.Prescription != null && mr.Prescription.Contains(term)) ||
                    (mr.Appointment != null && mr.Appointment.DoctorName != null && mr.Appointment.DoctorName.Contains(term)));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(mr => mr.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
    }
}

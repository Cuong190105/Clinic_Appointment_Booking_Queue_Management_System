using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

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
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
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
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
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
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(mr => mr.Patient)
                    .ThenInclude(p => p.Person)
                .Where(mr => mr.PatientId == patientId && !mr.IsDeleted)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync();
        }
    }
}

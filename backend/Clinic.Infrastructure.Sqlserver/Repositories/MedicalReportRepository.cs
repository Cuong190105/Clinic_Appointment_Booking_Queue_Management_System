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

        public async Task<MedicalReport?> GetByIdWithOwnershipAsync(Guid id)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Include(mr => mr.QueueTicket)
                .Include(mr => mr.Amendments)
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
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Include(mr => mr.QueueTicket)
                .Include(mr => mr.Amendments)
                .FirstOrDefaultAsync(mr => mr.AppointmentId == appointmentId && !mr.IsDeleted);
        }

        public async Task<MedicalReport?> GetByQueueTicketIdAsync(Guid queueTicketId)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Include(mr => mr.QueueTicket)
                .Include(mr => mr.Amendments)
                .FirstOrDefaultAsync(mr => (mr.QueueTicketId == queueTicketId || (mr.Appointment != null && mr.Appointment.QueueTicket != null && mr.Appointment.QueueTicket.Id == queueTicketId)) && !mr.IsDeleted);
        }

        public async Task<List<MedicalReport>> GetHistoryByPatientIdAsync(Guid patientId)
        {
            return await _context.MedicalReports
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Include(mr => mr.Amendments)
                .Where(mr => mr.Appointment.PatientId == patientId && !mr.IsDeleted)
                .OrderByDescending(mr => mr.CreatedAt)
                .ToListAsync();
        }

        public async Task<Clinic.Application.Contracts.PaginationResponse<Clinic.Application.Contracts.MedicalReportDto>> GetPagedReportsAsync(
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
            var query = _context.MedicalReports
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(ws => ws.Doctor)
                            .ThenInclude(d => d.WorkHistories)
                                .ThenInclude(wh => wh.Specialty)
                .Include(mr => mr.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Include(mr => mr.QueueTicket)
                .Include(mr => mr.Amendments)
                .Where(mr => !mr.IsDeleted);

            if (isDoctorScoped && doctorId.HasValue)
            {
                query = query.Where(mr => (mr.Appointment != null && mr.Appointment.WorkSchedule.DoctorId == doctorId.Value)
                    || (mr.QueueTicket != null && mr.QueueTicket.Appointment.WorkSchedule.DoctorId == doctorId.Value));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(mr =>
                    (mr.Appointment != null && mr.Appointment.Patient != null && mr.Appointment.Patient.Person.FullName.Contains(s))
                    || mr.Diagnosis.Contains(s)
                    || mr.Symptoms.Contains(s));
            }

            if (status.HasValue)
            {
                var statusEnum = (Clinic.Domain.Enums.MedicalReportStatus)status.Value;
                query = query.Where(mr => mr.Status == statusEnum);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(mr => mr.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(mr => mr.CreatedAt <= toDate.Value);
            }

            var totalCount = await query.CountAsync(ct);
            var reports = await query
                .OrderByDescending(mr => mr.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var dtos = reports.Select(report =>
            {
                var appointment = report.Appointment ?? report.QueueTicket?.Appointment;
                var docId = appointment?.WorkSchedule?.DoctorId ?? Guid.Empty;
                var amendments = (report.Amendments ?? Enumerable.Empty<Clinic.Domain.Entities.MedicalReportAmendment>())
                    .Select(a => new Clinic.Application.Contracts.MedicalReportAmendmentDto
                    {
                        Id = a.Id,
                        MedicalReportId = a.MedicalReportId,
                        Reason = a.Reason,
                        Content = a.Content,
                        CreatedByDoctorId = a.CreatedByDoctorId,
                        DoctorName = a.DoctorName,
                        CreatedAt = a.CreatedAt
                    }).ToList();

                return new Clinic.Application.Contracts.MedicalReportDto
                {
                    Id = report.Id,
                    AppointmentId = appointment?.Id ?? report.AppointmentId,
                    QueueTicketId = report.QueueTicketId ?? appointment?.QueueTicket?.Id,
                    PatientId = appointment?.PatientId ?? Guid.Empty,
                    PatientName = appointment?.Patient?.Person?.FullName ?? string.Empty,
                    DoctorId = docId,
                    DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    SpecialtyName = appointment?.WorkSchedule?.Doctor?.WorkHistories.FirstOrDefault(wh => wh.EndDate == null)?.Specialty?.Name ?? string.Empty,
                    ExamDate = appointment?.WorkSchedule?.Date ?? DateOnly.MinValue,
                    Symptoms = report.Symptoms,
                    Diagnosis = report.Diagnosis,
                    Prescription = report.Prescription,
                    Notes = report.Notes,
                    ExamStartTime = report.ExamStartTime,
                    ExamEndTime = report.ExamEndTime,
                    Status = report.Status,
                    CreatedAt = report.CreatedAt,
                    Amendments = amendments
                };
            }).ToList();

            return new Clinic.Application.Contracts.PaginationResponse<Clinic.Application.Contracts.MedicalReportDto>(dtos, totalCount, pageNumber, pageSize);
        }

        public async Task AddAuditAsync(MedicalAccessAudit audit)
        {
            await _context.MedicalAccessAudits.AddAsync(audit);
        }

        public async Task AddAmendmentAsync(MedicalReportAmendment amendment)
        {
            await _context.MedicalReportAmendments.AddAsync(amendment);
        }

        public async Task<List<MedicalReportAmendment>> GetAmendmentsByReportIdAsync(Guid medicalReportId)
        {
            return await _context.MedicalReportAmendments
                .Where(a => a.MedicalReportId == medicalReportId && !a.IsDeleted)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }
    }
}

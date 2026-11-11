using Azure;
using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    // Use for both WorkSchedule and ShiftRequest
    public class WorkScheduleRepository : IWorkScheduleRepository
    {
        private readonly ApplicationDbContext _context;

        public WorkScheduleRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===== NhÃ³m chá»©c nÄƒng: Ä‘áº·t lá»‹ch khÃ¡m (booking) =====

        public async Task<WorkSchedule?> GetByIdAsync(Guid id)
        {
            return await _context.WorkSchedules
                .Include(w => w.Appointments.Where(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))
                .Include(w => w.Doctor)
                    .ThenInclude(d => d.Employee)
                        .ThenInclude(e => e.Person)
                .FirstOrDefaultAsync(w => w.Id == id && w.IsDeleted == false);
        }

        public async Task LockAsync(Guid workScheduleId, CancellationToken cancellationToken = default)
        {
            // KhÃ³a ghi (UPDLOCK, HOLDLOCK) Ä‘Ãºng 1 dÃ²ng WorkSchedule, giá»¯ tá»›i khi transaction
            // hiá»‡n táº¡i COMMIT/ROLLBACK. Request thá»© 2 cÅ©ng gá»i LockAsync cho cÃ¹ng
            // workScheduleId sáº½ bá»‹ cháº·n ngay táº¡i cÃ¢u SELECT nÃ y cho tá»›i khi request thá»© 1
            // xong, nÃªn GetByIdAsync gá»i ngay sau Ä‘Ã³ luÃ´n tháº¥y Ä‘Ãºng sá»‘ Appointments má»›i nháº¥t -
            // khÃ´ng cÃ²n cá»­a sá»• "Ä‘áº¿m rá»“i má»›i insert" Ä‘á»ƒ 2 request cÃ¹ng lá»t qua nhÆ° trÆ°á»›c.
            // Cá»™t output pháº£i Ä‘áº·t tÃªn "Value" vÃ¬ SqlQuery<int> map theo quy Æ°á»›c nÃ y.
            await _context.Database
                .SqlQuery<int>($"SELECT 1 AS Value FROM WorkSchedules WITH (UPDLOCK, ROWLOCK) WHERE Id = {workScheduleId}")
                .ToListAsync(cancellationToken);
        }

        public async Task<List<WorkSchedule>> GetAvailableSlotsAsync(Guid? doctorId, Guid? specialtyId, DateTime? fromDate, DateTime? toDate)
        {
            var timeConverter = new Clinic.Domain.Common.TimeConverter();
            var today = timeConverter.Today;
            var nowTime = timeConverter.CurrentTime;

            var query = _context.WorkSchedules
                .Include(w => w.Appointments)
                .Include(w => w.Doctor)
                    .ThenInclude(d => d.Employee)
                        .ThenInclude(e => e.Person)
                .Where(w => w.IsDeleted == false &&
                            w.Status == WorkScheduleStatus.Active &&
                            (w.Date > today || (w.Date == today && w.ShiftEnd > nowTime)));

            if (doctorId.HasValue)
            {
                query = query.Where(w => w.DoctorId == doctorId.Value);
            }

            if (specialtyId.HasValue)
            {
                query = query.Where(w => w.Doctor.WorkHistories.Any(wh =>
                    wh.SpecialtyId == specialtyId.Value && wh.EndDate == null));
            }

            if (fromDate.HasValue)
            {
                var fromDateOnly = DateOnly.FromDateTime(fromDate.Value);
                var fromTimeOnly = TimeOnly.FromDateTime(fromDate.Value);
                query = query.Where(w => w.Date > fromDateOnly || (w.Date == fromDateOnly && w.ShiftStart >= fromTimeOnly));
            }

            if (toDate.HasValue)
            {
                var toDateOnly = DateOnly.FromDateTime(toDate.Value);
                var toTimeOnly = TimeOnly.FromDateTime(toDate.Value);
                query = query.Where(w => w.Date < toDateOnly || (w.Date == toDateOnly && w.ShiftStart <= toTimeOnly));
            }

            // Chá»‰ láº¥y khung giá» cÃ²n chá»— trá»‘ng (sá»‘ lá»‹ch háº¹n chÆ°a há»§y < giá»›i háº¡n/khung giá»).
            query = query.Where(w => w.Appointments.Count(a => !a.IsWalkIn && a.Status != AppointmentStatus.Cancelled && !a.IsDeleted) < w.RecommendedCapacity);

            return await query.OrderBy(w => w.ShiftStart).ToListAsync();
        }

        // ===== NhÃ³m chá»©c nÄƒng: quáº£n lÃ½ ca trá»±c bÃ¡c sÄ© (shift request) =====

        public async Task AddShiftRequestAsync(ShiftRequest shiftRequest)
        {
            await _context.ShiftRequests.AddAsync(shiftRequest);
        }

        public async Task AddWorkScheduleAsync(WorkSchedule workSchedule)
        {
            await _context.WorkSchedules.AddAsync(workSchedule);
        }

        public async Task<IEnumerable<WorkSchedule>> GetDoctorSchedulesWithAppointmentByDateAsync(Guid? doctorId, DateOnly date)
        {
            return await _context.WorkSchedules.Include(ws => ws.Appointments.Where(a => a.IsDeleted == false && a.Status != AppointmentStatus.Cancelled))
                .Where(ws =>
                    ws.DoctorId == doctorId &&
                    ws.IsDeleted == false &&
                    ws.Status == WorkScheduleStatus.Active &&
                    ws.Date == date)
                .AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<WorkSchedule>> GetPlannedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate)
        {
            // Check if the time range exceeds 1 month
            if (startDate.AddMonths(1) < endDate)
            {
                throw new ArgumentException("The time range cannot exceed 1 month.");
            }

            return await _context.WorkSchedules
                .Where(ws =>
                    ws.DoctorId == doctorId &&
                    ws.IsDeleted == false &&
                    ws.Status != WorkScheduleStatus.Cancelled &&
                    ws.Date >= startDate &&
                    ws.Date <= endDate
                ).AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<ShiftRequest>> GetRequestedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate)
        {
            // Check if the time range exceeds 1 month
            if (startDate.AddMonths(1) < endDate)
            {
                throw new ArgumentException("The time range cannot exceed 1 month.");
            }
            if (doctorId == null)
            {
                throw new ArgumentException("DoctorId cannot be null.");
            }

            return await _context.ShiftRequests
                .Where(sr =>
                    sr.DoctorId == doctorId &&
                    sr.IsDeleted == false &&
                    sr.Status != ShiftRequestStatus.Cancelled &&
                    sr.Date >= startDate &&
                    sr.Date <= endDate
                ).AsNoTracking().ToListAsync();
        }

        public async Task<ShiftRequest?> GetShiftRequestByIdAsync(Guid scheduleId)
        {
            return await _context.ShiftRequests.Include(sr => sr.Doctor)
                .FirstOrDefaultAsync(sr => sr.Id == scheduleId && sr.IsDeleted == false);
        }

        public async Task<WorkSchedule?> GetWorkScheduleByIdAsync(Guid scheduleId)
        {
            return await _context.WorkSchedules.Include(ws => ws.Doctor).ThenInclude(d => d.Employee).ThenInclude(e => e.Person)
                .Include(ws => ws.Appointments.Where(a => a.IsDeleted == false && a.Status != AppointmentStatus.Cancelled)).ThenInclude(a => a.Patient).ThenInclude(p => p.Person)
                .Include(ws => ws.Appointments.Where(a => a.IsDeleted == false && a.Status != AppointmentStatus.Cancelled)).ThenInclude(a => a.QueueTicket)
                .FirstOrDefaultAsync(ws => ws.Id == scheduleId && ws.IsDeleted == false);
        }

        public async Task<bool> HasDuplicateShiftRequest(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentSRId = null)
        {
            var hasDuplicate = await _context.ShiftRequests
                .AnyAsync(sr => sr.DoctorId == doctorId && sr.IsDeleted == false && sr.Status != Domain.Enums.ShiftRequestStatus.Cancelled &&
                    sr.Date == date && sr.ShiftStart == startTime && sr.ShiftEnd == endTime && (currentSRId == null || sr.Id != currentSRId));
            return hasDuplicate;
        }

        public async Task<bool> HasOverlappingWorkSchedule(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentWSId = null)
        {
            var hasOverlapping = await _context.WorkSchedules
                .AnyAsync(ws => ws.DoctorId == doctorId && ws.IsDeleted == false && ws.Status != Domain.Enums.WorkScheduleStatus.Cancelled &&
                    ws.Date == date && ws.ShiftStart < endTime && ws.ShiftEnd > startTime && (currentWSId == null || ws.Id != currentWSId));
            return hasOverlapping;
        }

        public async Task<PagedResult<Doctor>> GetWeeklyWorkSchedules(DateOnly startDate, DateOnly endDate, int pageNumber, int pageSize, Guid? specialtyId, string? searchTerm)
        {
            var query = _context.Doctors
                .Include(d => d.Employee)
                    .ThenInclude(e => e.Person)
                .Include(d => d.WorkHistories.Where(wh => wh.IsDeleted == false && wh.EndDate == null))
                    .ThenInclude(wh => wh.Specialty)
                .Include(d => d.WorkSchedules.Where(ws => ws.IsDeleted == false && ws.Date >= startDate && ws.Date <= endDate))
                .Where(d => d.IsDeleted == false);
            Console.WriteLine(startDate);
            if (specialtyId != Guid.Empty)
            {
                query = query.Where(d => d.WorkHistories.Any(wh =>
                    wh.SpecialtyId == specialtyId.Value && wh.EndDate == null));
            }

            if (string.IsNullOrEmpty(searchTerm) == false)
            {
                query = query.Where(d => d.Employee.Person.FullName.Contains(searchTerm)
                    || d.WorkHistories.Any(wh => wh.Specialty.Name.Contains(searchTerm) && wh.EndDate == null));
            }

            var totalCount = await query.CountAsync();

            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<Doctor>(items, totalCount);
        }
    }
}
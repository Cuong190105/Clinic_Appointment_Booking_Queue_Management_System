using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeWorkScheduleRepository : IWorkScheduleRepository
    {
        private readonly List<WorkSchedule> _workSchedules = [];
        private readonly List<ShiftRequest> _shiftRequests = [];

        // ===== Nhóm chức năng: đặt lịch khám (booking) =====

        public Task<WorkSchedule?> GetByIdAsync(Guid id)
        {
            var result = _workSchedules.FirstOrDefault(w => w.Id == id && w.IsDeleted == false);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Fake trong bộ nhớ, không có DB thật nên không thể lock thật - no-op để interface
        /// biên dịch được và test có thể gọi CreateAppointmentHandler bình thường.
        /// </summary>
        public Task LockAsync(Guid workScheduleId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>Helper cho test: seed sẵn 1 WorkSchedule để GetByIdAsync tìm thấy.</summary>
        public void Add(WorkSchedule workSchedule)
        {
            _workSchedules.Add(workSchedule);
        }

        public Task<List<WorkSchedule>> GetAvailableSlotsAsync(Guid? doctorId, Guid? specialtyId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _workSchedules
                .Where(w => w.IsDeleted == false && w.Status == WorkScheduleStatus.Active && new DateTime(w.Date, w.ShiftEnd) > DateTime.UtcNow);

            if (doctorId.HasValue)
            {
                query = query.Where(w => w.DoctorId == doctorId.Value);
            }

            if (specialtyId.HasValue)
            {
                query = query.Where(w => w.Doctor != null && w.Doctor.WorkHistories.Any(wh =>
                    wh.SpecialtyId == specialtyId.Value && wh.EndDate == null));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(w => new DateTime(w.Date, w.ShiftStart) >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(w => new DateTime(w.Date, w.ShiftStart) <= toDate.Value);
            }

            query = query.Where(w => w.Appointments.Count(a => a.Status != AppointmentStatus.Cancelled) < w.PatientLimit);

            var result = query.OrderBy(w => w.ShiftStart).ToList();
            return Task.FromResult(result);
        }

        // ===== Nhóm chức năng: quản lý ca trực bác sĩ (shift request) =====

        public async Task AddShiftRequestAsync(ShiftRequest shiftRequest)
        {
            _shiftRequests.Add(shiftRequest);
        }

        public async Task AddWorkScheduleAsync(WorkSchedule workSchedule)
        {
            _workSchedules.Add(workSchedule);
        }

        public async Task<IEnumerable<WorkSchedule>> GetDoctorSchedulesWithAppointmentByDateAsync(Guid doctorId, DateOnly date)
        {
            List<WorkSchedule> result = _workSchedules
                .Where(ws => ws.DoctorId == doctorId &&
                ws.IsDeleted == false &&
                ws.Status == WorkScheduleStatus.Active &&
                ws.Date == date).ToList();
            List<WorkSchedule> returnAns = new List<WorkSchedule>();
            foreach (var schedule in result)
            {
                // workaround to filter inclusion to preserve object stored in context, but lose the reference to other entity
                var ws = new WorkSchedule(schedule.Id, schedule.DoctorId, schedule.Date, schedule.ShiftStart, schedule.ShiftEnd, schedule.PatientLimit, schedule.Status, schedule.CreatedAt, schedule.UpdatedAt, schedule.IsDeleted);
                foreach (var appointment in schedule.Appointments)
                {
                    if (!appointment.IsDeleted && appointment.Status != AppointmentStatus.Cancelled)
                    {
                        ws.AddAppointment(appointment);
                    }
                }
                returnAns.Add(ws);
            }
            return returnAns;
        }

        public async Task<IEnumerable<WorkSchedule>> GetDoctorSchedulesWithAppointmentByDateAsync(Guid? doctorId, DateOnly date)
        {
            List<WorkSchedule> result = _workSchedules
                .Where(ws => ws.DoctorId == doctorId &&
                ws.IsDeleted == false &&
                ws.Status == WorkScheduleStatus.Active &&
                ws.Date == date).ToList();
            List<WorkSchedule> returnAns = new List<WorkSchedule>();
            foreach (var schedule in result)
            {
                // workaround to filter inclusion to preserve object stored in context, but lose the reference to other entity
                var ws = new WorkSchedule(schedule.Id, schedule.DoctorId, schedule.Date, schedule.ShiftStart, schedule.ShiftEnd, schedule.PatientLimit, schedule.Status, schedule.CreatedAt, schedule.UpdatedAt, schedule.IsDeleted);
                foreach (var appointment in schedule.Appointments)
                {
                    if (!appointment.IsDeleted && appointment.Status != AppointmentStatus.Cancelled)
                    {
                        ws.AddAppointment(appointment);
                    }
                }
                returnAns.Add(ws);
            }
            return returnAns;
        }

        public async Task<IEnumerable<WorkSchedule>> GetPlannedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate)
        {
            // Check if the time range exceeds 1 month
            if (startDate.AddMonths(1) < endDate)
            {
                throw new ArgumentException("The time range cannot exceed 1 month.");
            }

            return _workSchedules.Where(ws => ws.DoctorId == doctorId &&
                ws.Date >= startDate && ws.Date <= endDate && ws.IsDeleted == false && ws.Status != WorkScheduleStatus.Cancelled
            ).ToList();
        }

        public async Task<IEnumerable<ShiftRequest>> GetRequestedSchedulesByDoctorIdAsync(Guid? doctorId, DateOnly startDate, DateOnly endDate)
        {
            // Check if the time range exceeds 1 month
            if (startDate.AddMonths(1) < endDate)
            {
                throw new ArgumentException("The time range cannot exceed 1 month.");
            }

            return _shiftRequests.Where(sr => sr.DoctorId == doctorId &&
                sr.Date >= startDate && sr.Date <= endDate && sr.IsDeleted == false && sr.Status != ShiftRequestStatus.Cancelled
            ).ToList();
        }

        public async Task<ShiftRequest?> GetShiftRequestByIdAsync(Guid scheduleId)
        {
            return _shiftRequests.FirstOrDefault(sr => sr.Id == scheduleId && sr.IsDeleted == false);
        }

        public async Task<WorkSchedule?> GetWorkScheduleByIdAsync(Guid scheduleId)
        {
            return _workSchedules.FirstOrDefault(ws => ws.Id == scheduleId && ws.IsDeleted == false);
        }

        public async Task<bool> HasDuplicateShiftRequest(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentSRId = null)
        {
            return _shiftRequests.Any(sr => sr.DoctorId == doctorId &&
                sr.Date == date &&
                ((sr.ShiftStart == startTime && sr.ShiftEnd == endTime) && (currentSRId == null || sr.Id != currentSRId)));
        }

        public async Task<bool> HasOverlappingWorkSchedule(Guid doctorId, DateOnly date, TimeOnly startTime, TimeOnly endTime, Guid? currentWSId = null)
        {
            return _workSchedules.Any(ws => ws.DoctorId == doctorId &&
                ws.Date == date &&
                ((ws.ShiftStart < endTime && ws.ShiftEnd > startTime) && (currentWSId == null || ws.Id != currentWSId)));
        }
    }
}
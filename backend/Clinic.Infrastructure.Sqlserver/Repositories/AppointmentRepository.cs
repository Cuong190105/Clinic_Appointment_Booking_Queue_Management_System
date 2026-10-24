using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly ApplicationDbContext _context;
        public AppointmentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<Appointment>> GetAppointmentsByPatientIdAsync(Guid patientId, string category)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.Person)
                .Include(a => a.QueueTicket)
                .Where(a => a.IsDeleted == false && a.PatientId == patientId);

            // Apply category filter if provided
            if (!string.IsNullOrEmpty(category))
            {
                query = category.ToLower() switch
                {
                    "upcoming" => query.Where(a => new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn }.Contains(a.Status)),
                    "completed" => query.Where(a => a.Status == AppointmentStatus.Completed),
                    "cancelled" => query.Where(a => a.Status == AppointmentStatus.Cancelled),
                    _ => query
                };
            }

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.ScheduleDate).ThenByDescending(a => a.TimeSlot).AsNoTracking().ToListAsync();

            return new PagedResult<Appointment>
            (
                items: items,
                totalCount: count
            );
        }

        public async Task<Appointment?> GetByIdAsync(Guid appointmentId)
        {
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.Person)
                .Include(a => a.QueueTicket)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsDeleted == false);
        }

        public async Task AddAsync(Appointment appointment)
        {
            await _context.Appointments.AddAsync(appointment);
        }

        public async Task UpdateAsync(Appointment appointment)
        {
            _context.Appointments.Update(appointment);
        }

        public async Task<bool> ExistsForDoctorAndPatientAsync(Guid doctorId, Guid patientId)
        {
            return await _context.Appointments
                .AnyAsync(a => a.PatientId == patientId && a.DoctorId == doctorId);
        }

        public async Task<TotalAppointmentSummaryDto> GetTotalAppointmentSummaryAsync(DateOnly startDate, DateOnly endDate, Guid doctorId, Guid specialtyId)
        {
            var query = _context.Appointments
                .Include(a => a.QueueTicket)
                .Where(a => a.IsDeleted == false && a.ScheduleDate >= startDate && a.ScheduleDate <= endDate);

            if (doctorId != Guid.Empty)
            {
                query = query.Where(a => a.DoctorId == doctorId);
            }

            var result = await query.GroupBy(a => 1).Select(g => new TotalAppointmentSummaryDto
                {
                    AppointmentCount = g.Count(),
                    AppointmentOnlineCount = g.Count(a => a.IsWalkIn == false),
                    CompletedAppointments = g.Count(a => a.Status == AppointmentStatus.Completed),
                    CanceledAppointments = g.Count(a => a.Status == AppointmentStatus.Cancelled),
                    NoShowAppointments = g.Count(a => a.Status == AppointmentStatus.NoShow),
                    CancellationRate = g.Count(a => a.IsWalkIn == false) == 0 ? 0 : (double)g.Count(a => a.Status == AppointmentStatus.Cancelled && a.IsWalkIn == false) / g.Count(a => a.IsWalkIn == false),
                    AverageWaitingMinutes = g.Where(a => a.Status == AppointmentStatus.Completed).Average(a => EF.Functions.DateDiffMinute(a.QueueTicket.CheckInTime, a.QueueTicket.CalledAt)) ?? 0
                }).FirstOrDefaultAsync();

            return result;
        }

        public async Task<Appointment?> GetAppointmentChangelogAsync(Guid appointmentId)
        {
            var query = _context.Appointments.Include(a => a.Snapshots)
                .Include(a => a.Patient).ThenInclude(p => p.Person)
                .Where(a => a.Id == appointmentId);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<Dictionary<string, IValueWithChange>> GetDashboardData()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var yesterday = today.AddDays(-1);

            var currentPeriodStart = today.AddDays(-30);
            var previousPeriodStart = today.AddDays(-60);

            var stats = await _context.Appointments
                .Where(a => !a.IsDeleted)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    CurrentTotal = g.Count(a => a.ScheduleDate >= currentPeriodStart && a.ScheduleDate < today),
                    PreviousTotal = g.Count(a => a.ScheduleDate >= previousPeriodStart && a.ScheduleDate < currentPeriodStart),
                    TodayTotal = g.Count(a => a.ScheduleDate == today),
                    YesterdayTotal = g.Count(a => a.ScheduleDate == yesterday),
                    CurrentCompleted = g.Count(a => a.Status == AppointmentStatus.Completed && a.ScheduleDate >= currentPeriodStart && a.ScheduleDate < today),
                    PreviousCompleted = g.Count(a => a.Status == AppointmentStatus.Completed && a.ScheduleDate >= previousPeriodStart && a.ScheduleDate < currentPeriodStart),
                    CurrentWaitingTime = g.Where(a => a.Status == AppointmentStatus.Completed && a.ScheduleDate >= currentPeriodStart && a.ScheduleDate < today)
                        .Average(a => EF.Functions.DateDiffMinute(a.QueueTicket.CheckInTime, a.QueueTicket.CalledAt)) ?? 0,
                    PreviousWaitingTime = g.Where(a => a.Status == AppointmentStatus.Completed && a.ScheduleDate >= previousPeriodStart && a.ScheduleDate < currentPeriodStart)
                        .Average(a => EF.Functions.DateDiffMinute(a.QueueTicket.CheckInTime, a.QueueTicket.CalledAt)) ?? 0
                })
                .FirstOrDefaultAsync();

            var currentTotal = stats?.CurrentTotal ?? 0;
            var previousTotal = stats?.PreviousTotal ?? 0;
            var todayTotal = stats?.TodayTotal ?? 0;
            var yesterdayTotal = stats?.YesterdayTotal ?? 0;
            var currentCompleted = stats?.CurrentCompleted ?? 0;
            var previousCompleted = stats?.PreviousCompleted ?? 0;
            var currentWaitingTime = stats?.CurrentWaitingTime ?? 0;
            var previousWaitingTime = stats?.PreviousWaitingTime ?? 0;

            var currentCompletionRate = currentTotal == 0 ? 0 : (double)currentCompleted / currentTotal * 100;
            var previousCompletionRate = previousTotal == 0 ? 0 : (double)previousCompleted / previousTotal * 100;

            return new Dictionary<string, IValueWithChange>
            {
                ["TotalAppointmentsLast30Days"] = new ValueWithChange<int>
                {
                    Value = currentTotal,
                    Change = CalculatePercentChange(currentTotal, previousTotal)
                },
                ["TodayAppointments"] = new ValueWithChange<int>
                {
                    Value = todayTotal,
                    Change = CalculatePercentChange(todayTotal, yesterdayTotal)
                },
                ["CompletionRateLast30Days"] = new ValueWithChange<double>
                {
                    Value = Math.Round(currentCompletionRate),
                    Change = CalculatePercentChange(currentCompletionRate, previousCompletionRate)
                },
                ["AverageWaitingTimeLast30Days"] = new ValueWithChange<double>
                {
                    Value = Math.Round(currentWaitingTime),
                    Change = CalculatePercentChange(currentWaitingTime, previousWaitingTime)
                }
            };
        }

        private static double CalculatePercentChange(double currentValue, double previousValue)
        {
            if (previousValue == 0)
            {
                return currentValue == 0 ? 0 : 100;
            }

            return (currentValue - previousValue) / previousValue * 100;
        }

        public async Task<StatisticsDataDto> GetStatistics(bool isWeekPeriod)
        {
            var totalDays = isWeekPeriod ? 7 : 30;
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var startDate = endDate.AddDays(-(totalDays - 1));

            var countsByDate = await _context.Appointments
                .Where(a => !a.IsDeleted && a.ScheduleDate >= startDate && a.ScheduleDate <= endDate)
                .GroupBy(a => a.ScheduleDate)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var countLookup = countsByDate.ToDictionary(x => x.Date, x => x.Count);
            var appointmentsByDay = new List<AppointmentByDayDto>(totalDays);

            for (var i = 0; i < totalDays; i++)
            {
                var date = startDate.AddDays(i);
                appointmentsByDay.Add(new AppointmentByDayDto
                {
                    Date = date,
                    Count = countLookup.TryGetValue(date, out var value) ? value : 0
                });
            }

            var appointmentsByStatus = await _context.Appointments
                .Where(a => !a.IsDeleted && a.ScheduleDate >= startDate && a.ScheduleDate <= endDate)
                .GroupBy(a => a.Status)
                .Select(g => new AppointmentByStatusDto
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return new StatisticsDataDto
            {
                Period = isWeekPeriod ? "week" : "month",
                AppointmentsByDay = appointmentsByDay,
                AppointmentsByStatus = appointmentsByStatus
            };
        }

        public async Task<PagedResult<Appointment>> GetAppointmentsByDateAsync(DateOnly date, int pageNumber, int pageSize)
        {
            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.Person)
                .Where(a => !a.IsDeleted && a.ScheduleDate == date);

            var count = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.TimeSlot)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return new PagedResult<Appointment>(items, count);
        }

        public async Task<PagedResult<Appointment>> GetPendingAppointmentsAsync(string search, string sortBy, bool descending, int pageNumber, int pageSize)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.Person)
                .Where(a => !a.IsDeleted && a.Status == AppointmentStatus.Pending && a.ScheduleDate >= today);

            // Apply search filter if provided
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => (a.Patient.Person.FullName != null && a.Patient.Person.FullName.Contains(search)) ||
                                         (a.DoctorName != null && a.DoctorName.Contains(search)));
            }

            // Apply sorting
            if (string.IsNullOrEmpty(sortBy))
            {
                sortBy = "createdAt"; // Default sorting by CreatedAt
            }

            query = sortBy.ToLower() switch
            {
                "patientname" => descending ? query.OrderByDescending(a => a.Patient.Person.FullName) : query.OrderBy(a => a.Patient.Person.FullName),
                "doctorname" => descending ? query.OrderByDescending(a => a.DoctorName) : query.OrderBy(a => a.DoctorName),
                "createdat" => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
                "date" => descending ? query.OrderByDescending(a => a.ScheduleDate).ThenByDescending(a => a.TimeSlot) : query.OrderBy(a => a.ScheduleDate).ThenBy(a => a.TimeSlot),
                _ => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
            };

            var count = await query.CountAsync();
            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();
            // Return paged results
            return new PagedResult<Appointment>(items, count);
        }
    }
}

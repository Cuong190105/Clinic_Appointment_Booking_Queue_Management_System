using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeAppointmentRepository : IAppointmentRepository
    {
        private readonly List<Appointment> _appointments = [];

        public Task<bool> ExistsForDoctorAndPatientAsync(Guid doctorId, Guid patientId)
        {
            var exists = _appointments.Any(a => !a.IsDeleted && a.PatientId == patientId && a.WorkSchedule.DoctorId == doctorId);
            return Task.FromResult(exists);
        }

        public async Task<PagedResult<Appointment>> GetAppointmentsByPatientIdAsync(Guid patientId, string category)
        {
            var query = _appointments
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

            var items = query.OrderByDescending(a => a.WorkSchedule.Date).ThenByDescending(a => a.TimeSlot).ToList();

            return new PagedResult<Appointment>
            (
                items: items,
                totalCount: items.Count
            );
        }

        public async Task<Appointment?> GetByIdAsync(Guid appointmentId)
        {
            return _appointments.FirstOrDefault(a => a.Id == appointmentId && !a.IsDeleted);
        }

        public async Task<TotalAppointmentSummaryDto> GetTotalAppointmentSummaryAsync(DateOnly startDate, DateOnly endDate, Guid doctorId, Guid specialtyId)
        {
            var query = _appointments
                .Where(a => a.IsDeleted == false && a.WorkSchedule.Date >= startDate && a.WorkSchedule.Date <= endDate);

            if (doctorId != Guid.Empty)
            {
                query = query.Where(a => a.WorkSchedule.DoctorId == doctorId);
            }
            else if (specialtyId != Guid.Empty)
            {
                query = query.Where(a => a.WorkSchedule.Doctor.WorkHistories.Any(wh => wh.SpecialtyId == specialtyId));
            }

            var result = query.GroupBy(a => 1).Select(g => new TotalAppointmentSummaryDto
            {
                AppointmentCount = g.Count(),
                AppointmentOnlineCount = g.Count(a => a.IsWalkIn == false),
                CompletedAppointments = g.Count(a => a.Status == AppointmentStatus.Completed),
                CanceledAppointments = g.Count(a => a.Status == AppointmentStatus.Cancelled),
                NoShowAppointments = g.Count(a => a.Status == AppointmentStatus.NoShow),
                CancellationRate = g.Count(a => a.IsWalkIn == false) == 0 ? 0 : (double)g.Count(a => a.Status == AppointmentStatus.Cancelled && a.IsWalkIn == false) / g.Count(a => a.IsWalkIn == false),
                AverageWaitingMinutes = g.Where(a => a.Status == AppointmentStatus.Completed).Average(a => (a.QueueTicket!.CalledAt - a.QueueTicket!.CheckInTime).Value.TotalMinutes)
            }).FirstOrDefault();

            return result;
        }

        public Task<bool> IsTimeSlotTakenAsync(Guid doctorId, DateTime timeSlot)
        {
            throw new NotImplementedException();
        }

        public async Task AddAsync(Appointment appointment)
        {
            _appointments.Add(appointment);
        }

        public async Task<Appointment?> GetAppointmentChangelogAsync(Guid appointmentId)
        {
            return _appointments.FirstOrDefault(a => a.Id == appointmentId && !a.IsDeleted);
        }

        public Task<Dictionary<string, IValueWithChange>> GetDashboardData()
        {
            throw new NotImplementedException();
        }

        public Task<StatisticsDataDto> GetStatistics(bool isWeekPeriod)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<Appointment>> GetAppointmentsByDateAsync(DateOnly date, int pageNumber, int pageSize)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<Appointment>> GetPendingAppointmentsAsync(string search, string sortBy, bool descending, int pageNumber, int pageSize)
        {
            throw new NotImplementedException();
        }
    }
}

using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface IAppointmentRepository
    {
        /// <summary>
        /// Gets a paged list of appointments for a specific patient, filtered by category.
        /// </summary>
        Task<PagedResult<Appointment>> GetAppointmentsByPatientIdAsync(Guid patientId, string category);

        /// <summary>
        /// Gets an appointment by its ID, Include related doctor and patient information.
        /// </summary>
        Task<Appointment?> GetByIdAsync(Guid appointmentId);

        /// <summary>
        /// Kiểm tra bác sĩ có từng/đang phụ trách ca khám nào của bệnh nhân này không
        /// (tồn tại Appointment mà WorkSchedule.DoctorId = doctorId và PatientId = patientId).
        /// Dùng để scope "related" cho medical-report.view/patient-history.view của Doctor.
        /// </summary>
        Task<bool> ExistsForDoctorAndPatientAsync(Guid doctorId, Guid patientId);

        /// <summary>
        /// Gets a summary of total appointments within a specified date range, including counts of completed, waiting, canceled, and no-show appointments, as well as average waiting time and cancellation rate.
        /// </summary>
        Task<TotalAppointmentSummaryDto> GetTotalAppointmentSummaryAsync(DateOnly startDate, DateOnly endDate, Guid doctorId, Guid specialtyId);

        /// <summary>
        /// Gets the changelog of an appointment, including all snapshots of the appointment's state over time, doctor, patient, and updator information.
        /// </summary>
        Task<Appointment?> GetAppointmentChangelogAsync(Guid appointmentId);
        Task AddAsync(Appointment appointment);
        Task<Dictionary<string, IValueWithChange>> GetDashboardData();
        Task<StatisticsDataDto> GetStatistics(bool isWeekPeriod);
        Task<PagedResult<Appointment>> GetAppointmentsByDateAsync(DateOnly date, int pageNumber, int pageSize);
        Task<PagedResult<Appointment>> GetPendingAppointmentsAsync(string search, string sortBy, bool descending, int pageNumber, int pageSize);
    }
}

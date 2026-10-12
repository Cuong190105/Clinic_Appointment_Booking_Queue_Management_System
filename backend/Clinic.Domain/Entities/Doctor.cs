        using Clinic.Domain.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Clinic.Domain.Entities
{
    public class Doctor : BaseEntity
    {
        /// <summary>FK, UNIQUE - mỗi Doctor gắn với đúng 1 Employee.</summary>
        public Guid EmployeeId { get; protected set; }
        public Employee Employee { get; protected set; } = null!;

        public string LicenseNumber { get; protected set; } = string.Empty;

        public int ExperienceYears { get; protected set; }

        public string Qualification { get; protected set; }

        public string? Biography { get; protected set; }
        public DoctorStatus Status { get; protected set; } = DoctorStatus.Active;

        public ICollection<WorkSchedule> WorkSchedules { get; protected set; } = new List<WorkSchedule>();

        public ICollection<WorkHistory> WorkHistories { get; protected set; } = new List<WorkHistory>();

        public ICollection<ShiftRequest> ShiftRequests { get; protected set; } = new List<ShiftRequest>();

        public Doctor(Employee employee, string licenseNumber, string qualification, Specialty specialty = null, int experienceYears = 0, string? biography = null, DoctorStatus status = DoctorStatus.Active)
        {
            if (experienceYears < 0) throw new ArgumentException(nameof(experienceYears), "Experience years cannot be negative.");
            if (employee == null) throw new ArgumentNullException(nameof(employee));
            if (string.IsNullOrWhiteSpace(licenseNumber)) throw new ArgumentNullException(nameof(licenseNumber));
            if (string.IsNullOrWhiteSpace(qualification)) throw new ArgumentNullException(nameof(qualification));
            ArgumentNullException.ThrowIfNull(specialty, nameof(specialty));
            Employee = employee;
            EmployeeId = employee.Id;
            LicenseNumber = licenseNumber ?? throw new ArgumentNullException(nameof(licenseNumber));
            ExperienceYears = experienceYears;
            Qualification = qualification;
            Biography = biography;
            Status = status;
            WorkHistories.Add(new WorkHistory(this, specialty, new TimeConverter().Today));
        }

        public Doctor(Guid id, Guid employeeId, string licenseNumber, string qualification, int experienceYears, string? biography, DoctorStatus status, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            EmployeeId = employeeId;
            LicenseNumber = licenseNumber ?? throw new ArgumentNullException(nameof(licenseNumber));
            ExperienceYears = experienceYears;
            Qualification = qualification ?? throw new ArgumentNullException(nameof(qualification));
            Biography = biography;
            Status = status;
        }

        /// <summary>
        /// Updates the status of the doctor.
        /// </summary>
        public void UpdateStatus(DoctorStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                MarkUpdated();
            }
        }

        /// <summary>
        /// Updates the doctor's information, including license number, qualification, experience years, and biography.
        /// </summary>
        public void UpdateInfo(string licenseNumber, string qualification, int experienceYears = 0, string? biography = null)
        {
            if (experienceYears < 0) throw new ArgumentException(nameof(experienceYears), "Experience years cannot be negative.");
            if (string.IsNullOrWhiteSpace(licenseNumber)) throw new ArgumentNullException(nameof(licenseNumber));
            if (string.IsNullOrWhiteSpace(qualification)) throw new ArgumentNullException(nameof(qualification));
            LicenseNumber = licenseNumber;
            Qualification = qualification;
            ExperienceYears = experienceYears;
            Biography = biography;
            MarkUpdated();
        }

        public void Delete()
        {
            if (!IsDeleted)
            {
                IsDeleted = true;
                MarkUpdated();
            }
        }

        /// <summary>
        /// Adds a new work schedule for the doctor. This method checks for overlapping schedules and throws an exception if the new schedule conflicts with existing ones.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddWorkSchedule(WorkSchedule workSchedule)
        {
            if (workSchedule == null) throw new ArgumentNullException(nameof(workSchedule));
            if (WorkSchedules.Where(ws => !ws.IsDeleted && ws.Status != WorkScheduleStatus.Cancelled && ws.Date == workSchedule.Date && ws.ShiftStart < workSchedule.ShiftEnd && ws.ShiftEnd > workSchedule.ShiftStart).Any())
            {
                throw new ConflictException("An existing work schedule conflicts with that schedule.");
            }
            WorkSchedules.Add(workSchedule);
        }

        /// <summary>
        /// Removes an existing work schedule for the doctor. This method checks if the schedule exists and throws an exception if it does not.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void RemoveWorkSchedule(WorkSchedule workSchedule)
        {
            if (workSchedule == null) throw new ArgumentNullException(nameof(workSchedule));
            if (!WorkSchedules.Remove(workSchedule))
            {
                throw new InvalidOperationException("Work schedule not found.");
            }
        }

        /// <summary>
        /// Change the specialty of the doctor when they transition to a new role or retire.
        /// This method will end the current work history (if any) and create a new work history with the new specialty.
        /// If the doctor is currently not working in any specialty, it will simply create a new work history with the new specialty.
        /// If the new specialty is null, it will only end the current work history without creating a new one.
        /// </summary>
        public void ChangeSpecialty(Specialty? newSpecialty = null)
        {
            var currentWorkHistory = WorkHistories.FirstOrDefault(wh => wh.EndDate == null);
            currentWorkHistory?.EndWorkHistory();
            if (newSpecialty != null && newSpecialty.IsDeleted == false)
            {
                var newWorkHistory = new WorkHistory(this, newSpecialty, new TimeConverter().Today);
                WorkHistories.Add(newWorkHistory);
            }
            MarkUpdated();
        }

        /// <summary>
        /// Requests a shift for the doctor. This method adds a new shift request to the doctor's list of shift requests.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>"
        public void AddShiftRequest(ShiftRequest shiftRequest)
        {
            if (shiftRequest == null) throw new ArgumentNullException(nameof(shiftRequest));
            if (ShiftRequests.Any(sr =>
                !sr.IsDeleted &&
                (sr.Status == ShiftRequestStatus.Pending || sr.Status == ShiftRequestStatus.Approved) &&
                sr.Date == shiftRequest.Date &&
                sr.ShiftStart == shiftRequest.ShiftStart &&
                sr.ShiftEnd == shiftRequest.ShiftEnd))
            {
                throw new ConflictException("A request for this shift already exists.");
            }
            ShiftRequests.Add(shiftRequest);
        }

        /// <summary>
        /// Removes a shift request for the doctor. This method removes an existing shift request from the doctor's list of shift requests.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void RemoveShiftRequest(ShiftRequest shiftRequest)
        {
            if (shiftRequest == null) throw new ArgumentNullException(nameof(shiftRequest));
            if (!ShiftRequests.Remove(shiftRequest))
            {
                throw new InvalidOperationException("Shift request not found.");
            }
        }
    }
}

using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Sổ nhật ký lịch sử chuyển khoa của bác sĩ
    /// </summary>
    public class WorkHistory : BaseEntity
    {
        public WorkHistory(Doctor doctor, Specialty newSpecialty, DateOnly startDate)
        {
            if (doctor == null) throw new ArgumentNullException(nameof(doctor));
            if (newSpecialty == null) throw new ArgumentNullException(nameof(newSpecialty));
            if (startDate < doctor.Employee.HireDate) throw new ArgumentException("Start date cannot be earlier than the doctor's hire date.", nameof(startDate));
            Doctor = doctor;
            Specialty = newSpecialty;
            DoctorId = doctor.Id;
            StartDate = startDate;
            SpecialtyId = newSpecialty.Id;
        }

        public WorkHistory(Guid id, Guid doctorId, Guid specialtyId, DateOnly startDate, DateOnly? endDate, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            DoctorId = doctorId;
            SpecialtyId = specialtyId;
            StartDate = startDate;
            EndDate = endDate;
        }

        public Guid DoctorId { get; protected set; }
        public Doctor Doctor { get; protected set; } = null!;

        public Guid SpecialtyId { get; protected set; }
        public Specialty Specialty { get; protected set; } = null!;

        public DateOnly StartDate { get; protected set; }

        /// <summary>NULL nếu đây là giai đoạn hiện tại (chưa kết thúc).</summary>
        public DateOnly? EndDate { get; protected set; }

        public void EndWorkHistory(DateOnly? endDate = null)
        {
            if (EndDate != null) throw new InvalidOperationException("Work history has already ended.");
            if (endDate != null && endDate < StartDate) throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));
            EndDate = endDate ?? new TimeConverter().Today;
            MarkUpdated();
        }
    }
}

using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    public class ShiftRequest : BaseEntity
    {
        public Guid DoctorId { get; protected set; }
        public Doctor Doctor { get; protected set; } = null!;
        public DateOnly Date { get; set; }

        public TimeOnly ShiftStart { get; protected set; }

        public TimeOnly ShiftEnd { get; protected set; }

        public int PatientLimit { get; protected set; }

        public string? Reason { get; protected set; }

        public ShiftRequestStatus Status { get; protected set; } = ShiftRequestStatus.Pending;

        public ShiftRequest(Doctor doctor, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int patientLimit, string? reason)
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(date, shiftStart));
            if (utcStart < DateTime.UtcNow) throw new ArgumentException($"Shift start time must be in the future.");
            if (doctor == null) throw new ArgumentNullException(nameof(doctor));
            if (shiftStart >= shiftEnd) throw new ArgumentException("Shift start time must be before shift end time.");
            if (patientLimit <= 0) throw new ArgumentException("Patient limit per slot must be greater than zero.");
            Doctor = doctor;
            DoctorId = doctor.Id;
            Date = date;
            ShiftStart = shiftStart;
            ShiftEnd = shiftEnd;
            PatientLimit = patientLimit;
            Reason = reason;
        }

        public ShiftRequest(Guid id, Guid doctorId, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int patientLimit, string? reason, ShiftRequestStatus status, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            DoctorId = doctorId;
            Date = date;
            ShiftStart = shiftStart;
            ShiftEnd = shiftEnd;
            PatientLimit = patientLimit;
            Reason = reason;
            Status = status;
        }

        public void UpdateShift(DateOnly date, TimeOnly newShiftStart, TimeOnly newShiftEnd, int newPatientLimit, string? newReason)
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(Date, ShiftStart));
            var newUtcStart = new TimeConverter().ConvertToUtc(new DateTime(date, newShiftStart));
            if (utcStart <= DateTime.UtcNow) throw new InvalidOperationException("Cannot update a shift that has already started.");
            if (Status != ShiftRequestStatus.Pending) throw new InvalidOperationException("Only pending shift requests can be updated.");
            if (newShiftStart >= newShiftEnd) throw new ArgumentException("New shift start time must be before new shift end time.");
            if (newUtcStart < DateTime.UtcNow) throw new ArgumentException("New shift start time must be in the future.");
            if (newPatientLimit <= 0) throw new ArgumentException("Patient limit per slot must be greater than zero.");
            Date = date;
            ShiftStart = newShiftStart;
            ShiftEnd = newShiftEnd;
            PatientLimit = newPatientLimit;
            Reason = newReason;
            MarkUpdated();
        }

        public void Delete()
        {
            IsDeleted = true;
            MarkUpdated();
        }

        public WorkSchedule Approve()
        {
            if (Status != ShiftRequestStatus.Pending) throw new InvalidOperationException("Only pending shift requests can be approved.");
            Status = ShiftRequestStatus.Approved;
            var workSchedule = new WorkSchedule(Doctor, Date, ShiftStart, ShiftEnd, PatientLimit);
            Doctor.AddWorkSchedule(workSchedule);
            MarkUpdated();
            return workSchedule;
        }

        public void Cancel()
        {
            if (Status != ShiftRequestStatus.Pending) throw new InvalidOperationException("Only pending shift requests can be canceled.");
            Status = ShiftRequestStatus.Cancelled;
            MarkUpdated();
        }

        public void Reject()
        {
            if (Status != ShiftRequestStatus.Pending) throw new InvalidOperationException("Only pending shift requests can be rejected.");
            Status = ShiftRequestStatus.Rejected;
            MarkUpdated();
        }
    }
}

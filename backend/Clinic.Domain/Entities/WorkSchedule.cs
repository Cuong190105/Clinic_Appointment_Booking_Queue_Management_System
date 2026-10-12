using Clinic.Domain.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Clinic.Domain.Entities
{
    /// <summary>Một khung giờ làm việc của bác sĩ, giới hạn số bệnh nhân tối đa.</summary>
    public class WorkSchedule : BaseEntity
    {
        public const int SlotIntervalMinutes = 15; // Khoảng thời gian giữa các slot hẹn (15 phút)
        public Guid DoctorId { get; protected set; }
        public Doctor Doctor { get; protected set; } = null!;

        public DateOnly Date {  get; protected set; }

        public TimeOnly ShiftStart { get; protected set; }

        public TimeOnly ShiftEnd { get; protected set; }

        public int PatientLimit { get; protected set; }

        public WorkScheduleStatus Status { get; protected set; } = WorkScheduleStatus.Active;
        public string? CancellationReason { get; protected set; }

        public ICollection<Appointment> Appointments { get; protected set; } = new List<Appointment>();

        public WorkSchedule(Doctor doctor, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int patientLimit)
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(date, shiftStart));
            if (doctor == null) throw new ArgumentNullException(nameof(doctor));
            if (utcStart < DateTime.UtcNow) throw new ArgumentException("Shift start time must be in the future.");
            if (shiftStart >= shiftEnd) throw new ArgumentException("Shift start time must be before shift end time.");
            if (patientLimit <= 0) throw new ArgumentException("Patient limit per slot must be greater than zero.");
            Doctor = doctor;
            DoctorId = doctor.Id;
            Date = date;
            ShiftStart = shiftStart;
            ShiftEnd = shiftEnd;
            PatientLimit = patientLimit;
        }

        public WorkSchedule(Guid id, Guid doctorId, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int patientLimit, WorkScheduleStatus status, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            DoctorId = doctorId;
            Date = date;
            ShiftStart = shiftStart;
            ShiftEnd = shiftEnd;
            PatientLimit = patientLimit;
            Status = status;
        }

        public void UpdateShift(DateOnly newDate, TimeOnly newShiftStart, TimeOnly newShiftEnd, int newPatientLimit)
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(Date, ShiftStart));
            var utcNewStart = new TimeConverter().ConvertToUtc(new DateTime(newDate, newShiftStart));

            if (utcStart <= DateTime.UtcNow && utcNewStart != utcStart) throw new InvalidOperationException("Cannot update the start time of a shift that has already started.");
            if (utcNewStart != utcStart && utcNewStart < DateTime.UtcNow) throw new ArgumentException("New shift start time must be in the future.");
            if (newShiftStart >= newShiftEnd) throw new ArgumentException("New shift start time must be before new shift end time.");
            if (newPatientLimit <= 0) throw new ArgumentException("New patient limit per slot must be greater than zero.");
            Date = newDate;
            ShiftStart = newShiftStart;
            ShiftEnd = newShiftEnd;
            PatientLimit = newPatientLimit;
            MarkUpdated();
        }

        public void UpdateStatus(WorkScheduleStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                MarkUpdated();
            }
        }

        public void Delete()
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(Date, ShiftStart));
            if (utcStart <= DateTime.UtcNow) throw new InvalidOperationException("Cannot delete a shift that has already started.");
            if (Appointments.Count > 0) throw new InvalidOperationException("Cannot delete a shift that has appointments.");
            IsDeleted = true;
            MarkUpdated();
        }

        public void Cancel(string reason)
        {
            var utcStart = new TimeConverter().ConvertToUtc(new DateTime(Date, ShiftStart));
            if (utcStart <= DateTime.UtcNow) throw new InvalidOperationException("Cannot cancel a shift that has already started.");
            CancellationReason = reason;
            Status = WorkScheduleStatus.Cancelled;
            MarkUpdated();
        }

        /// <summary>
        /// Adds an appointment to the work schedule if it doesn't conflict with existing appointments and the schedule is not full or cancelled.
        /// If the appointment already belongs to this work schedule, it will not be added again.
        /// </summary>
        /// <param name="appointment"></param>
        /// <exception cref="ConflictException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddAppointment(Appointment appointment)
        {
            // Only check for conflicts if the appointment is not a walk-in.
            if (!appointment.IsWalkIn)
            {
                if (Appointments.Any(a => !a.IsWalkIn && a.TimeSlot == appointment.TimeSlot && a.Status != AppointmentStatus.Cancelled && !a.IsDeleted && a.Id != appointment.Id))
                    throw new ConflictException("An appointment already exists for this time slot.");
                if (Status == WorkScheduleStatus.Full)
                {
                    throw new ConflictException("Cannot add an appointment to a full work schedule.");
                }
            }
            if (Appointments.Any(a => a.Id == appointment.Id))
            {
                return; // Appointment already belongs to this work schedule
            }
            if (appointment.TimeSlot < ShiftStart || appointment.TimeSlot >= ShiftEnd)
            {
                throw new ArgumentOutOfRangeException(nameof(appointment.TimeSlot), $"Appointment time slot must be within the work schedule ({ShiftStart} - {ShiftEnd}).");
            }
            if (Status == WorkScheduleStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot add an appointment to a cancelled work schedule.");
            }
            Appointments.Add(appointment);
            if (Appointments.Count(a => a.Status != AppointmentStatus.Cancelled && a.IsDeleted == false) >= PatientLimit)
            {
                Status = WorkScheduleStatus.Full;
            }
        }
    }
}

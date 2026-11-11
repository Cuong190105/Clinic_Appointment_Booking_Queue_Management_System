using Clinic.Domain.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;
using System;

namespace Clinic.Domain.Entities
{
    public class Appointment : BaseEntity
    {
        private const int CancelLimitHours = 24;

        public Guid PatientId { get; protected set; }
        public Patient Patient { get; protected set; } = null!;

        public Guid WorkScheduleId { get; protected set; }
        public WorkSchedule WorkSchedule { get; protected set; } = null!;

        public string Reason { get; protected set; }

        public TimeOnly TimeSlot { get; protected set; }

        public AppointmentStatus Status { get; protected set; } = AppointmentStatus.Pending;

        public bool IsWalkIn { get; protected set; } = false;

        public string? IdempotencyKey { get; set; }

        public MedicalReport? MedicalReport { get; set; }

        public Guid UpdatedByUserId { get; protected set; }

        /// <summary>0..1 - chá»‰ cÃ³ sau khi bá»‡nh nhÃ¢n check-in.</summary>
        public QueueTicket? QueueTicket { get; protected set; }

        public ICollection<AppointmentSnapshot> Snapshots { get; protected set; } = [];

        public User Updator { get; protected set; } = null!;

        private Appointment() { } // For EF Core

        public Appointment( // For seeding sample data
            Guid id,
            Guid patientId,
            Guid workScheduleId,
            TimeOnly timeSlot,
            string reason,
            AppointmentStatus status,
            bool isWalkIn,
            Guid updatedByUserId,
            DateTime createdAt,
            DateTime? updatedAt,
            bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            PatientId = patientId;
            WorkScheduleId = workScheduleId;
            TimeSlot = timeSlot;
            Reason = reason;
            Status = status;
            IsWalkIn = isWalkIn;
            UpdatedByUserId = updatedByUserId;
        }

        public static Appointment CreateForTest(Patient patient, WorkSchedule workSchedule, TimeOnly timeSlot, string reason = "Reason", Guid? createdByUserId = null, bool isWalkIn = false)
        {
            var app = new Appointment(Guid.NewGuid(), patient.Id, workSchedule.Id, timeSlot, reason, AppointmentStatus.Pending, isWalkIn, createdByUserId ?? Guid.NewGuid(), DateTime.UtcNow, null, false);
            app.Patient = patient;
            app.WorkSchedule = workSchedule;
            return app;
        }

        public Appointment(Patient patient, WorkSchedule workSchedule, TimeOnly timeSlot, string reason, Guid createdByUserId, bool isWalkIn = false)
        {
            WorkSchedule = workSchedule ?? throw new ArgumentNullException(nameof(workSchedule));
            Patient = patient ?? throw new ArgumentNullException(nameof(patient));
            Reason = reason;
            if (timeSlot < workSchedule.ShiftStart || timeSlot >= workSchedule.ShiftEnd)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSlot), $"Time slot {timeSlot} must be within the work schedule ({workSchedule.ShiftStart} - {workSchedule.ShiftEnd}).");
            }
            var bookingTimeUtc = new TimeConverter().ConvertToUtc(new DateTime(workSchedule.Date, timeSlot));
            if (bookingTimeUtc < DateTime.UtcNow)
            {
                throw new ArgumentException("Cannot create an appointment for a past time.", nameof(workSchedule));
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
            }
            PatientId = patient.Id;
            WorkScheduleId = workSchedule.Id;
            TimeSlot = timeSlot;
            IsWalkIn = isWalkIn;
            UpdatedByUserId = createdByUserId;
        }

        public void AdminCancel(Guid adminId)
        {
            if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            {
                return; // No throw, if it passed, let it pass.
            }
            // Force cancellation by admin, no time limit check.
            if (QueueTicket != null && QueueTicket.Status is QueueStatus.Waiting or QueueStatus.Called)
            {
                QueueTicket.Cancel();
            }
            Snapshots.Add(new AppointmentSnapshot(this));
            Status = AppointmentStatus.Cancelled;
            UpdatedByUserId = adminId;
            MarkUpdated();
            WorkSchedule?.RecheckCapacity();
        }

        /// <summary>
        /// Check-in bá»‡nh nhÃ¢n táº¡i quáº§y/vÃ o hÃ ng Ä‘á»£i.
        /// </summary>
        /// <param name="checkInTime">Thá»i Ä‘iá»ƒm check-in thá»±c táº¿ (giá» há»‡ thá»‘ng).</param>
        /// <exception cref="ArgumentException">Appointment chÆ°a á»Ÿ tráº¡ng thÃ¡i Confirmed.</exception>
        /// <exception cref="ConflictException">
        /// TimeSlot cá»§a appointment khÃ´ng cÃ¹ng ngÃ y vá»›i checkInTime - vÃ­ dá»¥ lá»‹ch háº¹n cá»§a
        /// ngÃ y mai khÃ´ng Ä‘Æ°á»£c phÃ©p vÃ o hÃ ng Ä‘á»£i cá»§a ngÃ y hÃ´m nay (vÃ  ngÆ°á»£c láº¡i).
        /// </exception>
        public QueueTicket CheckIn(DateTime checkInTime, int queueNumber)
        {
            if (Status == AppointmentStatus.CheckedIn || QueueTicket != null) throw new ArgumentException("Lịch hẹn đã được check-in trước đó.");
            if (Status != AppointmentStatus.Confirmed)
            {
                throw new ArgumentException($"Cannot check in an appointment with status '{Status}'. Appointment must be confirmed first.");
            }

            var localCheckInDate = DateOnly.FromDateTime(new TimeConverter().ConvertToLocal(checkInTime));
            if (WorkSchedule.Date != localCheckInDate)
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(WorkSchedule.Date, TimeSlot):yyyy-MM-dd}, not today ({localCheckInDate:yyyy-MM-dd}).");
            }

            var queueTicket = new QueueTicket
            {
                AppointmentId = Id,
                QueueNumber = queueNumber,
                CheckInTime = checkInTime,
                QueueDate = WorkSchedule.Date,
                PatientName = Patient?.Person?.FullName,
                DoctorName = WorkSchedule.Doctor?.Employee?.Person?.FullName,
                Appointment = this
            };

            QueueTicket = queueTicket;
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
            return queueTicket;
        }

        public void Update(WorkSchedule workSchedule, TimeOnly timeSlot, string reason, Guid updatedByUserId)
        {
            if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            {
                throw new InvalidOperationException($"{Enum.GetName(Status)} appointments can't be updated.");
            }
            ArgumentNullException.ThrowIfNull(workSchedule);
            if (timeSlot < workSchedule.ShiftStart || timeSlot >= workSchedule.ShiftEnd)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSlot), $"Time slot must be within the work schedule ({workSchedule.ShiftStart} - {workSchedule.ShiftEnd}).");
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
            }
            if (WorkScheduleId == workSchedule.Id && TimeSlot == timeSlot && Reason == reason)
            {
                throw new InvalidOperationException("No changes detected in the appointment details.");
            }
            var bookingTimeUtc = new TimeConverter().ConvertToUtc(new DateTime(workSchedule.Date, timeSlot));
            if (bookingTimeUtc < DateTime.UtcNow)
            {
                throw new ArgumentException("Cannot create an appointment for a past time.", nameof(workSchedule));
            }
            Snapshots.Add(new AppointmentSnapshot(this));
            Reason = reason;
            WorkSchedule = workSchedule;
            WorkScheduleId = workSchedule.Id;
            TimeSlot = timeSlot;
            UpdatedByUserId = updatedByUserId;
            MarkUpdated();
        }

        public void Cancel(Guid cancelledByUserId)
        {
            if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            {
                throw new InvalidOperationException($"{Enum.GetName(Status)} appointments can't be cancelled.");
            }
            var utcSlot = new TimeConverter().ConvertToUtc(new DateTime(WorkSchedule.Date, TimeSlot));
            if (DateTime.UtcNow.AddHours(CancelLimitHours) > utcSlot)
            {
                throw new InvalidOperationException($"Appointments can only be cancelled at least {CancelLimitHours} hours before the scheduled time.");
            }
            // Cascade cancel QueueTicket if it exists and is still Waiting or Called.
            QueueTicket?.Cancel();
            Snapshots.Add(new AppointmentSnapshot(this));
            Status = AppointmentStatus.Cancelled;
            UpdatedByUserId = cancelledByUserId;
            MarkUpdated();
            WorkSchedule?.RecheckCapacity();
        }

        public void Confirm(Guid confirmedByUserId)
        {
            if (Status != AppointmentStatus.Pending)
            {
                throw new InvalidOperationException();
            }
            Snapshots.Add(new AppointmentSnapshot(this));
            Status = AppointmentStatus.Confirmed;
            UpdatedByUserId = confirmedByUserId;
            MarkUpdated();
        }

        public void Complete(Guid completedByUserId)
        {
            if (Status != AppointmentStatus.CheckedIn)
            {
                throw new InvalidOperationException();
            }
            Snapshots.Add(new AppointmentSnapshot(this));
            Status = AppointmentStatus.Completed;
            UpdatedByUserId = completedByUserId;
            MarkUpdated();
        }

        public void CheckIn(QueueTicket queueTicket)
        {
            var localCheckInDate = DateOnly.FromDateTime(new TimeConverter().ConvertToLocal(queueTicket.CheckInTime));
            if (localCheckInDate != WorkSchedule.Date)
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(WorkSchedule.Date, TimeSlot):yyyy-MM-dd}, not today ({localCheckInDate:yyyy-MM-dd}).");
            }
            QueueTicket = queueTicket;
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
        }
    }
}

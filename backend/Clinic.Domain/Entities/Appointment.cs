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

        public Guid UpdatedByUserId { get; protected set; }

        /// <summary>0..1 - chỉ có sau khi bệnh nhân check-in.</summary>
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
        }

        /// <summary>
        /// Check-in bệnh nhân tại quầy/vào hàng đợi.
        /// </summary>
        /// <param name="checkInTime">Thời điểm check-in thực tế (giờ hệ thống).</param>
        /// <exception cref="ArgumentException">Appointment chưa ở trạng thái Confirmed.</exception>
        /// <exception cref="ConflictException">
        /// TimeSlot của appointment không cùng ngày với checkInTime - ví dụ lịch hẹn của
        /// ngày mai không được phép vào hàng đợi của ngày hôm nay (và ngược lại).
        /// </exception>
        public QueueTicket CheckIn(DateTime checkInTime, int queueNumber)
        {
            if (Status != AppointmentStatus.Confirmed)
            {
                throw new ArgumentException($"Cannot check in an appointment with status '{Status}'. Appointment must be confirmed first.");
            }

            if (WorkSchedule.Date != DateOnly.FromDateTime(checkInTime))
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(WorkSchedule.Date, TimeSlot):yyyy-MM-dd}, not today ({checkInTime:yyyy-MM-dd}).");
            }

            var queueTicket = new QueueTicket
            {
                AppointmentId = Id,
                QueueNumber = queueNumber,
                CheckInTime = checkInTime,
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
            if (DateOnly.FromDateTime(queueTicket.CheckInTime) != WorkSchedule.Date)
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(WorkSchedule.Date, TimeSlot):yyyy-MM-dd}, not today ({queueTicket.CheckInTime:yyyy-MM-dd}).");
            }
            QueueTicket = queueTicket;
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
        }
    }
}

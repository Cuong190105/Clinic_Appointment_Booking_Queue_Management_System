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

        public Guid DoctorId { get; protected set; }
        public string DoctorName { get; protected set; } = string.Empty;
        public string? SpecialtyName { get; protected set; }
        public DateOnly ScheduleDate { get; protected set; }

        public string Reason { get; protected set; }

        public TimeOnly TimeSlot { get; protected set; }

        public AppointmentStatus Status { get; protected set; } = AppointmentStatus.Pending;

        public bool IsWalkIn { get; protected set; } = false;

        public Guid UpdatedByUserId { get; protected set; }

        /// <summary>0..1 - chỉ có sau khi bệnh nhân check-in.</summary>
        public QueueTicket? QueueTicket { get; protected set; }
        public MedicalReport? MedicalReport { get; set; }

        public ICollection<AppointmentSnapshot> Snapshots { get; protected set; } = [];

        private Appointment() { } // For EF Core

        public Appointment( // For seeding sample data
            Guid id,
            Guid patientId,
            Guid workScheduleId,
            Guid doctorId,
            string doctorName,
            string? specialtyName,
            DateOnly scheduleDate,
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
            DoctorId = doctorId;
            DoctorName = doctorName;
            SpecialtyName = specialtyName;
            ScheduleDate = scheduleDate;
            TimeSlot = timeSlot;
            Reason = reason;
            Status = status;
            IsWalkIn = isWalkIn;
            UpdatedByUserId = updatedByUserId;
        }

        public Appointment( // Legacy compatibility constructor for sample data
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
            : this(id, patientId, workScheduleId, Guid.Empty, string.Empty, null, default, timeSlot, reason, status, isWalkIn, updatedByUserId, createdAt, updatedAt, isDeleted)
        {
        }

        public Appointment(
            Patient patient,
            Guid workScheduleId,
            Guid doctorId,
            string doctorName,
            string? specialtyName,
            DateOnly scheduleDate,
            TimeOnly timeSlot,
            string reason,
            Guid createdByUserId,
            bool isWalkIn = false)
        {
            Patient = patient ?? throw new ArgumentNullException(nameof(patient));
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
            }
            var bookingTimeUtc = new TimeConverter().ConvertToUtc(new DateTime(scheduleDate, timeSlot));
            if (!isWalkIn && bookingTimeUtc < DateTime.UtcNow)
            {
                throw new ArgumentException("Cannot create an appointment for a past time.");
            }
            PatientId = patient.Id;
            WorkScheduleId = workScheduleId;
            DoctorId = doctorId;
            DoctorName = doctorName;
            SpecialtyName = specialtyName;
            ScheduleDate = scheduleDate;
            TimeSlot = timeSlot;
            Reason = reason;
            IsWalkIn = isWalkIn;
            UpdatedByUserId = createdByUserId;
        }

        public void SetDoctorDetails(Guid doctorId, string doctorName, string? specialtyName, DateOnly scheduleDate)
        {
            DoctorId = doctorId;
            DoctorName = doctorName;
            SpecialtyName = specialtyName;
            ScheduleDate = scheduleDate;
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
        public QueueTicket CheckIn(DateTime checkInTime, int queueNumber, bool isUrgent = false)
        {
            if (Status != AppointmentStatus.Confirmed)
            {
                throw new ArgumentException($"Cannot check in an appointment with status '{Status}'. Appointment must be confirmed first.");
            }

            var scheduleDate = ScheduleDate;
            var checkInDate = new TimeConverter().ToLocalDate(checkInTime);
            if (scheduleDate != checkInDate)
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(scheduleDate, TimeSlot):yyyy-MM-dd}, not today ({checkInDate:yyyy-MM-dd}).");
            }

            var queueTicket = new QueueTicket
            {
                AppointmentId = Id,
                QueueNumber = queueNumber,
                CheckInTime = checkInTime,
                Appointment = this
            };

            if (isUrgent)
            {
                queueTicket.SetPriority(true);
            }

            QueueTicket = queueTicket;
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
            return queueTicket;
        }

        public void Cancel(Guid cancelledByUserId)
        {
            if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            {
                throw new InvalidOperationException($"{Enum.GetName(Status)} appointments can't be cancelled.");
            }
            var scheduleDate = ScheduleDate;
            var utcSlot = new TimeConverter().ConvertToUtc(new DateTime(scheduleDate, TimeSlot));
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
            var scheduleDate = ScheduleDate;
            var checkInDate = new TimeConverter().ToLocalDate(queueTicket.CheckInTime);
            if (checkInDate != scheduleDate)
            {
                throw new ConflictException(
                    $"Cannot check in: appointment is scheduled for {new DateTime(scheduleDate, TimeSlot):yyyy-MM-dd}, not today ({checkInDate:yyyy-MM-dd}).");
            }
            QueueTicket = queueTicket;
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
        }

        public void MarkCheckedIn()
        {
            if (Status != AppointmentStatus.Confirmed && Status != AppointmentStatus.Pending)
            {
                throw new ArgumentException($"Cannot check in an appointment with status '{Status}'.");
            }
            Status = AppointmentStatus.CheckedIn;
            MarkUpdated();
        }
    }
}

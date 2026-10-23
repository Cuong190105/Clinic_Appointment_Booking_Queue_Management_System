using System;

namespace Clinic.Application.Contracts
{
    public class ReceptionistWalkInResultDto
    {
        public Guid AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string SpecialtyName { get; set; } = string.Empty;
        public Guid QueueTicketId { get; set; }
        public int QueueNumber { get; set; }
        public bool IsUrgent { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid WorkScheduleId { get; set; }
        public DateOnly ScheduleDate { get; set; }
        public TimeOnly TimeSlot { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}


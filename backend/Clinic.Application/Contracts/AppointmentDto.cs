using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class AppointmentDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid DoctorId { get; set; }
        public string PatientName { get; set; }
        public string DoctorName { get; set; }
        public TimeOnly TimeSlot { get; set; }
        public DateOnly Date { get; set; }
        public string Reason { get; set; }
        public bool IsWalkIn { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

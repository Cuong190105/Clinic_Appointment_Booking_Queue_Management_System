using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class AppointmentDetailDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid DoctorId { get; set; }
        public string PatientName { get; set; }
        public string DoctorName { get; set; }
        public TimeOnly TimeSlot { get; set; }
        public DateOnly Date { get; set; }
        public string Reason { get; set; }
        public AppointmentStatus Status { get; set; }
        public string? QueueNumber { get; set; } = string.Empty;
        public TimeOnly? QueueTime { get; set; }
        public MedicalReportDto? MedicalReport { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

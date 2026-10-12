using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class AppointmentHistoryDto
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; }
        public TimeOnly TimeSlot { get; set; }
        public DateOnly Date { get; set; }
        public string Reason { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid UpdatedByUserId { get; set; }
        public string UpdatorName { get; set; }
    }
}

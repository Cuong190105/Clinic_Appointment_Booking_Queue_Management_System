using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class RequestedShiftDto
    {
        public Guid Id { get; set; }
        public Guid DoctorId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int PatientLimit { get; set; }
        public string? Reason { get; set; }
        public ShiftRequestStatus Status { get; set; }
    }
}

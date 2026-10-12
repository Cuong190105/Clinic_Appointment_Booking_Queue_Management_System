using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class WorkSchedulesBookingDto
    {
        public Guid Id { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public ICollection<TimeOnly> TimeSlot { get; set; } = [];
    }
}

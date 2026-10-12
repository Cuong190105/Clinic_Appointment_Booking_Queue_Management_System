using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class DoctorScheduleDto<T>
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; }
        public List<T> Schedules { get; set; }
        public DateOnly startDate {  get; set; }
        public DateOnly endDate { get; set; }
    }
}

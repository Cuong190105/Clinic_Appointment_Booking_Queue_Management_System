using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class DoctorScheduleWithSpecialtyDto<T> : DoctorScheduleDto<T>
    {
        public Guid SpecialtyId { get; set; }
        public string SpecialtyName { get; set; }
    }
}

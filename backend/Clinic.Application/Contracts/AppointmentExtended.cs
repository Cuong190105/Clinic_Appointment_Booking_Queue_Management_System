using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class AppointmentExtended : AppointmentDto
    {
        public string SpecialtyName { get; set; }
        public Guid SpecialtyId { get; set; }
    }
}

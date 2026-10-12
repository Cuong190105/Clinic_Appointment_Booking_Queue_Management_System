using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class AppointmentChangelogDto
    {
        public Guid AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; }
        public IEnumerable<AppointmentHistoryDto> Changelog { get; set; }
    }
}

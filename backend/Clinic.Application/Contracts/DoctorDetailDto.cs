using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class DoctorDetailDto : DoctorSummaryDto
    {
        public DateOnly DateOfBirth { get; set; }
        public string Address { get; set; }
        public DateOnly HireDate { get; set; }
        public string Biography { get; set; }
    }
}

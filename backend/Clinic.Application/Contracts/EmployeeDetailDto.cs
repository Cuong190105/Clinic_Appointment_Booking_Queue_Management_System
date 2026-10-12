using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class EmployeeDetailDto : EmployeeSummaryDto
    {
        public string Address { get; set; }
        public string? ManagerName { get; set; }
    }
}

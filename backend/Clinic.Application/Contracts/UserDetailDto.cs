using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class UserDetailDto : UserSummaryDto
    {
        public Guid PersonId { get; set; }
        public string Address { get; set; }
        public DateOnly DateOfBirth { get; set; }
    }
}

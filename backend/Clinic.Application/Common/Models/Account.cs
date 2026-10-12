using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Common.Models
{
    public class Account
    {
        public string FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }
}

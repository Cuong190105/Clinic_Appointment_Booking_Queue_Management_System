using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class StatisticsDataDto
    {
        public string Period { get; set; } = string.Empty;
        public List<AppointmentByDayDto> AppointmentsByDay { get; set; } = new List<AppointmentByDayDto>();
        public List<AppointmentByStatusDto> AppointmentsByStatus { get; set; } = new List<AppointmentByStatusDto>();
    }

    public class AppointmentByDayDto
    {
        public DateOnly Date { get; set; }
        public int Count { get; set; }
    }

    public class AppointmentByStatusDto
    {
        public AppointmentStatus Status { get; set; }
        public int Count { get; set; }
    }
}

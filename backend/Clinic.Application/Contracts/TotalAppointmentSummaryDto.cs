using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Contracts
{
    public class TotalAppointmentSummaryDto
    {
        public int AppointmentCount { get; set; }
        public int AppointmentOnlineCount { get; set; }
        public int CompletedAppointments { get; set; }
        public int CanceledAppointments { get; set; }
        public int NoShowAppointments { get; set; }
        public double AverageWaitingMinutes { get; set; }
        public double CancellationRate { get; set; }
    }
}

using Clinic.Application.Interfaces;

namespace Clinic.API.Configuration
{
    /// <summary>
    /// Bind t? section "Appointment" trong appsettings.json. Ðang ký qua AddOptions trong Program.cs.
    /// </summary>
    public class AppointmentPolicySettings : IAppointmentPolicySettings
    {
        public const string SectionName = "Appointment";

        public int RescheduleMinNoticeHours { get; set; } = 24;
    }
}
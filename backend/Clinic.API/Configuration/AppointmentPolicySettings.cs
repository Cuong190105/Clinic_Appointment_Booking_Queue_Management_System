using Clinic.Application.Interfaces;

namespace Clinic.API.Configuration
{
    /// <summary>
    /// Bind từ section "Appointment" trong appsettings.json. Đăng ký qua AddOptions trong Program.cs.
    /// </summary>
    public class AppointmentPolicySettings : IAppointmentPolicySettings
    {
        public const string SectionName = "Appointment";

        public int RescheduleMinNoticeHours { get; set; } = 24;
    }
}
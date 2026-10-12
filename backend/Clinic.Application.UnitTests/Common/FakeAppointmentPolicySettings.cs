using Clinic.Application.Interfaces;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeAppointmentPolicySettings : IAppointmentPolicySettings
    {
        public int RescheduleMinNoticeHours { get; set; } = 2;
    }
}

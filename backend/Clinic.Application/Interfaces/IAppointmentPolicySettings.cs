namespace Clinic.Application.Interfaces
{
    /// <summary>
    /// Các tham s? c?u hình cho business rule c?a Appointment.
    /// Interface d?t ? Application d? handler không ph? thu?c tr?c ti?p vào Microsoft.Extensions.Configuration.
    /// </summary>
    public interface IAppointmentPolicySettings
    {
        /// <summary>
        /// S? gi? t?i thi?u Patient ph?i d?i l?ch tru?c gi? h?n hi?n t?i (rule "tru?c h?n").
        /// Không áp d?ng cho Admin/Receptionist.
        /// </summary>
        int RescheduleMinNoticeHours { get; }
    }
}
namespace Clinic.Application.Interfaces
{
    /// <summary>
    /// Các tham số cấu hình cho business rule của Appointment.
    /// Interface đặt ở Application để handler không phụ thuộc trực tiếp vào Microsoft.Extensions.Configuration.
    /// </summary>
    public interface IAppointmentPolicySettings
    {
        /// <summary>
        /// Số giờ tối thiểu Patient phải đổi lịch trước giờ hẹn hiện tại (rule "trước hạn").
        /// Không áp dụng cho Admin/Receptionist.
        /// </summary>
        int RescheduleMinNoticeHours { get; }
    }
}
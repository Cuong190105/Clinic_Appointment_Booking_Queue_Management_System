using Clinic.Domain.Entities;

namespace Clinic.Application.Notifications.Templates
{
    public class AppointmentCancelledTemplate
    {
        public EmailContent RenderEmail(Appointment appointment)
        {
            var doctorName = !string.IsNullOrWhiteSpace(appointment.DoctorName)
                ? appointment.DoctorName
                : "Bác sĩ";
            var scheduleDate = appointment.ScheduleDate != default ? appointment.ScheduleDate.ToString() : string.Empty;
            var patientName = appointment.Patient?.Person?.FullName ?? "Quý khách";
            var cancellationReason = "Lý do thay đổi lịch làm việc";

            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {patientName},</p>

                    <p>Lịch khám dưới đây của bạn đã bị hủy với lí do: {cancellationReason}.</p>

                    <p>
                        <strong>Bác sĩ:</strong> {doctorName}<br>
                        <strong>Ngày:</strong> {scheduleDate}<br>
                        <strong>Thời gian:</strong> {appointment.TimeSlot}
                    </p>

                    <p>Quý khách có thể tạo lịch hẹn mới bất cứ lúc nào.</p>

                    <p>Trân trọng,<br>Phòng khám của chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Thông báo hủy lịch khám", body);
        }

        public InAppContent RenderInApp(Appointment appointment)
        {
            var doctorName = !string.IsNullOrWhiteSpace(appointment.DoctorName)
                ? appointment.DoctorName
                : "Bác sĩ";
            var scheduleDate = appointment.ScheduleDate != default ? appointment.ScheduleDate.ToString() : string.Empty;
            var patientName = appointment.Patient?.Person?.FullName ?? "Quý khách";
            var cancellationReason = "Lý do thay đổi lịch làm việc";

            var message = $"Lịch khám của khách hàng {patientName} với bác sĩ {doctorName} vào ngày {scheduleDate} lúc {appointment.TimeSlot} đã bị hủy với lí do: {cancellationReason}.";
            return new InAppContent("Hủy lịch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var doctorName = !string.IsNullOrWhiteSpace(data.DoctorName)
                ? data.DoctorName
                : "Bác sĩ";
            var scheduleDate = data.ScheduleDate != default ? data.ScheduleDate.ToString() : string.Empty;
            var cancellationReason = "Lý do thay đổi lịch làm việc";

            var message = $"Lịch khám của bạn với bác sĩ {doctorName} vào ngày {scheduleDate} lúc {data.TimeSlot} đã bị hủy với lí do: {cancellationReason}.";
            return new SmsContent(message);
        }
    }
}

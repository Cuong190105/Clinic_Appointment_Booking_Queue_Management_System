using Clinic.Domain.Entities;

namespace Clinic.Application.Notifications.Templates
{
    public class AppointmentCancelledTemplate
    {
        public EmailContent RenderEmail(Appointment appointment)
        {
            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {appointment.Patient.Person.FullName},</p>

                    <p>Lịch khám dưới đây của bạn đã bị hủy với lí do: {appointment.WorkSchedule.CancellationReason}.</p>

                    <p>
                        <strong>Bác sĩ:</strong> {appointment.WorkSchedule.Doctor.Employee.Person.FullName}<br>
                        <strong>Ngày:</strong> {appointment.WorkSchedule.Date}<br>
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
            var message = $"Lịch khám của khách hàng {appointment.Patient.Person.FullName} với bác sĩ {appointment.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {appointment.WorkSchedule.Date} lúc {appointment.TimeSlot} đã bị hủy với lí do: {appointment.WorkSchedule.CancellationReason}.";
            return new InAppContent("Hủy lịch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var message = $"Lịch khám của bạn với bác sĩ {data.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {data.WorkSchedule.Date} lúc {data.TimeSlot} đã bị hủy với lí do: {data.WorkSchedule.CancellationReason}.";
            return new SmsContent(message);
        }
    }
}

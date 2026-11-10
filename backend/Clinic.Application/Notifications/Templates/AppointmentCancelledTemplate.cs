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
                    <h2 style="color: #333;">Xác nh?n l?ch khám</h2>

                    <p>Xin chào {appointment.Patient.Person.FullName},</p>

                    <p>L?ch khám du?i dây c?a b?n dã b? h?y v?i lí do: {appointment.WorkSchedule.CancellationReason}.</p>

                    <p>
                        <strong>Bác si:</strong> {appointment.WorkSchedule.Doctor.Employee.Person.FullName}<br>
                        <strong>Ngày:</strong> {appointment.WorkSchedule.Date}<br>
                        <strong>Th?i gian:</strong> {appointment.TimeSlot}
                    </p>

                    <p>Quý khách có th? t?o l?ch h?n m?i b?t c? lúc nào.</p>

                    <p>Trân tr?ng,<br>Phòng khám c?a chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Thông báo h?y l?ch khám", body);
        }

        public InAppContent RenderInApp(Appointment appointment)
        {
            var message = $"L?ch khám c?a khách hàng {appointment.Patient.Person.FullName} v?i bác si {appointment.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {appointment.WorkSchedule.Date} lúc {appointment.TimeSlot} dã b? h?y v?i lí do: {appointment.WorkSchedule.CancellationReason}.";
            return new InAppContent("H?y l?ch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var message = $"L?ch khám c?a b?n v?i bác si {data.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {data.WorkSchedule.Date} lúc {data.TimeSlot} dã b? h?y v?i lí do: {data.WorkSchedule.CancellationReason}.";
            return new SmsContent(message);
        }
    }
}

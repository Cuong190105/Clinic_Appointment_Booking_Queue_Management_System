using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Templates
{
    public class AppointmentReminderTemplate
    {
        public EmailContent RenderEmail(Appointment appointment)
        {
            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nh?n l?ch khám</h2>

                    <p>Xin chào {appointment.Patient.Person.FullName},</p>

                    <p>Quý khách có l?ch khám s?p t?i.</p>

                    <p>
                        <strong>Bác si:</strong> {appointment.WorkSchedule.Doctor.Employee.Person.FullName}<br>
                        <strong>Ngày:</strong> {appointment.WorkSchedule.Date}<br>
                        <strong>Th?i gian:</strong> {appointment.TimeSlot}
                    </p>

                    <p>Vui lòng d?n dúng gi? d? d?m b?o l?ch trình khám b?nh c?a b?n.</p>

                    <p>N?u quý khách mu?n thay d?i ho?c h?y l?ch h?n, vui lòng th?c hi?n tru?c 24 gi? so v?i th?i gian dã h?n.</p>

                    <p>Trân tr?ng,<br>Phòng khám c?a chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Xác nh?n l?ch khám", body);
        }

        public InAppContent RenderInApp(Appointment appointment)
        {
            var message = $"L?ch khám c?a khách hàng {appointment.Patient.Person.FullName} v?i bác si {appointment.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {appointment.WorkSchedule.Date} lúc {appointment.TimeSlot} dã du?c xác nh?n thành công.";
            return new InAppContent("Xác nh?n l?ch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var message = $"Quý khách {data.Patient.Person.FullName} có l?ch khám v?i bác si {data.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {data.WorkSchedule.Date} lúc {data.TimeSlot}. Vui lòng d?n phòng khám dúng gi? d? d?m b?o quy?n l?i.";
            return new SmsContent(message);
        }
    }
}

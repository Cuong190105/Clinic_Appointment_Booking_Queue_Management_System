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
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {appointment.Patient.Person.FullName},</p>

                    <p>Quý khách có lịch khám sắp tới.</p>

                    <p>
                        <strong>Bác sĩ:</strong> {appointment.WorkSchedule.Doctor.Employee.Person.FullName}<br>
                        <strong>Ngày:</strong> {appointment.WorkSchedule.Date}<br>
                        <strong>Thời gian:</strong> {appointment.TimeSlot}
                    </p>

                    <p>Vui lòng đến đúng giờ để đảm bảo lịch trình khám bệnh của bạn.</p>

                    <p>Nếu quý khách muốn thay đổi hoặc hủy lịch hẹn, vui lòng thực hiện trước 24 giờ so với thời gian đã hẹn.</p>

                    <p>Trân trọng,<br>Phòng khám của chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Xác nhận lịch khám", body);
        }

        public InAppContent RenderInApp(Appointment appointment)
        {
            var message = $"Lịch khám của khách hàng {appointment.Patient.Person.FullName} với bác sĩ {appointment.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {appointment.WorkSchedule.Date} lúc {appointment.TimeSlot} đã được xác nhận thành công.";
            return new InAppContent("Xác nhận lịch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var message = $"Quý khách {data.Patient.Person.FullName} có lịch khám với bác sĩ {data.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {data.WorkSchedule.Date} lúc {data.TimeSlot}. Vui lòng đến phòng khám đúng giờ để đảm bảo quyền lợi.";
            return new SmsContent(message);
        }
    }
}

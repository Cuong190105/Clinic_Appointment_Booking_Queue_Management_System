using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Templates
{
    public class AppointmentUpdatedTemplate
    {
        public EmailContent RenderEmail(Appointment appointment)
        {
            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {appointment.Patient.Person.FullName},</p>

                    <p>Lịch khám của bạn đã được cập nhật.</p>

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
            return new EmailContent("Cập nhật lịch khám", body);
        }

        public InAppContent RenderInApp(Appointment appointment)
        {
            var message = $"Lịch khám của khách hàng {appointment.Patient.Person.FullName} với bác sĩ {appointment.WorkSchedule.Doctor.Employee.Person.FullName} vào ngày {appointment.WorkSchedule.Date} lúc {appointment.TimeSlot} đã được cập nhật.";
            return new InAppContent("Cập nhật lịch khám", message);
        }
    }
}

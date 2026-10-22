using Clinic.Domain.Entities;

namespace Clinic.Application.Notifications.Templates
{
    public class AppointmentReminderTemplate
    {
        public EmailContent RenderEmail(Appointment appointment)
        {
            var doctorName = !string.IsNullOrWhiteSpace(appointment.DoctorName)
                ? appointment.DoctorName
                : appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ";
            var scheduleDate = appointment.ScheduleDate != default
                ? appointment.ScheduleDate.ToString()
                : (appointment.WorkSchedule != null ? appointment.WorkSchedule.Date.ToString() : string.Empty);
            var patientName = appointment.Patient?.Person?.FullName ?? "Quý khách";

            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {patientName},</p>

                    <p>Quý khách có lịch khám sắp tới.</p>

                    <p>
                        <strong>Bác sĩ:</strong> {doctorName}<br>
                        <strong>Ngày:</strong> {scheduleDate}<br>
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
            var doctorName = !string.IsNullOrWhiteSpace(appointment.DoctorName)
                ? appointment.DoctorName
                : appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ";
            var scheduleDate = appointment.ScheduleDate != default
                ? appointment.ScheduleDate.ToString()
                : (appointment.WorkSchedule != null ? appointment.WorkSchedule.Date.ToString() : string.Empty);
            var patientName = appointment.Patient?.Person?.FullName ?? "Quý khách";

            var message = $"Lịch khám của khách hàng {patientName} với bác sĩ {doctorName} vào ngày {scheduleDate} lúc {appointment.TimeSlot} đã được xác nhận thành công.";
            return new InAppContent("Xác nhận lịch khám", message);
        }

        public SmsContent RenderSms(Appointment data)
        {
            var doctorName = !string.IsNullOrWhiteSpace(data.DoctorName)
                ? data.DoctorName
                : data.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ";
            var scheduleDate = data.ScheduleDate != default
                ? data.ScheduleDate.ToString()
                : (data.WorkSchedule != null ? data.WorkSchedule.Date.ToString() : string.Empty);
            var patientName = data.Patient?.Person?.FullName ?? "Quý khách";

            var message = $"Quý khách {patientName} có lịch khám với bác sĩ {doctorName} vào ngày {scheduleDate} lúc {data.TimeSlot}. Vui lòng đến phòng khám đúng giờ để đảm bảo quyền lợi.";
            return new SmsContent(message);
        }
    }
}

using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Domain.UnitTests.Common
{
    public class TestDataFactory
    {
        public static Person CreatePerson(string fullName = "John Doe", string? phoneNumber = "1234567890", string? email = "", string? address = "")
        {
            return new Person(fullName, phoneNumber, email, DateOnly.FromDateTime(DateTime.UtcNow), Gender.Male, address);
        }

        /// <summary>
        /// Tạo 1 Appointment hợp lệ (kèm WorkSchedule) để dùng cho test Confirm/CheckIn/Cancel.
        /// Status mặc định là Pending; truyền confirmed = true / checkedIn = true để có sẵn
        /// Appointment ở trạng thái tương ứng.
        /// </summary>
        public static Appointment CreateAppointment(Guid? patientId = null, Guid? doctorId = null, bool confirmed = false, bool checkedIn = false, double? day = 0, TimeOnly? startTime = null, TimeOnly? endTime = null)
        {
            // WorkSchedule giờ bắt buộc gắn với 1 Doctor object (không chỉ DoctorId), nên
            // dựng 1 Doctor "giả" qua constructor reconstruct để giữ đúng Id đã truyền vào,
            // tránh phải dựng cả Employee/Specialty chỉ để phục vụ test Appointment.
            var doctor = new Doctor(
                id: doctorId ?? Guid.NewGuid(),
                employeeId: Guid.NewGuid(),
                licenseNumber: "TEST-LICENSE",
                qualification: "MD",
                experienceYears: 0,
                biography: null,
                status: DoctorStatus.Active,
                createdAt: DateTime.UtcNow,
                updatedAt: DateTime.UtcNow,
                isDeleted: false
            );

            var localNow = new Clinic.Domain.Common.TimeConverter().ConvertFromUtc(DateTime.UtcNow);
            var scheduleDate = DateOnly.FromDateTime(localNow.AddDays(day.Value));
            var effectiveStart = startTime ?? TimeOnly.FromDateTime(localNow.AddMinutes(30));
            var effectiveEnd = endTime ?? effectiveStart.AddHours(2);
            if (effectiveEnd <= effectiveStart)
            {
                scheduleDate = scheduleDate.AddDays(1);
                effectiveStart = new TimeOnly(8, 0);
                effectiveEnd = new TimeOnly(10, 0);
            }
            var workSchedule = new WorkSchedule(
                doctor,
                scheduleDate,
                effectiveStart,
                effectiveEnd,
                5
            );

            var patientPerson = CreatePerson(fullName: "Test Patient");

            var patient = new Patient(
                patientPerson, "", ""
            );

            var appointment = new Appointment(patient, workSchedule, workSchedule.ShiftStart, "Reason", Guid.NewGuid());

            if (confirmed || checkedIn)
            {
                appointment.Confirm(Guid.NewGuid());
            }

            if (checkedIn)
            {
                appointment.CheckIn(DateTime.UtcNow, 1);
            }

            return appointment;
        }

        /// <summary>
        /// Tạo 1 QueueTicket hợp lệ, liên kết 2 chiều với appointment (giống CheckInHandler thật).
        /// </summary>
        public static QueueTicket CreateQueueTicket(Appointment appointment, int queueNumber = 1, bool priority = false)
        {
            var queueTicket = new QueueTicket
            {
                AppointmentId = appointment.Id,
                Appointment = appointment,
                QueueNumber = queueNumber,
                CheckInTime = DateTime.UtcNow
            };
            appointment.CheckIn(queueTicket);

            if (priority)
            {
                queueTicket.SetPriority(true);
            }

            return queueTicket;
        }
    }
}
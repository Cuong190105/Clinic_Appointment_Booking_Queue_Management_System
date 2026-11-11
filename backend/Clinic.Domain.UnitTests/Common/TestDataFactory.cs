using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

namespace Clinic.Domain.UnitTests.Common
{
    public class TestDataFactory
    {
        public static List<Role> RoleSet => new List<Role>
        {
            new Role(Guid.NewGuid(), "Admin", "Administrator role", DateTime.UtcNow, DateTime.UtcNow, false),
            new Role(Guid.NewGuid(), "Doctor", "Doctor role", DateTime.UtcNow, DateTime.UtcNow, false),
            new Role(Guid.NewGuid(), "Receptionist", "Receptionist role", DateTime.UtcNow, DateTime.UtcNow, false),
            new Role(Guid.NewGuid(), "Patient", "Patient role", DateTime.UtcNow, DateTime.UtcNow, false),
        };

        public static Person CreatePerson(string fullName = "John Doe", string? phoneNumber = "1234567890", string? email = "", string? address = "")
        {
            return new Person(fullName, phoneNumber, email, DateOnly.FromDateTime(DateTime.UtcNow), Gender.Male, address);
        }

        public static User CreateUser(string username = "testuser", string passwordHash = "hashedpassword", Person person = null)
        {
            var newUser = new User(username, passwordHash, person ?? CreatePerson());
            return newUser;
        }

        /// <summary>
        /// T?o 1 Appointment h?p l? (kèm WorkSchedule) d? dùng cho test Confirm/CheckIn/Cancel.
        /// Status m?c d?nh là Pending; truy?n confirmed = true / checkedIn = true d? có s?n
        /// Appointment ? tr?ng thái tuong ?ng.
        /// </summary>
        public static Appointment CreateAppointment(Guid? patientId = null, Guid? doctorId = null, bool confirmed = false, bool checkedIn = false, double? day = 0, TimeOnly? startTime = null, TimeOnly? endTime = null)
        {
            // WorkSchedule gi? b?t bu?c g?n v?i 1 Doctor object (không ch? DoctorId), nên
            // d?ng 1 Doctor "gi?" qua constructor reconstruct d? gi? dúng Id dã truy?n vào,
            // tránh ph?i d?ng c? Employee/Specialty ch? d? ph?c v? test Appointment.
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

            var targetDate = new TimeConverter().Today.AddDays((int)(day ?? 0));
            var start = startTime ?? new TimeOnly(8, 0);
            var end = endTime ?? new TimeOnly(12, 0);

            var workSchedule = WorkSchedule.CreateForTest(
                doctor,
                targetDate,
                start,
                end,
                5
            );

            var patientPerson = CreatePerson(fullName: "Test Patient");

            var patient = new Patient(
                patientPerson, "", ""
            );

            var appointment = Appointment.CreateForTest(patient, workSchedule, start, "Reason", Guid.NewGuid());

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
        /// T?o 1 QueueTicket h?p l?, liên k?t 2 chi?u v?i appointment (gi?ng CheckInHandler th?t).
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
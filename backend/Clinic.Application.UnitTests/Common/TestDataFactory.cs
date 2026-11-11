using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
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

        public static Person CreatePerson(string fullName = "John Doe", string? phoneNumber = "1234567890", string? email = "", string? address = "", DateOnly? dateOfBirth = null, string? userRole = null)
        {
            var person = new Person(fullName, phoneNumber, email, dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow), Gender.Male, address);
            if (!string.IsNullOrEmpty(userRole))
            {
                var user = CreateUser(userRole, $"{fullName.Replace(" ", "").ToLower()}user", "hashedpassword", person);
                person.User = user;
            }
            return person;
        }

        public static User CreateUser(string role = "Admin", string username = "testuser", string passwordHash = "hashedpassword", Person? person = null)
        {
            person ??= CreatePerson();
            var newUser = new User(username, passwordHash, person);
            person.User = newUser;
            newUser.AssignRole(RoleSet.FirstOrDefault(r => r.Name == role) ?? RoleSet.First());
            return newUser;
        }

        /// <summary>
        /// Táº¡o 1 QueueTicket há»£p lá»‡, liÃªn káº¿t 2 chiá»u vá»›i appointment (giá»‘ng CheckInHandler tháº­t).
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

        public static RefreshToken CreateRefreshToken(string hashedToken, User user)
        {
            var refreshToken = new RefreshToken(hashedToken, DateTime.UtcNow.AddDays(7), user);
            return refreshToken;
        }

        public static Specialty CreateSpecialty(string? name = "Cardiology", string? description = "Heart specialist")
        {
            var specialty = new Specialty(name, description, DateOnly.FromDateTime(DateTime.UtcNow));
            return specialty;
        }

        public static Employee CreateEmployee(Person? person = null, string role = "Admin")
        {
            person ??= CreatePerson(userRole: role);
            var newEmployee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            person.Employee = newEmployee;
            return newEmployee;
        }

        internal static Doctor CreateDoctor(Employee? employee = null, Specialty? specialty = null, string? licenseNumber = "ABC123", string? qualification = "MD", string? bio = "", int yoe = 0)
        {
            var doctor = new Doctor(employee ?? CreateEmployee(role: "Doctor"), licenseNumber, qualification, specialty ?? CreateSpecialty(), yoe, bio);
            doctor.Employee?.AssignDoctor(doctor);
            return doctor;
        }

        internal static Patient CreatePatient(Person? person = null)
        {
            person ??= CreatePerson(userRole: "Patient");
            var patient = new Patient(person, "ABC123", "0111111111");
            person.Patient = patient;
            return patient;
        }

        internal static WorkSchedule CreateWorkSchedule(Doctor? doctor = null, DateOnly? date = null, TimeOnly? startTime = null, TimeOnly? endTime = null, int patientLimit = 15)
        {
            var targetDoctor = doctor ?? CreateDoctor();
            var targetDate = date ?? new TimeConverter().Today.AddDays(2);
            var start = startTime ?? new TimeOnly(9, 0);
            var end = endTime ?? (start < new TimeOnly(22, 0) ? start.AddHours(2) : new TimeOnly(23, 59, 59));
            return WorkSchedule.CreateForTest(targetDoctor, targetDate, start, end, patientLimit);
        }

        internal static Appointment CreateAppointment(Patient? patient = null, WorkSchedule? workSchedule = null, TimeOnly? timeSlot = null, string? reason = null, Guid? createdBy = null, bool confirmed = false, bool checkedIn = false, int queueNumber = 0, bool today = false)
        {
            var isToday = checkedIn || today;
            workSchedule ??= CreateWorkSchedule(date: isToday ? new TimeConverter().Today : null);
            var slot = timeSlot ?? workSchedule.ShiftStart;
            var targetPatient = patient ?? CreatePatient();
            var app = Appointment.CreateForTest(targetPatient, workSchedule, slot, reason ?? "Reason", createdBy ?? Guid.NewGuid());
            if (confirmed || checkedIn)
            {
                app.Confirm(new Guid());
            }

            if (checkedIn)
            {
                // LÆ°u Ã½: náº¿u truyá»n timeSlot khÃ¡c ngÃ y hÃ´m nay kÃ¨m checkedIn: true, dÃ²ng nÃ y sáº½ tá»±
                // nÃ©m ConflictException ngay trong lÃºc dá»±ng dá»¯ liá»‡u test (Ä‘Ãºng theo domain rule má»›i) -
                // muá»‘n test case "check-in lá»‹ch ngÃ y khÃ¡c" thÃ¬ dÃ¹ng confirmed: true + timeSlot khÃ¡c
                // ngÃ y, rá»“i tá»± gá»i handler.Handle(...) Ä‘á»ƒ assert exception, khÃ´ng dÃ¹ng checkedIn: true.
                app.CheckIn(DateTime.UtcNow, queueNumber);
            }

            return app;
        }

        public static MedicalReport CreateMedicalReport(QueueTicket? queueTicket = null, MedicalReportStatus status = MedicalReportStatus.Draft, string? symptoms = "Headache", string? diagnosis = "Mild migraine", string? prescription = "Paracetamol 500mg")
        {
            var appointment = CreateAppointment(checkedIn: true);
            queueTicket ??= CreateQueueTicket(appointment);
            var report = new MedicalReport
            {
                QueueTicketId = queueTicket.Id,
                QueueTicket = queueTicket,
                Symptoms = symptoms,
                Diagnosis = diagnosis,
                Prescription = prescription,
                Status = status,
                ExamStartTime = DateTime.UtcNow.AddMinutes(-30),
                ExamEndTime = status == MedicalReportStatus.Finalized ? DateTime.UtcNow : null
            };
            return report;
        }
    }
}
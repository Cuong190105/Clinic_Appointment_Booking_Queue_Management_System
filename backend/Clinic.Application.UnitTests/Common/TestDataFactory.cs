using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
{
    public class TestDataFactory
    {
        public static Person CreatePerson(string fullName = "John Doe", string? phoneNumber = "1234567890", string? email = "", string? address = "", DateOnly? dateOfBirth = null, string? userRole = null)
        {
            var person = new Person(fullName, phoneNumber, email, dateOfBirth ?? DateOnly.FromDateTime(DateTime.UtcNow), Gender.Male, address);
            if (!string.IsNullOrEmpty(userRole))
                CreateUser(userRole, $"{fullName.Replace(" ", "").ToLower()}user", "hashedpassword", person);
            return person;
        }

        public static TestIdentityAccount CreateUser(string role = "Admin", string username = "testuser", string passwordHash = "hashedpassword", Person? person = null)
        {
            person ??= CreatePerson();
            var id = person.IdentityUserId ?? Guid.NewGuid();
            person.LinkIdentityUser(id);
            return new TestIdentityAccount(id, person, role);
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

        internal static WorkSchedule CreateWorkSchedule(Doctor? doctor = null, DateOnly? date = null, TimeOnly? startTime = null, TimeOnly? endTime = null, int RecommendedCapacity = 15)
        {
            return new WorkSchedule(doctor ?? CreateDoctor(), date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), startTime ?? (date != null ? TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddMinutes(1)) : new TimeOnly(9, 0)), endTime ?? (date != null ? TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(9)) : new TimeOnly(11, 0)), RecommendedCapacity);
        }

        internal static Appointment CreateAppointment(Patient? patient = null, WorkSchedule? workSchedule = null, TimeOnly? timeSlot = null, string? reason = null, Guid? createdBy = null, bool confirmed = false, bool checkedIn = false, int queueNumber = 0, bool today = false)
        {
            workSchedule ??= CreateWorkSchedule(date: checkedIn | today ? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddMinutes(1)) : null, startTime: timeSlot, endTime: timeSlot?.AddHours(8));
            timeSlot ??= workSchedule.ShiftStart;
            var app = new Appointment(patient ?? CreatePatient(), workSchedule, timeSlot.Value, reason ?? "Reason", createdBy ?? Guid.NewGuid());
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
                AppointmentId = queueTicket.AppointmentId,
                Appointment = queueTicket.Appointment,
                PatientId = queueTicket.Appointment.PatientId,
                Patient = queueTicket.Appointment.Patient,
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

    public sealed class TestIdentityAccount(Guid id, Person person, string role)
    {
        public Guid Id { get; } = id;
        public Person Person { get; } = person;
        public string Role { get; private set; } = role;
        public void AssignRole(string roleName) => Role = roleName;
    }
}


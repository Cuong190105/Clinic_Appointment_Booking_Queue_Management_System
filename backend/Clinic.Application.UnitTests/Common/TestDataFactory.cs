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

        public static TestEmployee CreateEmployee(Person? person = null, string role = "Admin")
        {
            person ??= CreatePerson(userRole: role);
            return new TestEmployee(Guid.NewGuid(), person);
        }

        internal static TestDoctor CreateDoctor()
        {
            var employee = CreateEmployee(role: "Doctor");
            return new TestDoctor(Guid.NewGuid(), employee, "Doctor Name", "Cardiology");
        }

        internal static Patient CreatePatient(Person? person = null)
        {
            person ??= CreatePerson(userRole: "Patient");
            var patient = new Patient(person, "ABC123", "0111111111");
            person.Patient = patient;
            return patient;
        }

        internal static TestWorkSchedule CreateWorkSchedule(TestDoctor? doctor = null, DateOnly? date = null, TimeOnly? startTime = null, TimeOnly? endTime = null, int RecommendedCapacity = 15)
        {
            var doc = doctor ?? CreateDoctor();
            var sTime = startTime ?? new TimeOnly(8, 0);
            var eTime = endTime ?? new TimeOnly(23, 59, 59);
            var d = date ?? new Clinic.Domain.Common.TimeConverter().Today.AddDays(2);
            return new TestWorkSchedule(Guid.NewGuid(), doc.Id, doc.FullName, doc.SpecialtyName, d, sTime, eTime, RecommendedCapacity);
        }

        internal static Appointment CreateAppointment(Patient? patient = null, TestWorkSchedule? workSchedule = null, TimeOnly? timeSlot = null, string? reason = null, Guid? createdBy = null, bool confirmed = false, bool checkedIn = false, int queueNumber = 0, bool today = false, bool isWalkIn = false)
        {
            var scheduleEndTime = timeSlot.HasValue ? (timeSlot.Value.AddHours(2) <= timeSlot.Value ? new TimeOnly(23, 59, 59) : timeSlot.Value.AddHours(2)) : (TimeOnly?)null;
            workSchedule ??= CreateWorkSchedule(date: (checkedIn || today) ? new Clinic.Domain.Common.TimeConverter().Today : null, startTime: timeSlot, endTime: scheduleEndTime);
            timeSlot ??= workSchedule.ShiftStart;
            var isPast = new Clinic.Domain.Common.TimeConverter().ConvertToUtc(new DateTime(workSchedule.Date, timeSlot.Value)) < DateTime.UtcNow;
            var walkInFlag = isWalkIn || (isPast && (checkedIn || today || workSchedule.Date <= new Clinic.Domain.Common.TimeConverter().Today));

            var app = new Appointment(patient ?? CreatePatient(), workSchedule.Id, workSchedule.DoctorId, workSchedule.DoctorName, workSchedule.SpecialtyName, workSchedule.Date, timeSlot.Value, reason ?? "Reason", createdBy ?? Guid.NewGuid(), walkInFlag);
            if (!isWalkIn && walkInFlag)
            {
                typeof(Appointment).GetProperty("IsWalkIn")!.SetValue(app, false);
            }
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

    public sealed class TestEmployee(Guid id, Person person)
    {
        public Guid Id { get; } = id;
        public Person Person { get; } = person;
    }

    public sealed class TestDoctor(Guid id, TestEmployee employee, string fullName, string specialtyName)
    {
        public Guid Id { get; } = id;
        public TestEmployee Employee { get; } = employee;
        public string FullName { get; } = fullName;
        public string SpecialtyName { get; } = specialtyName;
    }

    public sealed class TestWorkSchedule(Guid id, Guid doctorId, string doctorName, string specialtyName, DateOnly date, TimeOnly shiftStart, TimeOnly shiftEnd, int recommendedCapacity)
    {
        public Guid Id { get; } = id;
        public Guid DoctorId { get; } = doctorId;
        public string DoctorName { get; } = doctorName;
        public string SpecialtyName { get; } = specialtyName;
        public DateOnly Date { get; } = date;
        public TimeOnly ShiftStart { get; } = shiftStart;
        public TimeOnly ShiftEnd { get; } = shiftEnd;
        public int RecommendedCapacity { get; } = recommendedCapacity;
    }
}


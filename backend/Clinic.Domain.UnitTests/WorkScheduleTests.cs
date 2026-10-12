using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;

namespace Clinic.Domain.UnitTests
{
    public class WorkScheduleTests
    {
        [Fact]
        public void TestCreateWorkScheduleValid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);

            // Act & Assert
            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5);
            Assert.Equal(doctor, workSchedule.Doctor);
        }

        [Fact]
        public void TestCreateWorkScheduleInvalid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);

            Assert.Throws<ArgumentException>(() => new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(1)), 5));
            Assert.Throws<ArgumentException>(() => new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(-1)), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5));
            Assert.Throws<ArgumentException>(() => new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), -1));
        }

        [Fact]
        public void TestUpdateWorkSchedule()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);

            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5);
            workSchedule.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(3)), TimeOnly.FromDateTime(now.AddHours(4)), 10);

            Assert.True(workSchedule.ShiftStart > TimeOnly.FromDateTime(now.AddHours(2)));
            Assert.True(workSchedule.ShiftEnd > TimeOnly.FromDateTime(now.AddHours(3)));
            Assert.Equal(10, workSchedule.PatientLimit);
        }

        [Fact]
        public void TestUpdateWorkScheduleInvalid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);
            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5);

            Assert.Throws<ArgumentException>(() => workSchedule.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(1)), 5));
            Assert.Throws<ArgumentException>(() => workSchedule.UpdateShift(DateOnly.FromDateTime(now.AddDays(-1)), TimeOnly.FromDateTime(now.AddHours(-2)), TimeOnly.FromDateTime(now.AddHours(1)), 5));
            Assert.Throws<ArgumentException>(() => workSchedule.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), -1));

            // Test updating a shift that has already started
            // Pls ignore if getting an error about shift start greater than shift end (wish i knew abt TimeProvider earlier).
            // Better go to bed at that moment then
            now = DateTime.UtcNow.AddHours(7);
            var nowShift = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddSeconds(1)), TimeOnly.FromDateTime(now.AddHours(1)), 5);
            Thread.Sleep(2000); // Wait for 2 seconds to ensure the shift has started
            Assert.Throws<InvalidOperationException>(() => nowShift.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), 10));
            nowShift.UpdateShift(DateOnly.FromDateTime(now), nowShift.ShiftStart, nowShift.ShiftEnd.AddHours(1), 15); // This should be valid since we are not changing the start time
            Assert.Equal(15, nowShift.PatientLimit);
            Assert.Throws<InvalidOperationException>(() => nowShift.Delete());
        }

        [Fact]
        public void TestDeleteWorkSchedule()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30))), 0);
            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5);
            workSchedule.Delete();
            Assert.True(workSchedule.IsDeleted);

            // Test deleting a shift that has already started
            // Pls ignore if getting an error about shift start greater than shift end (wish i knew abt TimeProvider earlier).
            // Better go to bed at that moment then
            now = DateTime.UtcNow.AddHours(7);
            var nowShift = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddSeconds(1)), TimeOnly.FromDateTime(now.AddHours(1)), 5);
            Thread.Sleep(2000); // Wait for 2 seconds to ensure the shift has started
            Assert.Throws<InvalidOperationException>(() => nowShift.Delete());
        }

        [Fact]
        public void TestAddAppointmentToWorkSchedule()
        {
            var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30))), 0);
            var workSchedule = new WorkSchedule(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 2);
            var patient = new Patient(person, "INS123", "00000000");
            var appointment = new Appointment(patient, workSchedule, TimeOnly.FromDateTime(now.AddHours(1)), "Reason", Guid.NewGuid());

            workSchedule.AddAppointment(appointment);
            Assert.Single(workSchedule.Appointments);
            
            var appointment2 = new Appointment(patient, workSchedule, TimeOnly.FromDateTime(now.AddHours(1)), "Reason2", Guid.NewGuid());
            Assert.Throws<ConflictException>(() => workSchedule.AddAppointment(appointment2));
            
            appointment2.Update(workSchedule, TimeOnly.FromDateTime(now.AddHours(1).AddMinutes(15)), "Reason2", Guid.NewGuid());
            workSchedule.AddAppointment(appointment2);
            Assert.Equal(2, workSchedule.Appointments.Count);
            
            var appointment3 = new Appointment(patient, workSchedule, TimeOnly.FromDateTime(now.AddHours(1).AddMinutes(30)), "Reason3", Guid.NewGuid());
            Assert.Throws<ConflictException>(() => workSchedule.AddAppointment(appointment3));

            workSchedule.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(3)), 3);
            workSchedule.Cancel("Reason");
            Assert.Throws<InvalidOperationException>(() => workSchedule.AddAppointment(appointment3));
        }
    }
}

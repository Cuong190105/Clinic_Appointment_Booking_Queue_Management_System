using Clinic.Domain.Entities;

namespace Clinic.Domain.UnitTests
{
    public class ShiftRequestTests
    {
        [Fact]
        public void TestCreateShiftRequestValid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);
            var shift = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5, "");
            Assert.Equal(doctor, shift.Doctor);
            Assert.Equal(5, shift.PatientLimit);
        }

        [Fact]
        public void TestCreateShiftRequestInvalid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);

            Assert.Throws<ArgumentException>(() => new ShiftRequest(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(1)), 5, ""));
            Assert.Throws<ArgumentException>(() => new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(-1)), TimeOnly.FromDateTime(now.AddHours(-2)), TimeOnly.FromDateTime(now.AddHours(1)), 5, ""));
            Assert.Throws<ArgumentNullException>(() => new ShiftRequest(null!, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), 5, ""));
            Assert.Throws<ArgumentException>(() => new ShiftRequest(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), -1, ""));
        }

        [Fact]
        public void TestUpdateShiftRequestInvalid()
        {
            var now = DateTime.UtcNow.AddHours(7);
            if (now.Hour >= 20)
            {
                now = now.AddHours(12);
            }
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(now.AddYears(-30)), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(now.AddYears(-30)));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(now.AddYears(-30))), 0);

            var shift = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddHours(1)), TimeOnly.FromDateTime(now.AddHours(2)), 5, "");

            Assert.Throws<ArgumentException>(() => shift.UpdateShift(DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(1)), 5, ""));
            Assert.Throws<ArgumentException>(() => shift.UpdateShift(DateOnly.FromDateTime(now.AddDays(-1)), TimeOnly.FromDateTime(now.AddHours(-2)), TimeOnly.FromDateTime(now.AddHours(1)), 5, ""));
            Assert.Throws<ArgumentException>(() => shift.UpdateShift(DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), -1, ""));

            Assert.Empty(doctor.WorkSchedules);
            shift.Approve();
            Assert.Single(doctor.WorkSchedules);

            Assert.Throws<InvalidOperationException>(() => shift.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), 5, ""));

            Assert.Throws<InvalidOperationException>(() => shift.Cancel());
            Assert.Throws<InvalidOperationException>(() => shift.Approve());
            Assert.Throws<InvalidOperationException>(() => shift.Reject());

            // Test deleting a shift that has already started
            // Pls ignore if getting an error about shift start greater than shift end (wish i knew abt TimeProvider earlier).
            // Better go to bed at that moment then
            now = DateTime.UtcNow.AddHours(7);
            var nowShift = new ShiftRequest(doctor, DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddSeconds(1)), TimeOnly.FromDateTime(now.AddHours(1)), 5, "");

            Thread.Sleep(1500);
            Assert.Throws<InvalidOperationException>(() => nowShift.UpdateShift(DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now.AddHours(2)), TimeOnly.FromDateTime(now.AddHours(3)), 5, ""));

        }
    }
}

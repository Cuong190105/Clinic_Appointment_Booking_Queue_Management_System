using Clinic.Domain.Entities;

namespace Clinic.Domain.UnitTests
{
    public class WorkHistoryTests
    {
        [Fact]
        public void TestCreateWorkHistoryValid()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, new DateOnly(2020, 1, 1));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            var specialty = new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow));
            var startDate = new DateOnly(2020, 1, 1);
            // Act
            var workHistory = new WorkHistory(doctor, specialty, startDate);
            // Assert
            Assert.Equal(doctor.Id, workHistory.DoctorId);
            Assert.Equal(specialty.Id, workHistory.SpecialtyId);
            Assert.Equal(startDate, workHistory.StartDate);
            Assert.Null(workHistory.EndDate);
        }

        [Fact]
        public void TestCreateWorkHistoryInvalid()
        {
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, new DateOnly(2020, 1, 1));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            var specialty = new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow));
            var startDate = new DateOnly(2020, 1, 1);
            // Act
            Assert.Throws<ArgumentNullException>(() => new WorkHistory(null, specialty, startDate));
            Assert.Throws<ArgumentNullException>(() => new WorkHistory(doctor, null, startDate));
            Assert.Throws<ArgumentException>(() => new WorkHistory(doctor, specialty, new DateOnly(2010, 10, 1)));
        }

        [Fact]
        public void TestEndWorkHistory()
        {
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, new DateOnly(2020, 1, 1));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            var specialty = new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow));
            var startDate = new DateOnly(2020, 1, 1);
            // Act
            var workHistory = new WorkHistory(doctor, specialty, startDate);
            Assert.Throws<ArgumentException>(() => workHistory.EndWorkHistory(new DateOnly(2019, 1, 1)));
            workHistory.EndWorkHistory(new DateOnly(2021, 1, 1));
            Assert.Equal(new DateOnly(2021, 1, 1), workHistory.EndDate);
            Assert.Throws<InvalidOperationException>(() => workHistory.EndWorkHistory());
        }
    }
}

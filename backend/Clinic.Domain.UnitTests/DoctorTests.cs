using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Domain.UnitTests
{
    public class DoctorTests
    {
        [Fact]
        public void TestCreateDoctorValid()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Assert
            Assert.NotNull(doctor);
        }

        [Fact]
        public void TestCreateDoctorNoLicense()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, null!, "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "    ", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
        }

        [Fact]
        public void TestCreateDoctorNoQualification()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "123ABC", null!, new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "123ABC", "", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "123ABC", "    ", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
        }

        [Fact]
        public void TestCreateDoctorNegativeExperienceYears()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            Assert.Throws<ArgumentException>(() => new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), -1));
        }

        [Fact]
        public void TestCreateDoctorNullEmployee()
        {
            // Act
            Assert.Throws<ArgumentNullException>(() => new Doctor(null!, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0));
        }

        [Fact]
        public void TestCreateDoctorNullSpecialty()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            // Act
            Assert.Throws<ArgumentNullException>(() => new Doctor(employee, "123ABC", "Tien si", null!, 0));
        }

        [Fact]
        public void TestUpdateDoctorStatus()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);

            doctor.UpdateInfo("456DEF", "PGS TS", 2, "Updated biography");
            Assert.Equal("Updated biography", doctor.Biography);
            Assert.Equal("456DEF", doctor.LicenseNumber);
        }

        [Fact]
        public void TestUpdateDoctorInfoInvalid()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo(null!, "PGS TS", 2, "Updated biography"));
            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo("", "PGS TS", 2, "Updated biography"));
            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo("    ", "PGS TS", 2, "Updated biography"));

            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo("456DEF", null!, 2, "Updated biography"));
            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo("456DEF", "", 2, "Updated biography"));
            Assert.Throws<ArgumentNullException>(() => doctor.UpdateInfo("456DEF", "    ", 2, "Updated biography"));

            Assert.Throws<ArgumentException>(() => doctor.UpdateInfo("456DEF", "PGS TS", -1, "Updated biography"));
        }

        [Fact]
        public void TestAddWorkHistoryToDoctor()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name1", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Act
            doctor.ChangeSpecialty(new Specialty("name2", "", DateOnly.FromDateTime(DateTime.UtcNow)));
            // Assert
            // Check that the work history for the first specialty has an end date
            var wh1 = doctor.WorkHistories.FirstOrDefault(x => x.Specialty.Name == "name1");
            Assert.NotNull(wh1);
            Assert.NotNull(wh1.EndDate);

            // Check that the work history for the second specialty has no end date
            var wh2 = doctor.WorkHistories.FirstOrDefault(x => x.Specialty.Name == "name2");
            Assert.NotNull(wh2);
            Assert.Null(wh2.EndDate);

            // Change specialty with no new specialty and check that the previous work history has an end date and the update makes no error
            doctor.ChangeSpecialty();
            Assert.NotNull(wh2.EndDate);
        }

        [Fact]
        public void TestAddWorkScheduleToDoctor()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Act
            var now = DateTime.UtcNow;
            var shift = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5);
            var overlappedShift = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1).AddHours(2)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(2)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(6)), 5);
            doctor.AddWorkSchedule(shift);
            // Assert
            Assert.Contains(shift, doctor.WorkSchedules);
            Assert.Throws<ConflictException>(() => doctor.AddWorkSchedule(overlappedShift));
        }

        [Fact]
        public void TestRemoveSchedule()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Act
            var now = DateTime.UtcNow;
            var shift = new WorkSchedule(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5);
            doctor.AddWorkSchedule(shift);
            // Assert
            doctor.RemoveWorkSchedule(shift);
            Assert.DoesNotContain(shift, doctor.WorkSchedules);
            Assert.Throws<InvalidOperationException>(() => doctor.RemoveWorkSchedule(shift)); // Removing again should throw an exception
        }

        [Fact]
        public void TestAddShiftRequestToDoctor()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Act
            var now = DateTime.UtcNow;
            var shift = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5, "Reason");
            doctor.AddShiftRequest(shift);
            // Assert
            Assert.Contains(shift, doctor.ShiftRequests);
            Assert.Throws<ConflictException>(() => doctor.AddShiftRequest(shift));
        }

        [Fact]
        public void TestRemoveShiftRequest()
        {
            // Arrange
            var person = new Person("Full name", "00000000", "email@example.com", DateOnly.FromDateTime(DateTime.UtcNow), Enums.Gender.Male, "");
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            var doctor = new Doctor(employee, "123ABC", "Tien si", new Specialty("name", "", DateOnly.FromDateTime(DateTime.UtcNow)), 0);
            // Act
            var now = DateTime.UtcNow;
            var shift = new ShiftRequest(doctor, DateOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1)), TimeOnly.FromDateTime(now.AddDays(1).AddHours(4)), 5, "Reason");
            doctor.AddShiftRequest(shift);
            // Assert
            Assert.Contains(shift, doctor.ShiftRequests);
            doctor.RemoveShiftRequest(shift);
            Assert.DoesNotContain(shift, doctor.ShiftRequests);
            Assert.Throws<InvalidOperationException>(() => doctor.RemoveShiftRequest(shift));
        }
    }
}

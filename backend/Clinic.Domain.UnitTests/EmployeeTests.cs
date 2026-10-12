using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Domain.UnitTests.Common;

namespace Clinic.Domain.UnitTests
{
    public class EmployeeTests
    {
        [Fact]
        public void CreateEmployee_WithValidData_ShouldInitializeActive()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson(fullName: "Bob Manager");
            var hireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));

            // Act
            var employee = new Employee(person, hireDate, status: EmployeeStatus.Active);

            // Assert
            Assert.Equal(person.Id, employee.PersonId);
            Assert.Equal(person, employee.Person);
            Assert.Equal(hireDate, employee.HireDate);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
            Assert.Null(employee.ManagerId);
        }

        [Fact]
        public void CreateEmployee_HireDateTooFarInFuture_ShouldThrowArgumentException()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var farFutureHireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new Employee(person, farFutureHireDate));
        }

        [Fact]
        public void AssignManager_ValidManager_ShouldSetManager()
        {
            // Arrange
            var managerPerson = TestDataFactory.CreatePerson(fullName: "Senior Manager");
            var manager = new Employee(managerPerson, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));

            var employeePerson = TestDataFactory.CreatePerson(fullName: "Junior Staff");
            var employee = new Employee(employeePerson, DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            employee.AssignManager(manager);

            // Assert
            Assert.Equal(manager.Id, employee.ManagerId);
            Assert.Equal(manager, employee.Manager);
        }

        [Fact]
        public void AssignManager_SelfAsManager_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                employee.AssignManager(employee));
        }

        [Fact]
        public void UpdateStatus_ShouldChangeStatusAndMarkUpdated()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            employee.UpdateStatus(EmployeeStatus.OnLeave);

            // Assert
            Assert.Equal(EmployeeStatus.OnLeave, employee.Status);
            Assert.NotNull(employee.UpdatedAt);
        }

        [Fact]
        public void Delete_ShouldSetIsDeletedTrue()
        {
            // Arrange
            var person = TestDataFactory.CreatePerson();
            var employee = new Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            employee.Delete();

            // Assert
            Assert.True(employee.IsDeleted);
        }
    }
}

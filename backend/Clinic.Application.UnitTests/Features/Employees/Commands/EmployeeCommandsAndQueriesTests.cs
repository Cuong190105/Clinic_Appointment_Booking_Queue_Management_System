using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Employees.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Employees.Commands
{
    public class EmployeeCommandsAndQueriesTests
    {
        [Fact]
        public async Task CreateEmployeeFromUser_ValidUser_ShouldCreateEmployee()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();

            var handler = new CreateEmployeeFromUserCommandHandler(
                userRepository, employeeRepository, roleRepository, unitOfWork);

            var person = TestDataFactory.CreatePerson(email: "receptionist@example.com", address: "456 Clinic Ave");
            var patient = TestDataFactory.CreatePatient(person);
            var user = TestDataFactory.CreateUser("Patient", "receptionist_user", "hash", person);
            await userRepository.AddAsync(user);

            var command = new CreateEmployeeFromUserCommand(
                user.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ["Receptionist"],
                "receptionist@example.com",
                "456 Clinic Ave"
            );

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(person.FullName, result.FullName);
            Assert.Contains("Receptionist", result.Roles);
            var employee = await employeeRepository.GetByIdAsync(result.Id);
            Assert.NotNull(employee);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
        }

        [Fact]
        public async Task CreateEmployeeFromUser_AlreadyEmployee_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();

            var handler = new CreateEmployeeFromUserCommandHandler(
                userRepository, employeeRepository, roleRepository, unitOfWork);

            var person = TestDataFactory.CreatePerson(email: "existing@example.com", address: "123 Main St", userRole: "Admin");
            var existingEmployee = new Domain.Entities.Employee(person, DateOnly.FromDateTime(DateTime.UtcNow));
            person.Employee = existingEmployee;
            var user = person.User!;
            await userRepository.AddAsync(user);

            var command = new CreateEmployeeFromUserCommand(
                user.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ["Admin"],
                "existing@example.com",
                "123 Main St"
            );

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateEmployeeStatus_ValidEmployee_ShouldUpdateStatus()
        {
            // Arrange
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateEmployeeStatusCommandHandler(employeeRepository, unitOfWork);

            var employee = TestDataFactory.CreateEmployee();
            await employeeRepository.AddAsync(employee);

            var command = new UpdateEmployeeStatusCommand(employee.Id, EmployeeStatus.Resigned);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(employee.Id, result);
            Assert.Equal(EmployeeStatus.Resigned, employee.Status);
        }

        [Fact]
        public async Task UpdateEmployeeStatus_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateEmployeeStatusCommandHandler(employeeRepository, unitOfWork);

            var command = new UpdateEmployeeStatusCommand(Guid.NewGuid(), EmployeeStatus.Resigned);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}

using Clinic.Application.Features.Employees.Commands;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System.Net;

namespace Clinic.Application.UnitTests.Features.Employees.Commands
{
    public class CreateEmployeeTests
    {
        [Fact]
        public async Task CreateEmployeeCommandHandler_Should_Create_Employee_Successfully()
        {
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateEmployeeCommandHandler(
                new UserService(userRepository, personRepository, passwordHasher),
                new PersonService(personRepository),
                employeeRepository,
                roleRepository,
                unitOfWork
            );

            var command = new CreateEmployeeCommand
            (
                Username: "testuser",
                Password: "testpassword",
                FullName: "John Doe",
                PhoneNumber: "1234567890",
                Email: "johndoe@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: DateOnly.FromDateTime(DateTime.Now),
                Roles: new List<string> { "Receptionist" }
            );

            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(await employeeRepository.GetByIdAsync(result.Id));
        }

        [Fact]
        public async Task CreateEmployeeCommandHandler_WrongRoleName_Should_Throw_ArgumentException()
        {
            var userRepository = new FakeUserRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateEmployeeCommandHandler(
                new UserService(userRepository, personRepository, passwordHasher),
                new PersonService(personRepository),
                employeeRepository,
                roleRepository,
                unitOfWork
            );

            var command = new CreateEmployeeCommand
            (
                Username: "testuser",
                Password: "testpassword",
                FullName: "John Doe",
                PhoneNumber: "1234567890",
                Email: "johndoe@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: DateOnly.FromDateTime(DateTime.Now),
                Roles: new List<string> { "WrongRole" }
            );

            await Assert.ThrowsAsync<ArgumentException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}

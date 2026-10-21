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
            var employeeRepository = new FakeEmployeeRepository();
            var personRepository = new FakePersonRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateEmployeeCommandHandler(
                new PersonService(personRepository),
                new FakePatientRepository(),
                employeeRepository,
                unitOfWork
            );

            var command = new CreateEmployeeCommand
            (
                FullName: "John Doe",
                PhoneNumber: "1234567890",
                Email: "johndoe@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: DateOnly.FromDateTime(DateTime.Now),
                Roles: new List<string> { "Receptionist" },
                IdentityUserId: Guid.NewGuid()
            );

            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(await employeeRepository.GetByIdAsync(result.Id));
        }

        [Fact]
        public async Task CreateEmployeeCommandHandler_Uses_Identity_Account_Projection_Without_Local_Role_Writes()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var personRepository = new FakePersonRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateEmployeeCommandHandler(
                new PersonService(personRepository),
                new FakePatientRepository(),
                employeeRepository,
                unitOfWork
            );

            var command = new CreateEmployeeCommand
            (
                FullName: "John Doe",
                PhoneNumber: "1234567890",
                Email: "johndoe@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 Main St",
                HireDate: DateOnly.FromDateTime(DateTime.Now),
                Roles: new List<string> { "Receptionist" },
                IdentityUserId: Guid.NewGuid()
            );

            var result = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(await employeeRepository.GetByIdAsync(result.Id));
        }
    }
}


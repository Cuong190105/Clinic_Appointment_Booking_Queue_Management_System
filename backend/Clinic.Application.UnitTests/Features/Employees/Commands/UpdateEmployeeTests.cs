using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Employees.Commands;
using Clinic.Application.Interfaces;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System.Net;
using System.Net.NetworkInformation;

namespace Clinic.Application.UnitTests.Features.Employees.Commands
{
    public class UpdateEmployeeTests
    {
        [Fact]
        public async Task TestValidUpdateEmployeeCommand()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateEmployeeCommandHandler(userRepository, personRepository, employeeRepository, roleRepository, unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var employee = TestDataFactory.CreateEmployee(person: person);
            var admin = TestDataFactory.CreateEmployee(role: "Admin");

            await employeeRepository.AddAsync(employee);
            await employeeRepository.AddAsync(admin);
            await userRepository.AddAsync(admin.Person.User);
            await userRepository.AddAsync(user);

            var command = new UpdateEmployeeCommand(
                UserId: admin.Person.User.Id,
                FullName: "New FName",
                PhoneNumber: "0111222333",
                Email: "newemail@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 New Address",
                Roles: new List<string> { "Receptionist" },
                EmployeeId: employee.Id
            );
            var result = await handler.Handle(command, CancellationToken.None);

            Assert.Equal("New FName", employee.Person.FullName);
            Assert.NotNull(employee.Person.User.UserRoles.FirstOrDefault(r => r.Role.Name == "Receptionist"));

            var command2 = new UpdateEmployeeCommand(
                UserId: user.Id,
                FullName: "New FName2",
                PhoneNumber: "0111222334",
                Email: "newemail2@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 New Address",
                Roles: null
            );
            var result2 = await handler.Handle(command2, CancellationToken.None);
            Assert.Equal("New FName2", employee.Person.FullName);
        }

        [Fact]
        public async Task TestUpdateNonExistentEmployeeCommand()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateEmployeeCommandHandler(userRepository, personRepository, employeeRepository, roleRepository, unitOfWork);
            var admin = TestDataFactory.CreateEmployee(role: "Admin");
            await employeeRepository.AddAsync(admin);
            await userRepository.AddAsync(admin.Person.User);
            var command = new UpdateEmployeeCommand(
                UserId: admin.Person.User.Id,
                FullName: "New FName",
                PhoneNumber: "0111222333",
                Email: "newemail@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 New Address",
                Roles: new List<string> { "Receptionist" },
                EmployeeId: Guid.NewGuid()
            );
             await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task TestInvalidRoleUpdateEmployeeCommand()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var employeeRepository = new FakeEmployeeRepository();
            var roleRepository = new FakeRoleRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new UpdateEmployeeCommandHandler(userRepository, personRepository, employeeRepository, roleRepository, unitOfWork);
            var employee = TestDataFactory.CreateEmployee();
            var otherEmployee = TestDataFactory.CreateEmployee(role: "Receptionist");
            await employeeRepository.AddAsync(employee);
            await employeeRepository.AddAsync(otherEmployee);
            await userRepository.AddAsync(otherEmployee.Person.User);
            await userRepository.AddAsync(employee.Person.User);
            var command = new UpdateEmployeeCommand(
                UserId: otherEmployee.Person.User.Id,
                FullName: "New FName",
                PhoneNumber: "0111222333",
                Email: "newemail@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 New Address",
                Roles: new List<string> { "InvalidRole" },
                EmployeeId: employee.Id
            );
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(command, CancellationToken.None));

            var command2 = new UpdateEmployeeCommand(
                UserId: employee.Person.User.Id,
                FullName: "New FName2",
                PhoneNumber: "0111222334",
                Email: "newemail2@example.com",
                DateOfBirth: DateOnly.FromDateTime(DateTime.Now.AddYears(-30)),
                Gender: Gender.Male,
                Address: "123 New Address",
                Roles: new List<string> { "Receptionist" }
            );
            await Assert.ThrowsAsync<ForbiddenException>(async () => await handler.Handle(command2, CancellationToken.None));
        }
    }
}

using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Employees.Commands;
using Clinic.Application.UnitTests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Features.Employees.Commands
{
    public class DeleteEmployeeTests
    {
        [Fact]
        public async Task TestDeleteValid()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteEmployeeCommandHandler(employeeRepository, unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var employee = TestDataFactory.CreateEmployee(person: person);
            await employeeRepository.AddAsync(employee);
            var command = new DeleteEmployeeCommand(EmployeeId: employee.Id);
            await handler.Handle(command, CancellationToken.None);
            Assert.True(employee.IsDeleted);
        }

        [Fact]
        public async Task TestDeleteNonExistent()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new DeleteEmployeeCommandHandler(employeeRepository, unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser(person: person);
            var employee = TestDataFactory.CreateEmployee(person: person);
            await employeeRepository.AddAsync(employee);
            var command = new DeleteEmployeeCommand(EmployeeId: Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(command, CancellationToken.None));
        }
    }
}

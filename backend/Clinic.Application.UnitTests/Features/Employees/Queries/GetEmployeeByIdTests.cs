using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Employees.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Employees.Queries
{
    public class GetEmployeeByIdTests
    {
        [Fact]
        public async Task TestGetEmployeeById()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeeByIdHandler(employeeRepository);
            var person = TestDataFactory.CreatePerson("John Doe 2");
            var user = TestDataFactory.CreateUser("Doctor", "johndoe2", "hashedpassword", person);
            var employee = TestDataFactory.CreateEmployee(person);
            var manager = TestDataFactory.CreateEmployee(TestDataFactory.CreatePerson("Manager Name"));
            employee.UpdateManager(manager);
            await employeeRepository.AddAsync(employee);

            var query = new GetEmployeeByIdQuery(employee.Id);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("John Doe 2", result.FullName);
            Assert.Equal("Manager Name", result.ManagerName);
        }

        [Fact]
        public async Task TestGetNonExistentEmployeeById()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeeByIdHandler(employeeRepository);
            var query = new GetEmployeeByIdQuery(Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(query, CancellationToken.None));
        }
    }
}

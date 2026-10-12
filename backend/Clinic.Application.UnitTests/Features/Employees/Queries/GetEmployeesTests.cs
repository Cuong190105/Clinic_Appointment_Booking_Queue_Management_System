using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.Employees.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Employees.Queries
{
    public class GetEmployeesTests
    {
        [Fact]
        public async Task TestGetAllEmployees()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeesHandler(employeeRepository);
            var person1 = TestDataFactory.CreatePerson(fullName: "P1", address: "A123");
            var person2 = TestDataFactory.CreatePerson(fullName: "P2", address: "A456");
            var user1 = TestDataFactory.CreateUser(username: "user1", person: person1);
            var user2 = TestDataFactory.CreateUser(username: "user2", person: person2);
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            await employeeRepository.AddAsync(employee1);
            await employeeRepository.AddAsync(employee2);
            var query = new GetEmployeesQuery();
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task TestSearchEmployees()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeesHandler(employeeRepository);
            var person1 = TestDataFactory.CreatePerson(fullName: "P1", address: "A123");
            var person2 = TestDataFactory.CreatePerson(fullName: "P2", address: "A456");
            var user1 = TestDataFactory.CreateUser(username: "user1", person: person1);
            var user2 = TestDataFactory.CreateUser(username: "user2", person: person2);
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            await employeeRepository.AddAsync(employee1);
            await employeeRepository.AddAsync(employee2);
            var query = new GetEmployeesQuery(Search: "P1");
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Single(result.Items);
            Assert.Equal(employee1.Id, result.Items[0].Id);

            var query2 = new GetEmployeesQuery(Search: "A1");
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.Single(result2.Items);
            Assert.Equal(employee1.Id, result2.Items[0].Id);
        }

        [Fact]
        public async Task TestSortEmployees()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeesHandler(employeeRepository);
            var person1 = TestDataFactory.CreatePerson(fullName: "A", dateOfBirth: new DateOnly(1990, 1, 1));
            var person2 = TestDataFactory.CreatePerson(fullName: "B", dateOfBirth: new DateOnly(1985, 1, 1));
            var user1 = TestDataFactory.CreateUser(username: "user1", person: person1);
            var user2 = TestDataFactory.CreateUser(username: "user2", person: person2);
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            await employeeRepository.AddAsync(employee1);
            await employeeRepository.AddAsync(employee2);
            var query = new GetEmployeesQuery(SortBy: "dateOfBirth", Descending: false);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(employee2.Id, result.Items[0].Id);
        }

        [Fact]
        public async Task TestFilterEmployees()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeesHandler(employeeRepository);
            var person1 = TestDataFactory.CreatePerson(fullName: "A");
            var person2 = TestDataFactory.CreatePerson(fullName: "B");
            var user1 = TestDataFactory.CreateUser(username: "user1", person: person1);
            var user2 = TestDataFactory.CreateUser(username: "user2", person: person2, role: "Doctor");
            var employee1 = TestDataFactory.CreateEmployee(person: person1);
            var employee2 = TestDataFactory.CreateEmployee(person: person2);
            await employeeRepository.AddAsync(employee1);
            await employeeRepository.AddAsync(employee2);
            var query = new GetEmployeesQuery(Gender: Domain.Enums.Gender.Female);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Empty(result.Items);

            var query2 = new GetEmployeesQuery(Status: Domain.Enums.EmployeeStatus.Active);
            var result2 = await handler.Handle(query2, CancellationToken.None);
            Assert.NotEmpty(result2.Items);
            Assert.Equal(2, result2.TotalCount);

            var query3 = new GetEmployeesQuery(Roles: new List<string> { "Admin" });
            var result3 = await handler.Handle(query3, CancellationToken.None);
            Assert.Single(result3.Items);
            Assert.Equal(1, result3.TotalCount);

            var query4 = new GetEmployeesQuery(Roles: new List<string> { "Admin", "Doctor" });
            var result4 = await handler.Handle(query4, CancellationToken.None);
            Assert.NotEmpty(result4.Items);
            Assert.Equal(2, result4.TotalCount);
        }

        [Fact]
        public async Task TestPaginationEmployees()
        {
            var employeeRepository = new FakeEmployeeRepository();
            var handler = new GetEmployeesHandler(employeeRepository);
            for (int i = 0; i < 15; i++)
            {
                var person = TestDataFactory.CreatePerson(fullName: $"Employee {i + 1}");
                var user = TestDataFactory.CreateUser(username: $"user{i + 1}", person: person);
                var employee = TestDataFactory.CreateEmployee(person: person);
                await employeeRepository.AddAsync(employee);
            }
            var query = new GetEmployeesQuery(PageNumber: 2, PageSize: 10);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Equal(5, result.Items.Count);
            Assert.Equal(15, result.TotalCount);
        }
    }
}

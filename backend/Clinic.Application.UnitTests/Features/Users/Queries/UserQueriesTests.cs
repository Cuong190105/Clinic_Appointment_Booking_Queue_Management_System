using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Users.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Users.Queries
{
    public class UserQueriesTests
    {
        [Fact]
        public async Task GetAllUsers_ShouldReturnBriefList()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var handler = new GetAllUsersQueryHandler(userRepository);

            var user1 = TestDataFactory.CreateUser("Admin", "user1");
            var user2 = TestDataFactory.CreateUser("Patient", "user2");
            await userRepository.AddAsync(user1);
            await userRepository.AddAsync(user2);

            // Act
            var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, u => u.Username == "user1");
            Assert.Contains(result, u => u.Username == "user2");
        }

        [Fact]
        public async Task GetUserById_Exists_ShouldReturnDetailDto()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var handler = new GetUserByIdQueryHandler(userRepository);

            var user = TestDataFactory.CreateUser("Doctor", "doctor_bob");
            await userRepository.AddAsync(user);

            // Act
            var result = await handler.Handle(new GetUserQuery(user.Id), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal("doctor_bob", result.Username);
            Assert.Equal(user.Person.FullName, result.FullName);
        }

        [Fact]
        public async Task GetUserById_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var handler = new GetUserByIdQueryHandler(userRepository);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                handler.Handle(new GetUserQuery(Guid.NewGuid()), CancellationToken.None));
        }

        [Fact]
        public async Task GetPagedUsers_ShouldReturnPaginatedResults()
        {
            // Arrange
            var userRepository = new FakeUserRepository();
            var handler = new GetPagedUsersQueryHandler(userRepository);

            for (int i = 1; i <= 5; i++)
            {
                var person = TestDataFactory.CreatePerson(fullName: $"Person {i}");
                var user = TestDataFactory.CreateUser("Patient", $"user_{i}", "hash", person);
                await userRepository.AddAsync(user);
            }

            var query = new GetPagedUsersQuery(PageNumber: 1, PageSize: 2);

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(2, result.PageSize);
        }
    }
}

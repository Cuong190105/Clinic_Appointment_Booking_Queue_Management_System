using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Services
{
    public class UserServiceTests
    {
        [Fact]
        public async Task TestFindUserById()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);

            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "username", "hashedpassword", person);

            await userRepository.AddAsync(user);
            var retrievedUser = await userService.GetByIdAsync(user.Id);
            Assert.NotNull(retrievedUser);
            Assert.Equal(retrievedUser.Id, user.Id);
            Assert.Null(await userService.GetByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task TestUserCreation()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);

            var person = TestDataFactory.CreatePerson();
            var user = await userService.CreateUserAsync("newuser", "password", person);
            Assert.NotNull(user);
            Assert.Equal("newuser", user.Username);
            Assert.NotEqual("password", user.PasswordHash);
        }

        [Fact]
        public async Task TestUserCreationDuplicateUsername()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);

            var person1 = TestDataFactory.CreatePerson();
            var user1 = TestDataFactory.CreateUser("Patient", "testuser", "hashedpassword", person1);
            await userRepository.AddAsync(user1);

            var person2 = TestDataFactory.CreatePerson();
            await Assert.ThrowsAsync<ArgumentException>(() => userService.CreateUserAsync("testuser", "password", person2));
            await Assert.ThrowsAsync<ArgumentException>(() => userService.CreateUserAsync("   testuser      ", "password", person2));
        }

        [Fact]
        public async Task TestVerifyUser()
        {
            var userRepository = new FakeUserRepository();
            var personRepository = new FakePersonRepository();
            var passwordHasher = new FakePasswordHasher();
            var userService = new UserService(userRepository, personRepository, passwordHasher);

            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "testuser", passwordHasher.HashPassword("password"), person);
            await userRepository.AddAsync(user);

            Assert.NotNull(await userService.VerifyUser("testuser", "password"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => userService.VerifyUser("testuser", "wrongpassword"));
        }
    }
}

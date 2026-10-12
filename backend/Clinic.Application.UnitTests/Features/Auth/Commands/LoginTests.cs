using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Auth.Commands
{
    public class LoginTests
    {
        [Fact]
        // Login success must return role names so clients can route users without inspecting JWT internals.
        public async Task TestLogin()
        {
            FakeUserRepository userRepository = new FakeUserRepository();
            FakePasswordHasher hasher = new FakePasswordHasher();
            FakeTokenProvider tokenProvider = new FakeTokenProvider();
            FakeRefreshTokenRepository refreshTokenRepository = new FakeRefreshTokenRepository();
            FakeUnitOfWork unitOfWork = new FakeUnitOfWork();
            var personRepository = new FakePersonRepository();
            LoginCommandHandler handler = new LoginCommandHandler(new UserService(userRepository, personRepository, hasher), new TokenService(tokenProvider, refreshTokenRepository), unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "testuser", hasher.HashPassword("password"), person);
            await userRepository.AddAsync(user);

            LoginCommand command = new(
                Username: "testuser",
                Password: "password"
            );

            LoginResponse response = await handler.Handle(command, CancellationToken.None);
            Assert.NotEmpty(response.AccessToken);
            Assert.NotEmpty(response.RefreshToken);
            Assert.Equal(new[] { "Patient" }, response.Roles);
            Assert.NotNull(await refreshTokenRepository.GetByTokenHashAsync(tokenProvider.HashToken(response.RefreshToken)));

            LoginCommand wrongCommand = new("testuser", hasher.HashPassword("wrongpassword"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await handler.Handle(wrongCommand, CancellationToken.None);
            });
        }

        [Fact]
        // Multi-role users must keep every assigned role in the login response for admin/staff hybrid accounts.
        public async Task TestLoginReturnsMultipleRoles()
        {
            FakeUserRepository userRepository = new FakeUserRepository();
            FakePasswordHasher hasher = new FakePasswordHasher();
            FakeTokenProvider tokenProvider = new FakeTokenProvider();
            FakeRefreshTokenRepository refreshTokenRepository = new FakeRefreshTokenRepository();
            FakeUnitOfWork unitOfWork = new FakeUnitOfWork();
            var personRepository = new FakePersonRepository();
            LoginCommandHandler handler = new LoginCommandHandler(new UserService(userRepository, personRepository, hasher), new TokenService(tokenProvider, refreshTokenRepository), unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "testuser", hasher.HashPassword("password"), person);
            user.AssignRole(TestDataFactory.RoleSet.First(role => role.Name == "Receptionist"));
            await userRepository.AddAsync(user);

            LoginCommand command = new(
                Username: "testuser",
                Password: "password"
            );

            LoginResponse response = await handler.Handle(command, CancellationToken.None);

            Assert.Equal(new[] { "Patient", "Receptionist" }, response.Roles);
        }

        [Fact]
        // Duplicate refresh-token retries should not drop the client role metadata from the successful login response.
        public async Task TestLoginDuplicateToken()
        {
            FakeUserRepository userRepository = new FakeUserRepository();
            FakePasswordHasher hasher = new FakePasswordHasher();
            FakeTokenProvider tokenProvider = new FakeTokenProvider();
            FakeRefreshTokenRepository refreshTokenRepository = new FakeRefreshTokenRepository();
            FakeUnitOfWork unitOfWork = new FakeUnitOfWork();
            var personRepository = new FakePersonRepository();
            LoginCommandHandler handler = new LoginCommandHandler(new UserService(userRepository, personRepository, hasher), new TokenService(tokenProvider, refreshTokenRepository), unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "testuser", hasher.HashPassword("password"), person);
            await userRepository.AddAsync(user);

            LoginCommand command = new(
                Username: "testuser",
                Password: "password"
            );
            refreshTokenRepository.SetupDuplicateToken(3); // Simulate duplicate token scenario

            LoginResponse response = await handler.Handle(command, CancellationToken.None);
            Assert.NotEmpty(response.AccessToken);
            Assert.NotEmpty(response.RefreshToken);
            Assert.Equal(new[] { "Patient" }, response.Roles);
            Assert.NotNull(await refreshTokenRepository.GetByTokenHashAsync(tokenProvider.HashToken(response.RefreshToken)));
            Assert.Equal(0, refreshTokenRepository.showRemainingDuplicateCount());
        }
    }
}

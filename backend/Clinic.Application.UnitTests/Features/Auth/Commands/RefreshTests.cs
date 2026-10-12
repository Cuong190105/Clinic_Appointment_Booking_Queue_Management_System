using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Features.Auth.Commands
{
    public class RefreshTests
    {
        [Fact]
        public async Task TestRefresh()
        {
            var userRepository = new FakeUserRepository();
            var hasher = new FakePasswordHasher();
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var unitOfWork = new FakeUnitOfWork();
            var personRepository = new FakePersonRepository();
            var handler = new RefreshCommandHandler(new UserService(userRepository, personRepository, hasher), new TokenService(tokenProvider, refreshTokenRepository), unitOfWork);
            var person = TestDataFactory.CreatePerson();
            var user = TestDataFactory.CreateUser("Patient", "testuser", hasher.HashPassword("password"), person);
            await userRepository.AddAsync(user);
            var refreshToken = tokenProvider.GenerateRefreshToken();
            var hashedToken = tokenProvider.HashToken(refreshToken);
            await refreshTokenRepository.AddAsync(new RefreshToken
            (hashedToken, DateTime.UtcNow.AddDays(7), user));
            var command = new RefreshCommand(refreshToken);
            var response = await handler.Handle(command, CancellationToken.None);
            Assert.NotEmpty(response.AccessToken);
            Assert.NotEmpty(response.RefreshToken);
        }
    }
}

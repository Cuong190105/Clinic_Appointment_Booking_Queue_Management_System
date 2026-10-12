using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Auth.Commands
{
    public class LogoutTests
    {
        [Fact]
        public async Task TestLogout()
        {
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenProvider = new FakeTokenProvider();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new LogoutCommandHandler(refreshTokenRepository, tokenProvider, unitOfWork);
            var refreshToken = "valid_refresh_token";
            var hashedToken = tokenProvider.HashToken(refreshToken);
            var user = TestDataFactory.CreateUser();
            var token = TestDataFactory.CreateRefreshToken(hashedToken, user);
            await refreshTokenRepository.AddAsync(token);
            var command = new LogoutCommand(refreshToken);
            await handler.Handle(command, CancellationToken.None);

            var revokedToken = await refreshTokenRepository.GetByTokenHashAsync(hashedToken);
            //Assert.NotNull(revokedToken);
            Assert.NotNull(revokedToken?.RevokedAt);
        }
    }
}

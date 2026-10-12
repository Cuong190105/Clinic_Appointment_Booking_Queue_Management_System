using Clinic.Application.Services;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Services
{
    public class TokenServiceTests
    {
        [Fact]
        public async Task TestGenerateTokensAsync()
        {
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenService = new TokenService(tokenProvider, refreshTokenRepository);
            var user = TestDataFactory.CreateUser("testuser", "password");
            var (accessToken, refreshToken) = await tokenService.GenerateTokensAsync(user);
            Assert.NotNull(accessToken);
            Assert.NotNull(refreshToken);
            var hashedRefreshToken = tokenProvider.HashToken(refreshToken);
            var storedRefreshToken = await refreshTokenRepository.GetByTokenHashAsync(hashedRefreshToken);
            Assert.NotNull(storedRefreshToken);
            Assert.Equal(user.Id, storedRefreshToken.User.Id);
        }

        [Fact]
        public async Task TestInactiveUserGenerateTokens()
        {
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenService = new TokenService(tokenProvider, refreshTokenRepository);
            var user = TestDataFactory.CreateUser("testuser", "password");
            user.IsActive = false;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await tokenService.GenerateTokensAsync(user));
        }

        [Fact]
        public async Task TestValidateRefreshTokenAsync()
        {
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenService = new TokenService(tokenProvider, refreshTokenRepository);
            var user = TestDataFactory.CreateUser("testuser", "password");
            var token = tokenProvider.GenerateRefreshToken();
            var refreshToken = new RefreshToken(
                tokenProvider.HashToken(token),
                expiresAt: DateTime.UtcNow.AddDays(7),
                user: user
            );
            user.IsActive = false;
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await tokenService.ValidateRefreshTokenAsync(token));
        }

        [Fact]
        public async Task TestValidateExpiredRefreshTokenAsync()
        {
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenService = new TokenService(tokenProvider, refreshTokenRepository);
            var user = TestDataFactory.CreateUser("testuser", "password");
            var token = tokenProvider.GenerateRefreshToken();
            var refreshToken = new RefreshToken(
                tokenProvider.HashToken(token),
                expiresAt: DateTime.UtcNow.AddDays(-1), // expired
                user: user
            );
            await refreshTokenRepository.AddAsync(refreshToken);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await tokenService.ValidateRefreshTokenAsync(token));
        }

        [Fact]
        public async Task TestValidateRevokedRefreshTokenAsync()
        {
            var tokenProvider = new FakeTokenProvider();
            var refreshTokenRepository = new FakeRefreshTokenRepository();
            var tokenService = new TokenService(tokenProvider, refreshTokenRepository);
            var user = TestDataFactory.CreateUser("testuser", "password");
            var token = tokenProvider.GenerateRefreshToken();
            var refreshToken = new RefreshToken(
                tokenProvider.HashToken(token),
                expiresAt: DateTime.UtcNow.AddDays(7),
                user: user
            );
            refreshToken.Revoke();
            await refreshTokenRepository.AddAsync(refreshToken);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await tokenService.ValidateRefreshTokenAsync(token));
        }
    }
}

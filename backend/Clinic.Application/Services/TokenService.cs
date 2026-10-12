using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Services
{
    public class TokenService : ITokenService
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public TokenService(ITokenProvider tokenProvider, IRefreshTokenRepository refreshTokenRepository)
        {
            _tokenProvider = tokenProvider;
            _refreshTokenRepository = refreshTokenRepository;
        }

        public async Task<TokenPair> GenerateTokensAsync(User user)
        {
            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedAccessException();
            }
            var accessToken = _tokenProvider.GenerateAccessToken(user);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var refreshToken = _tokenProvider.GenerateRefreshToken();
                var hashedRefreshToken = _tokenProvider.HashToken(refreshToken);
                var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(hashedRefreshToken);
                if (existingToken == null)
                {
                    var refreshTokenEntity = new RefreshToken
                    (
                        user: user,
                        tokenHash: hashedRefreshToken,
                        expiresAt: DateTime.UtcNow.AddDays(7)
                    );
                    await _refreshTokenRepository.AddAsync(refreshTokenEntity);
                    return new TokenPair(accessToken, refreshToken);
                }
            }
            throw new Exception("Failed to generate a unique refresh token after multiple attempts");
        }

        public async Task<User> ValidateRefreshTokenAsync(string refreshToken)
        {
            var hashedRefreshToken = _tokenProvider.HashToken(refreshToken);
            var refreshTokenEntity = await _refreshTokenRepository.GetByTokenHashAsync(hashedRefreshToken);
            if (refreshTokenEntity == null || refreshTokenEntity.ExpiresAt < DateTime.UtcNow || refreshTokenEntity.RevokedAt is not null)
            {
                throw new UnauthorizedAccessException();
            }

            return refreshTokenEntity.User;
        }
    }
}

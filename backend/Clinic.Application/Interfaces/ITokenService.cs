using Clinic.Application.Common.Models;
using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface ITokenService
    {
        /// <summary>
        /// Create a new pair of access and refresh tokens for the specified user. Throws an exception if the user is not active.
        /// </summary>
        Task<TokenPair> GenerateTokensAsync(User user);

        /// <summary>
        /// Validates the provided refresh token and returns the associated user if valid. Throws an exception if the token is invalid or expired.
        /// </summary>
        Task<User> ValidateRefreshTokenAsync(string refreshToken);
    }
}

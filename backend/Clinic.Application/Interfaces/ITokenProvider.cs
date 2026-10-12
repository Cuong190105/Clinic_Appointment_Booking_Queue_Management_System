using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface ITokenProvider
    {
        /// <summary>
        /// Generates an access token for the specified user. Typically JWT containing user claims (user ID, roles), issuer, audience and expiration information.
        /// </summary>
        string GenerateAccessToken(User user);

        /// <summary>
        /// Generates a secure random refresh token.
        /// </summary>
        string GenerateRefreshToken();

        /// <summary>
        /// Hashes the provided refresh token using a secure hashing algorithm (e.g., SHA-256) to store it securely in the database.
        /// </summary>
        string HashToken(string refreshToken);
    }
}

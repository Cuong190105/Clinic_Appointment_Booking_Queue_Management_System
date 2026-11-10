using Clinic.Domain.Common;
using System;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// LÆ°u refresh token dáº¡ng hash (khÃ´ng lÆ°u token gá»‘c) Ä‘á»ƒ cÃ³ thá»ƒ thu há»“i
    /// (RevokedAt) khi Ä‘Äƒng xuáº¥t, vÃ  phÃ¡t hiá»‡n token bá»‹ Ä‘Ã¡nh cáº¯p náº¿u dÃ¹ng láº¡i
    /// má»™t token Ä‘Ã£ bá»‹ thu há»“i/háº¿t háº¡n.
    /// </summary>
    public class RefreshToken
    {
        public Guid UserId { get; init; }
        public User User { get; init; } = null!;

        public Guid Id { get; init; }

        /// <summary>Hash cá»§a refresh token, khÃ´ng lÆ°u giÃ¡ trá»‹ gá»‘c.</summary>
        public string TokenHash { get; init; } = string.Empty;

        public DateTime IssuedAt { get; init; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; init; }

        /// <summary>NULL náº¿u token váº«n cÃ²n hiá»‡u lá»±c; cÃ³ giÃ¡ trá»‹ khi Ä‘Ã£ bá»‹ thu há»“i (logout, refresh 1 láº§n...).</summary>
        public DateTime? RevokedAt { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshToken"/> class.
        /// </summary>
        public RefreshToken(string tokenHash, DateTime expiresAt, User user)
        {
            Id = Guid.NewGuid();
            TokenHash = tokenHash;
            IssuedAt = DateTime.UtcNow;
            ExpiresAt = expiresAt;
            UserId = user.Id;
            User = user;
        }

        /// <summary>
        /// Rebuild the RefreshToken entity from the database
        /// </summary>
        public RefreshToken(Guid id, string tokenHash, DateTime issuedAt, DateTime expiresAt, DateTime? revokedAt, Guid userId)
        {
            Id = id;
            TokenHash = tokenHash;
            IssuedAt = issuedAt;
            ExpiresAt = expiresAt;
            RevokedAt = revokedAt;
            UserId = userId;
        }

        /// <summary>
        /// Revokes the refresh token 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Revoke()
        {
            if (RevokedAt != null)
            {
                throw new InvalidOperationException("This token has already revoked");
            }
            RevokedAt = DateTime.UtcNow;
        }
    }
}

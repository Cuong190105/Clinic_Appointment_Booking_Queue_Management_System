using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeTokenProvider : ITokenProvider
    {
        private int cnt = 0;
        public string GenerateAccessToken(User user)
        {
            return $"fake_access_token_for_user_{user.Id}";
        }

        public string GenerateRefreshToken()
        {
            return $"fake_refresh_token_{++cnt}";
        }

        public string HashToken(string refreshToken)
        {
            return $"fake_hashed_token_for_{refreshToken}";
        }
    }
}

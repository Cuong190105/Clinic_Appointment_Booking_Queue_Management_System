using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _tokens = [];
        private int _duplicateCount = 0;

        public async Task AddAsync(RefreshToken refreshToken)
        {
            _tokens.Add(refreshToken);
        }

        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            if (_duplicateCount > 0)
            {
                _duplicateCount--;
                return new RefreshToken(tokenHash, DateTime.UtcNow.AddDays(7), TestDataFactory.CreateUser("testuser", "password"));
            }
            //foreach(RefreshToken refreshToken in _tokens)
            //{
            //    Console.WriteLine(refreshToken.TokenHash);
            //}
            return _tokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {
            return;
        }

        public void SetupDuplicateToken(int count)
        {
            _duplicateCount = count;
        }

        public int showRemainingDuplicateCount()
        {
            return _duplicateCount;
        }
    }
}

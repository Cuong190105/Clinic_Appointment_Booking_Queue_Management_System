using Clinic.Application.Interfaces;

namespace Clinic.Application.UnitTests.Common
{
    public class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return $"HashedPasswordFor{password}";
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return hashedPassword == HashPassword(password);
        }
    }
}

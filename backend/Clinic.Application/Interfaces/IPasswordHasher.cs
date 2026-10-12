namespace Clinic.Application.Interfaces
{
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashes the given password using a secure hashing algorithm.
        /// </summary>
        string HashPassword(string password);

        /// <summary>
        /// Verifies if the given password matches the hashed password.
        /// </summary>
        bool VerifyPassword(string password, string hashedPassword);
    }
}

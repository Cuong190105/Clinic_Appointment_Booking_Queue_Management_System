using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface IUserService
    {
        /// <summary>
        /// Creates a new user with the specified username, password, and associated person ID. Throws an exception if the username is already taken, the person already has an account, or if the person does not exist.
        /// </summary>
        /// <returns></returns>
        Task<User> CreateUserAsync(string username, string password, Person person);

        /// <summary>
        /// Gets a user by their username. Returns null if the user does not exist.
        /// </summary>
        /// <returns></returns>
        Task<User?> GetByIdAsync(Guid id);

        /// <summary>
        /// Hash and store the new password for the specified user. Throws an exception if the user is null or if the new password is invalid.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="newPassword"></param>
        Task UpdatePassword(User user, string newPassword);
        Task VerifyAndUpdatePassword(Guid UserId, string currentPassword, string newPassword);

        /// <summary>
        /// Verifies the user's credentials and returns the user if valid, otherwise returns null.
        /// </summary>
        /// <returns></returns>
        Task<User> VerifyUser(string username, string password);
    }
}

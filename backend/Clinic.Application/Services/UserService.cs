using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;

namespace Clinic.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPersonRepository _personRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UserService(IUserRepository userRepository, IPersonRepository personRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _personRepository = personRepository;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Gets a user by their unique identifier.
        /// </summary>
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _userRepository.GetByIdAsync(id);
        }


        /// <summary>
        /// Creates a new user with the specified username, password, and associated person. The password is hashed before being stored.
        /// </summary>
        public async Task<User> CreateUserAsync(string username, string password, Person person)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(username.Trim());
            if (person.User != null)
            {
                throw new ArgumentException("The provided person is already associated with a user.");
            }
            if (existingUser != null)
            {
                throw new ArgumentException("A user with that username already exists.");
            }
            var hashedPassword = _passwordHasher.HashPassword(password);
            var user = new User(username, hashedPassword, person);
            await _userRepository.AddAsync(user);
            return user;
        }

        /// <summary>
        /// Verifies a user's credentials by checking the provided username and password against stored data. If the credentials are valid, the corresponding user is returned; otherwise, an exception is thrown.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public async Task<User> VerifyUser(string username, string password)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                var person = await _personRepository.GetByEmailAsync(username);
                if (person == null)
                {
                    person = await _personRepository.GetByPhoneNumberAsync(username);
                    if (person == null)
                    {
                        throw new UnauthorizedAccessException("Username or password is incorrect.");
                    }
                }
                user = person.User;
            }
            if (user == null
                || !_passwordHasher.VerifyPassword(password, user.PasswordHash)
                || !user.IsActive
            ){
                throw new UnauthorizedAccessException("Username or password is incorrect.");
            }

            return user;
        }

        public async Task UpdatePassword(User user, string password)
        {
            var hashedPassword = _passwordHasher.HashPassword(password);
            user.ResetPassword(hashedPassword);
            await _userRepository.UpdateAsync(user);
        }

        public async Task VerifyAndUpdatePassword(Guid userId, string currentPassword, string newPassword)
        {
            var user = await GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            await UpdatePassword(user, newPassword);
        }
    }
}

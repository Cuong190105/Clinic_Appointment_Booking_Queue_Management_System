using Clinic.Domain.Common;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Clinic.Domain.Entities
{
    /// <summary>
    /// Tài khoản đăng nhập. Không phải mọi Person đều có User (VD: khách vãng lai).
    /// </summary>
    public class User : BaseEntity
    {
        /// <summary>FK, UNIQUE - 1 Person chỉ có tối đa 1 tài khoản.</summary>
        public Guid PersonId { get; set; }
        public Person Person { get; set; } = null!;

        /// <summary>UNIQUE.</summary>
        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        /// <summary>
        /// Initializes a new instance of the <see cref="User"/> class with the specified username, password hash, and associated person.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public User(string username, string passwordHash, Person person)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash cannot be null or empty.", nameof(passwordHash));

            if (person == null)
                throw new ArgumentNullException("Person cannot be null.", nameof(person));

            Username = username.Trim();
            PasswordHash = passwordHash;
            Person = person;
            PersonId = person.Id;
            IsActive = true;
        }

        /// <summary>
        /// Constructor for reconstructing a User entity from the database with all properties specified.
        /// </summary>
        public User(Guid id, string username, string passwordHash, bool isActive, Guid personId, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            Username = username;
            PasswordHash = passwordHash;
            IsActive = isActive;
            PersonId = personId;
        }

        /// <summary>
        /// Updates the user's password hash.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void UpdatePassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("New password hash cannot be null or empty.", nameof(newPasswordHash));
            PasswordHash = newPasswordHash;
            MarkUpdated();
        }

        /// <summary>
        /// Changes the user's active status.
        /// </summary>
        public void ChangeStatus(bool isActive)
        {
            IsActive = isActive;
            MarkUpdated();
        }

        /// <summary>
        /// Assigns a role to the user. If the user already has the role, an exception is thrown.
        /// </summary>
        /// <param name="role"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void AssignRole(Role role)
        {
            if (role == null)
                throw new ArgumentNullException(nameof(role), "Role cannot be null.");
            if (UserRoles.Any(ur => ur.Role.Name == role.Name))
            {
                throw new ArgumentException($"User already has the role '{role.Name}' assigned.");
            }
            UserRoles.Add(new UserRole(this, role));
            MarkUpdated();
        }

        public void RemoveRole(Role role)
        {
            if (role == null)
                throw new ArgumentNullException(nameof(role), "Role cannot be null.");
            var userRole = UserRoles.FirstOrDefault(ur => ur.Role.Name == role.Name);
            if (userRole == null)
                throw new ArgumentException($"User does not have the role '{role.Name}' assigned.");
            UserRoles.Remove(userRole);

            MarkUpdated();
        }

        public void ResetPassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("New password hash cannot be null or empty.", nameof(newPasswordHash));
            PasswordHash = newPasswordHash;
            MarkUpdated();
        }
    }
}

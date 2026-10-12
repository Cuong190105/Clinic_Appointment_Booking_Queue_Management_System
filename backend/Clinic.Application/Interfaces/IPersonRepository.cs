using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Interfaces
{
    public interface IPersonRepository
    {
        /// <summary>
        /// Adds a new person to the repository.
        /// </summary>
        Task AddAsync(Person person);
        Task UpdateAsync(Person person);

        /// <summary>
        /// Gets a person by their email address.
        /// </summary>
        Task<Person?> GetByEmailAsync(string email);

        /// <summary>
        /// Gets a person by their phone number
        /// </summary>
        Task<Person?> GetByPhoneNumberAsync(string phoneNumber);

        /// <summary>
        /// Gets a person by their unique identifier (ID).
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Person?> GetByIdAsync(Guid id);

        Task DeleteAsync(Guid id);

        /// <summary>
        /// Gets a person by their full name, date of birth, and gender.
        /// </summary>
        Task<Person?> GetByBasicInfoAsync(string fullName, DateOnly dateOfBirth, Gender gender);
    }
}
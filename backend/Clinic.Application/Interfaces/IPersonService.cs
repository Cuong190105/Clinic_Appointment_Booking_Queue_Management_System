using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Interfaces
{
    public interface IPersonService
    {
        Task<Person?> GetByPhoneNumberAsync(string phoneNumber);
        Task<Person?> GetByEmailAsync(string email);
        Task<Person?> GetByIdAsync(Guid id);
        Task<ICollection<Person>> GetAllAsync();

        /// <summary>
        /// Creates a new person profile or links to an existing one based on the provided phone number.
        /// If the phone number is registered, override the existing number owner's details with the new information.
        /// If no phone number is provided, try to match the person by the combination of full name, date of birth, and gender.
        /// If a match is found, return that person; otherwise, create a new person profile with the provided details.
        /// </summary>
        /// <returns>The created or linked person entity.</returns>
        /// <exception cref="ArgumentException"></exception>
        Task<Person> CreateOrGetPersonAsync(string fullName, string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string address);
        Task UpdatePersonAsync(Guid id, string fullName, string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string address);
        Task DeletePersonAsync(Guid id);
    }
}

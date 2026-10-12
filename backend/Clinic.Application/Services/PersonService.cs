using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Services
{
    public class PersonService : IPersonService
    {
        private readonly IPersonRepository _personRepository;

        public PersonService(IPersonRepository personRepository)
        {
            _personRepository = personRepository;
        }

        public async Task<Person> CreateOrGetPersonAsync(string fullName, string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string address)
        {
            // Check if a person with the same phone number already exists
            // The clinic uses phone number as a unique identifier for patients.
            // If a patient already exists with the same phone number, link that profile to the new user account. If not, create a new person profile.
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                var existingPersonByPhone = await _personRepository.GetByPhoneNumberAsync(phoneNumber);
                if (existingPersonByPhone != null)
                {
                    // If this person already has a user account, don't allow registration.
                    if (existingPersonByPhone.User != null)
                    {
                        throw new ArgumentException("A user account already exists for this phone number.");
                    }

                    // Override the person's details if not empty
                    existingPersonByPhone.UpdateDetails(email, gender, address);
                    await _personRepository.UpdateAsync(existingPersonByPhone);
                    return existingPersonByPhone;
                }
            }
            var existingPersonByBasicInfo = await _personRepository.GetByBasicInfoAsync(fullName, dateOfBirth, gender);
            if (existingPersonByBasicInfo != null)
            {
                existingPersonByBasicInfo.UpdateDetails(email, gender, address);
                await _personRepository.UpdateAsync(existingPersonByBasicInfo);
                return existingPersonByBasicInfo;
            }

            var person = new Person
            (
                fullName: fullName,
                phoneNumber: phoneNumber,
                email: email,
                dateOfBirth: dateOfBirth,
                gender: gender,
                address: address
            );
            // Add the person to the repository
            await _personRepository.AddAsync(person);
            return person;
        }

        // Method phía dưới chưa dùng đến/đã dùng thông qua Repo
        public Task DeletePersonAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Person>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Person?> GetByEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<Person?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<Person?> GetByPhoneNumberAsync(string phoneNumber)
        {
            throw new NotImplementedException();
        }

        public Task UpdatePersonAsync(Guid id, string fullName, string phoneNumber, string email, DateOnly dateOfBirth, Gender gender, string address)
        {
            throw new NotImplementedException();
        }
    }
}

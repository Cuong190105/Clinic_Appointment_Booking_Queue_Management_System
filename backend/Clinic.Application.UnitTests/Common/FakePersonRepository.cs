using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
{
    public class FakePersonRepository : IPersonRepository
    {
        private readonly List<Person> _people = [];
        public async Task AddAsync(Person person)
        {
            _people.Add(person);
        }

        public Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public async Task<Person?> GetByBasicInfoAsync(string fullName, DateOnly dateOfBirth, Gender gender)
        {
            return _people.FirstOrDefault(p => p.FullName == fullName && p.DateOfBirth == dateOfBirth && p.Gender == gender);
        }

        public async Task<Person?> GetByEmailAsync(string email)
        {
            return _people.FirstOrDefault(p => p.Email == email);
        }

        public async Task<Person?> GetByIdAsync(Guid id)
        {
            return _people.FirstOrDefault(p => p.Id == id);
        }

        public async Task<Person?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return _people.FirstOrDefault(p => p.PhoneNumber == phoneNumber);
        }

        public async Task UpdateAsync(Person person)
        {
            return;
        }
    }
}

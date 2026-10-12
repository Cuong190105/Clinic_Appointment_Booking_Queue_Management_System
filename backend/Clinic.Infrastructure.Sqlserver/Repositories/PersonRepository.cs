using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class PersonRepository : IPersonRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public PersonRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(Person person)
        {
            await _dbContext.Persons.AddAsync(person);
        }

        public async Task<Person?> GetByEmailAsync(string email)
        {
            return await _dbContext.Persons
                .Include(p => p.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.RolePermissions)
                                .ThenInclude(rp => rp.Permission)
                .Include(p => p.Patient)
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Doctor)
                .FirstOrDefaultAsync(p => p.Email == email);
        }

        public async Task<Person?> GetByPhoneNumberAsync(string phoneNumber)
        {
            return await _dbContext.Persons
                .Include(p => p.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.RolePermissions)
                                .ThenInclude(rp => rp.Permission)
                .Include(p => p.Patient)
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Doctor)
                .FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber);
        }

        public async Task<Person?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Persons
                .Include(p => p.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Include(p => p.Patient)
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Doctor)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var person = await _dbContext.Persons.FindAsync(id);
            if (person != null)
            {
                person.Delete();
                _dbContext.Persons.Update(person);
            }
            else
            {
                throw new ArgumentException($"Person with id {id} not found.");
            }
        }
        public async Task UpdateAsync(Person person)
        {
            _dbContext.Persons.Update(person);
        }

        public async Task<Person?> GetByBasicInfoAsync(string fullName, DateOnly dateOfBirth, Gender gender)
        {
            return await _dbContext.Persons
                .Include(p => p.User)
                .Include(p => p.Patient)
                .Include(p => p.Employee)
                    .ThenInclude(e => e.Doctor)
                .FirstOrDefaultAsync(p => p.FullName == fullName && p.DateOfBirth == dateOfBirth && p.Gender == gender);
        }
    }
}

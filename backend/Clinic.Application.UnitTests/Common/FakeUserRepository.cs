using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];
        public async Task AddAsync(User user)
        {
            _users.Add(user);
        }

        public async Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return _users.Where(u => !u.IsDeleted).ToList();
        }

        public async Task<User?> GetByIdAsync(Guid? id)
        {
            return _users.FirstOrDefault(u => u.Id == id);
        }

        public async Task<User?> GetByPersonIdAsync(Guid id)
        {
            return _users.FirstOrDefault(u => u.PersonId == id);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return _users.FirstOrDefault(u => u.Username == username);
        }

        public async Task<PagedResult<User>> GetPagedAsync(string? search, string sortBy, string? role, Gender? gender, bool descending, int pageNumber, int pageSize)
        {
            var query = _users
                .Where(u => u.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Username.Contains(search) || u.Person.FullName.Contains(search));
            }

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name.Equals(role, StringComparison.OrdinalIgnoreCase)));
            }

            if (gender.HasValue)
            {
                query = query.Where(u => u.Person.Gender == gender.Value);
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "username" => descending ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
                    "fullName" => descending ? query.OrderByDescending(u => u.Person.FullName) : query.OrderBy(u => u.Person.FullName),
                    _ => descending ? query.OrderByDescending(u => u.Person.FullName) : query.OrderBy(u => u.Person.FullName),
                };
            } else
            {
                query = descending ? query.OrderByDescending(u => u.Person.FullName) : query.OrderBy(u => u.Person.FullName); // Default sorting by FullName
            }


            var totalItems = query.Count();
            var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<User>(items, totalItems);
        }

        public async Task UpdateAsync(User user)
        {
            return;
        }
    }
}

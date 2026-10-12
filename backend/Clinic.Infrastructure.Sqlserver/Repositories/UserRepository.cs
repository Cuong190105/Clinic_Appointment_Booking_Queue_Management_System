using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Clinic.Application.Interfaces;
using Clinic.Application.Common.Models;
using Clinic.Domain.Enums;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;
        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task DeleteAsync(Guid id)
        {
            var model = await _context.Users.FindAsync(id);
            if (model == null)
            {
                throw new InvalidOperationException("User not found");
            }
            _context.Users.Remove(model);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Person)
                .ToListAsync();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .Include(u => u.Person)
                    .ThenInclude(p => p.Patient)
                .Include(u => u.Person)
                    .ThenInclude(p => p.Employee)
                        .ThenInclude(e => e!.Doctor)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetByIdAsync(Guid? id)
        {
            if (id == null)
            {
                return null;
            }
            return await _context.Users
                .Include(u => u.Person)
                    .ThenInclude(p => p.Patient)
                .Include(u => u.Person)
                    .ThenInclude(p => p.Employee)
                        .ThenInclude(e => e!.Doctor)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
        }

        public async Task<User?> GetByPersonIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Person)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.PersonId == id);
        }

        public async Task<PagedResult<User>> GetPagedAsync(string? search, string sortBy, string? role, Gender? gender, bool descending, int pageNumber, int pageSize)
        {
            var query = _context.Users
                .Include(u => u.Person)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Username.Contains(search) || u.Person.FullName.Contains(search));
            }

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Name.ToLower() == role.ToLower()));
            }

            if (gender.HasValue)
            {
                query = query.Where(u => u.Person.Gender == gender.Value);
            }

            if (string.IsNullOrEmpty(sortBy))
            {
                sortBy = "fullname"; // Default sorting by FullName
            }

            query = sortBy.ToLower() switch
            {
                "username" => descending ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
                "fullName" => descending ? query.OrderByDescending(u => u.Person.FullName) : query.OrderBy(u => u.Person.FullName),
                _ => descending ? query.OrderByDescending(u => u.Person.FullName) : query.OrderBy(u => u.Person.FullName),
            };


            var totalItems = await query.CountAsync();
            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();

            return new PagedResult<User>(items, totalItems);
        }
    }
}
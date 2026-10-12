using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly ApplicationDbContext _context;
        public EmployeeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Employee employee)
        {
            _context.Employees.Add(employee);
        }

        public async Task<Employee?> GetByIdAsync(Guid employeeId)
        {
            return await _context.Employees
                .Include(e => e.Person)
                    .ThenInclude(p => p.User)
                    .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(e => e.Manager)
                    .ThenInclude(m => m.Person)
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsDeleted == false);
        }

        public async Task<Employee?> GetByPersonIdAsync(Guid personId)
        {
            return await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.User)
                .ThenInclude(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(e => e.PersonId == personId && e.IsDeleted == false);
        }

        public async Task<PagedResult<Employee>> GetPagedAsync(string? search, string sortBy, bool descending, List<string>? roles, Gender? gender, EmployeeStatus? status, int page, int pageSize)
        {
            var query = _context.Employees
                .Include(e => e.Person)
                    .ThenInclude(p => p.User)
                    .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(e => e.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Person.FullName.Contains(search) ||
                    (s.Person.Address != null && s.Person.Address.Contains(search)));
            }

            if (gender != null)
            {
                query = query.Where(e => e.Person.Gender == gender);
            }
            if (status != null)
            {
                query = query.Where(e => e.Status == status);
            }
            if (roles != null && roles.Count != 0)
            {
                query = query.Where(e => e.Person.User.UserRoles.Any(ur => roles.Contains(ur.Role.Name)));
            }

            if (string.IsNullOrEmpty(sortBy))
            {
                sortBy = "fullname"; // Default sorting by FullName
            }

            query = sortBy?.ToLower() switch
            {
                "fullname" => descending ? query.OrderByDescending(s => s.Person.FullName) : query.OrderBy(s => s.Person.FullName),
                "dateofbirth" => descending ? query.OrderByDescending(s => s.Person.DateOfBirth) : query.OrderBy(s => s.Person.DateOfBirth),
                _ => query.OrderBy(s => s.Person.FullName), // Default sorting by FullName
            };

            var totalCount = await query.CountAsync();

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();
            return new PagedResult<Employee>(items, totalCount);
        }
    }
}

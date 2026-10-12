using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class SpecialtyRepository : ISpecialtyRepository
    {
        private readonly ApplicationDbContext _context;
        public SpecialtyRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Specialty specialty)
        {
            _context.Specialties.Add(specialty);
        }

        public async Task<IEnumerable<Specialty>> GetAllAsync()
        {
            return await _context.Specialties.Where(s => s.IsDeleted == false).ToListAsync();
        }

        public async Task<Specialty?> GetByIdAsync(Guid id)
        {
            return await _context.Specialties.FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted == false);
        }

        public async Task<Specialty?> GetByNameAsync(string name)
        {
            return await _context.Specialties.FirstOrDefaultAsync(s => s.Name == name && s.IsDeleted == false);
        }

        public async Task<PagedResult<Specialty>> GetPagedAsync(string? search, string sortBy, bool descending, int page, int pageSize)
        {
            var query = _context.Specialties.Where(s => s.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s =>
                    s.Name.Contains(search) ||
                    (s.Description != null && s.Description.Contains(search)));
            }

            if (string.IsNullOrEmpty(sortBy))
            {
                sortBy = "name"; // Default sorting by Name
            }

            query = sortBy.ToLower() switch
            {
                "name" => descending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                "description" => descending ? query.OrderByDescending(s => s.Description) : query.OrderBy(s => s.Description),
                "establisheddate" => descending ? query.OrderByDescending(s => s.EstablishedDate) : query.OrderBy(s => s.EstablishedDate),
                _ => query.OrderBy(s => s.Name), // Default sorting by Name
            };

            var totalCount = await query.CountAsync();

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<Specialty>(items, totalCount);
        }

        public async Task UpdateAsync(Specialty specialty)
        {
            _context.Specialties.Update(specialty);
        }
    }
}

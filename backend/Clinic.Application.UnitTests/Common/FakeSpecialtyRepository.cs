using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common;
using Clinic.Domain.Entities;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeSpecialtyRepository : ISpecialtyRepository
    {
        private readonly List<Specialty> _specialties = [];
        public async Task<Specialty?> GetByIdAsync(Guid id)
        {
            return _specialties.FirstOrDefault(s => s.Id == id && !s.IsDeleted);
        }

        public async Task<Specialty?> GetByNameAsync(string name)
        {
            return _specialties.FirstOrDefault(s => s.Name == name && !s.IsDeleted);
        }

        public async Task<IEnumerable<Specialty>> GetAllAsync()
        {
            return _specialties.Where(s => !s.IsDeleted);
        }

        public async Task<PagedResult<Specialty>> GetPagedAsync(string? search, string sortBy, bool descending, int page, int pageSize)
        {
            var query = _specialties.Where(s => s.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search) || s.Description.Contains(search));
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "name" => descending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                    "description" => descending ? query.OrderByDescending(s => s.Description) : query.OrderBy(s => s.Description),
                    "establisheddate" => descending ? query.OrderByDescending(s => s.EstablishedDate) : query.OrderBy(s => s.EstablishedDate),
                    _ => query.OrderBy(s => s.Name), // Default sorting by Name
                };
            } else
            {
                query = query.OrderBy(s => s.Name); // Default sorting by Name
            }

            var totalCount = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<Specialty>(items, totalCount);
        }

        public async Task AddAsync(Specialty specialty)
        {
            _specialties.Add(specialty);
        }

        public async Task UpdateAsync(Specialty specialty)
        {
            return;
        }

        /// <summary>
        /// Prepares the fake repository with sample data of 5 specialties for testing purposes.
        /// </summary>
        /// <returns></returns>
        public void PrepareData()
        {
            _specialties.Clear();
            _specialties.Add(new Domain.Entities.Specialty("Cardiology", "Heart specialist", new DateOnly(1980, 1, 1)));
            _specialties.Add(new Domain.Entities.Specialty("Dermatology", "Skin specialist", new DateOnly(2010, 1, 1)));
            _specialties.Add(new Domain.Entities.Specialty("Oncology", "Cancer specialist", new DateOnly(2020, 1, 1)));
            _specialties.Add(new Domain.Entities.Specialty("Neurology", "Brain specialist", new DateOnly(1990, 1, 1)));
            _specialties.Add(new Domain.Entities.Specialty("Pediatrics", "Child specialist", new DateOnly(2000, 1, 1)));
        }
    }
}

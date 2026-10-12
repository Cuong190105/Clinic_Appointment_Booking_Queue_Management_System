using Clinic.Application.Common.Models;
using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface ISpecialtyRepository
    {
        /// <summary>
        /// Gets a <see cref="Specialty"/> entity by its unique identifier asynchronously.
        /// </summary>
        Task<Specialty?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets a <see cref="Specialty"/> entity by its unique name asynchronously.
        /// </summary>
        Task<Specialty?> GetByNameAsync(string name);

        /// <summary>
        /// Gets all <see cref="Specialty"/> entities asynchronously.
        /// </summary>
        Task<IEnumerable<Specialty>> GetAllAsync();

        /// <summary>
        /// Gets a paginated list of <see cref="Specialty"/> entities based on search criteria, sorting, and pagination parameters asynchronously.
        /// </summary>
        Task<PagedResult<Specialty>> GetPagedAsync(string? search, string sortBy, bool descending, int page, int pageSize);

        /// <summary>
        /// Adds a new <see cref="Specialty"/> entity to the repository asynchronously.
        /// </summary>
        Task AddAsync(Specialty specialty);

        /// <summary>
        /// Updates an existing <see cref="Specialty"/> entity in the repository asynchronously.
        /// </summary>
        Task UpdateAsync(Specialty specialty);
    }
}

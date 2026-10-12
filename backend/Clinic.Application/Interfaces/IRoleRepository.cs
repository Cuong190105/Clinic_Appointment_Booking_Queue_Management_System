using Clinic.Domain.Entities;

namespace Clinic.Application.Interfaces
{
    public interface IRoleRepository
    {
        /// <summary>
        /// Gets a role by its name asynchronously.
        /// </summary>
        Task<Role?> GetByNameAsync(string roleName);
    }
}
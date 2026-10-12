using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.Interfaces
{
    public interface IDoctorRepository
    {
        /// <summary>
        /// Adds a new doctor to the repository asynchronously.
        /// </summary>
        Task AddAsync(Doctor doctor);

        /// <summary>
        /// Retrieves a list of active doctors associated with a specific specialty asynchronously, including related entities Employee and Person for name retrieval.
        /// If the specialtyId is null, it retrieves all active doctors regardless of specialty.
        /// </summary>
        Task<List<Doctor>> GetActiveDoctorsBySpecialty(Guid? specialtyId);
        Task<Dictionary<string, IValueWithChange>> GetDashboardData();

        /// <summary>
        /// Retrieves personal information for a doctor by their unique identifier asynchronously. Includes related entities Employee, Person, User, and WorkHistories.
        /// </summary>
        Task<Doctor?> GetInfoByIdAsync(Guid? doctorId);

        /// <summary>
        /// Retrieves a paginated list of doctors based on the provided search term, sorting options, status filter, specialty filter, and pagination parameters asynchronously.
        /// </summary>
        Task<PagedResult<Doctor>> GetPagedAsync(string? searchTerm, string? sortBy, string? qualification, DoctorStatus? status, Guid? specialtyId, bool descending, int pageNumber, int pageSize);
    }
}
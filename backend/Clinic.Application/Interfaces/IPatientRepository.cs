using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface IPatientRepository
    {
        /// <summary>
        /// Adds a new patient to the repository.
        /// </summary>
        Task AddAsync(Patient patient);

        /// <summary>
        /// Get a patient by their unique identifier (ID). Returns null if the patient does not exist.
        /// </summary>
        Task<Patient?> GetByIdAsync(Guid? patientId);

        /// <summary>
        /// Gets a paginated list of patients based on the provided search criteria, sorting options, and pagination parameters.
        /// </summary>
        Task<PagedResult<Patient>> GetPagedAsync(string? search, string? sortBy, bool descending, int pageNumber, int pageSize);

        /// <summary>
        /// Gets a patient by their associated user ID. Used for retrieving patient information based on the their user account.
        /// </summary>
        Task<Patient?> GetPatientByUserIdAsync(Guid userId);

        /// <summary>
        /// Láº¥y Patient theo PersonId. DÃ¹ng Ä‘á»ƒ kiá»ƒm tra 1 Person Ä‘Ã£ cÃ³ há»“ sÆ¡ Patient hay chÆ°a
        /// trÆ°á»›c khi táº¡o má»›i (trÃ¡nh vi pháº¡m UNIQUE index PersonId).
        /// </summary>
        Task<Patient?> GetByPersonIdAsync(Guid personId);
        Task<Dictionary<string, IValueWithChange>> GetDashboardData();
    }
}

using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
namespace Clinic.Application.Interfaces
{
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Adds a new refresh token to the repository.
        /// </summary>
        Task AddAsync(RefreshToken refreshToken);

        /// <summary>
        /// Updates an existing refresh token in the repository.
        /// </summary>
        Task UpdateAsync(RefreshToken refreshToken);

        /// <summary>
        /// Retrieves a refresh token by its token hash. Include the associated user entity and its roles in the result.
        /// </summary>
        Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    }
}

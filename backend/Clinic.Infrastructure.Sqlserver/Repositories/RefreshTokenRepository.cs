using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _context;
        public RefreshTokenRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
        }
        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                    .ThenInclude(u => u.Person)
                        .ThenInclude(p => p.Patient)
                .Include(rt => rt.User)
                    .ThenInclude(u => u.Person)
                        .ThenInclude(p => p.Employee)
                            .ThenInclude(e => e!.Doctor)
                .Include(rt => rt.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.RolePermissions)
                                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {

            _context.RefreshTokens.Update(refreshToken);
        }
    }
}
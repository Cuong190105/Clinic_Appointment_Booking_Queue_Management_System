using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly ApplicationDbContext _context;
        public PatientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Patient?> GetByIdAsync(Guid? patientId)
        {
            return await _context.Patients.Include(p => p.Person).ThenInclude(p => p.User).FirstOrDefaultAsync(p => p.Id == patientId);
        }

        public async Task<PagedResult<Patient>> GetPagedAsync(string? search, string? sortBy, bool descending, int pageNumber, int pageSize)
        {
            var query = _context.Patients.Include(p => p.Person).ThenInclude(p => p.User).Where(p => !p.IsDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Person.FullName.Contains(search) || (p.Person.Email != null && p.Person.Email.Contains(search)) || (p.Person.PhoneNumber != null && p.Person.PhoneNumber.Contains(search)) || (p.InsuranceNumber != null && p.InsuranceNumber.Contains(search)));
            }

            if (string.IsNullOrEmpty(sortBy))
            {
                sortBy = "fullname"; // Default sorting by FullName
            }

            query = sortBy.ToLower() switch
            {
                "fullname" => descending ? query.OrderByDescending(p => p.Person.FullName) : query.OrderBy(p => p.Person.FullName),
                "email" => descending ? query.OrderByDescending(p => p.Person.Email) : query.OrderBy(p => p.Person.Email),
                "dateofbirth" => descending ? query.OrderByDescending(p => p.Person.DateOfBirth) : query.OrderBy(p => p.Person.DateOfBirth),
                _ => query.OrderBy(p => p.Person.FullName)
            };

            var totalCount = await query.CountAsync();
            var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync();
            return new PagedResult<Patient>(items, totalCount);
        }

        public async Task<Patient?> GetPatientByUserIdAsync(Guid userId)
        {
            return await _context.Patients.Include(p => p.Person).ThenInclude(p => p.User).FirstOrDefaultAsync(p => p.Person.User.Id == userId);
        }

        public async Task<Patient?> GetByPersonIdAsync(Guid personId)
        {
            return await _context.Patients.FirstOrDefaultAsync(p => p.PersonId == personId);
        }

        public async Task AddAsync(Patient patient)
        {
            await _context.Patients.AddAsync(patient);
        }

        public async Task<Dictionary<string, IValueWithChange>> GetDashboardData()
        {
            var now = DateTime.UtcNow;
            var currentPeriodStart = now.AddDays(-30);
            var previousPeriodStart = now.AddDays(-60);

            var stats = await _context.Patients
                .Where(p => !p.IsDeleted)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    CurrentTotal = g.Count(p => p.CreatedAt < now),
                    PreviousTotal = g.Count(p => p.CreatedAt < currentPeriodStart),
                })
                .FirstOrDefaultAsync();

            var currentTotal = stats?.CurrentTotal ?? 0;
            var previousTotal = stats?.PreviousTotal ?? 0;

            return new Dictionary<string, IValueWithChange>
            {
                ["TotalPatients"] = new ValueWithChange<int>
                {
                    Value = currentTotal,
                    Change = CalculatePercentChange(currentTotal, previousTotal)
                },
            };
        }

        private static double CalculatePercentChange(double currentValue, double previousValue)
        {
            if (previousValue == 0)
            {
                return currentValue == 0 ? 0 : 100;
            }

            return (currentValue - previousValue) / previousValue * 100;
        }
    }
}
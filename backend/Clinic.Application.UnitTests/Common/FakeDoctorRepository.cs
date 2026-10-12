using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeDoctorRepository : IDoctorRepository
    {
        private readonly List<Doctor> _doctors = [];
        public async Task AddAsync(Doctor doctor)
        {
            _doctors.Add(doctor);
        }

        public async Task<List<Doctor>> GetActiveDoctorsBySpecialty(Guid? specialtyId)
        {
            var query = _doctors.Where(d => d.Status == DoctorStatus.Active && d.IsDeleted == false);
            if (specialtyId != null)
            {
                query = query.Where(d => d.WorkHistories.Any(wh => wh.EndDate == null && wh.SpecialtyId == specialtyId));
            }
            return query.ToList();
        }

        public Task<Dictionary<string, IValueWithChange>> GetDashboardData()
        {
            throw new NotImplementedException();
        }

        public async Task<Doctor?> GetInfoByIdAsync(Guid? doctorId)
        {
            return _doctors.FirstOrDefault(d => d.Id == doctorId);
        }

        public async Task<PagedResult<Doctor>> GetPagedAsync(string? searchTerm, string? sortBy, string? qualification, DoctorStatus? status, Guid? specialtyId, bool descending, int pageNumber, int pageSize)
        {
            var query = _doctors
                .Where(d => d.IsDeleted == false);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(d => d.Employee.Person.FullName.Contains(searchTerm) ||
                    d.LicenseNumber.Contains(searchTerm) ||
                    d.Qualification.Contains(searchTerm) ||
                    (d.Biography != null && d.Biography.Contains(searchTerm)) ||
                    (d.WorkHistories.Any(wh => wh.EndDate == null && wh.Specialty.Name.Contains(searchTerm)))
                );
            }

            if (!string.IsNullOrEmpty(qualification))
            {
                query = query.Where(d => d.Qualification == qualification);
            }

            if (status != null)
            {
                query = query.Where(d => d.Status == status);
            }

            if (specialtyId != null)
            {
                query = query.Where(d => d.WorkHistories.Any(wh => wh.EndDate == null && wh.SpecialtyId == specialtyId));
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "name" => !descending ? query.OrderBy(d => d.Employee.Person.FullName) : query.OrderByDescending(d => d.Employee.Person.FullName),
                    "experienceyears" => !descending ? query.OrderBy(d => d.ExperienceYears) : query.OrderByDescending(d => d.ExperienceYears),
                    _ => !descending ? query.OrderBy(d => d.Employee.Person.FullName) : query.OrderByDescending(d => d.Employee.Person.FullName), // Default sorting by Name
                };
            } else
            {
                query = !descending ? query.OrderBy(d => d.Employee.Person.FullName) : query.OrderByDescending(d => d.Employee.Person.FullName); // Default sorting by Name
            }

            var totalCount = query.Count();

            var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<Doctor>(items, totalCount);
        }
    }
}

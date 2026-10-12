using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Common
{
    public class FakePatientRepository : IPatientRepository
    {
        private readonly List<Patient> _patients = [];

        public async Task<Patient?> GetByIdAsync(Guid? patientId)
        {
            return _patients.FirstOrDefault(p => p.Id == patientId);
        }

        public Task<PagedResult<Patient>> GetPagedAsync(string? search, string? sortBy, bool descending, int pageNumber, int pageSize)
        {
            throw new NotImplementedException();
        }

        public async Task<Patient?> GetPatientByUserIdAsync(Guid userId)
        {
            return _patients.FirstOrDefault(p => p.Person.User.Id == userId);
        }

        internal async Task AddAsync(Patient patient)
        {
            _patients.Add(patient);
        }

        Task IPatientRepository.AddAsync(Patient patient)
        {
            return AddAsync(patient);
        }

        public Task<Patient?> GetByPersonIdAsync(Guid personId)
        {
            return Task.FromResult(_patients.FirstOrDefault(p => p.PersonId == personId));
        }

        public Task<Dictionary<string, IValueWithChange>> GetDashboardData()
        {
            throw new NotImplementedException();
        }
    }
}

using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeEmployeeRepository : IEmployeeRepository
    {
        private readonly List<Employee> _employees = [];
        public async Task AddAsync(Employee employee)
        {
            _employees.Add(employee);
        }

        public async Task<PagedResult<Employee>> GetPagedAsync(string? search, string sortBy, bool descending, List<string>? roles, Gender? gender, EmployeeStatus? status, int page, int pageSize)
        {
            var query = _employees
                .Where(e => e.IsDeleted == false);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Person.FullName.Contains(search) ||
                    (s.Person.Address != null && s.Person.Address.Contains(search)));
            }

            if (gender != null)
            {
                query = query.Where(e => e.Person.Gender == gender);
            }
            if (status != null)
            {
                query = query.Where(e => e.Status == status);
            }
            if (roles != null && roles.Any())
            {
                query = query.Where(e => e.Person.User.UserRoles.Any(ur => roles.Contains(ur.Role.Name)));
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "fullname" => descending ? query.OrderByDescending(s => s.Person.FullName) : query.OrderBy(s => s.Person.FullName),
                    "dateofbirth" => descending ? query.OrderByDescending(s => s.Person.DateOfBirth) : query.OrderBy(s => s.Person.DateOfBirth),
                    _ => query.OrderBy(s => s.Person.FullName), // Default sorting by FullName
                };
            } else
            {
                query = query.OrderBy(s => s.Person.FullName); // Default sorting by FullName
            }

            var totalCount = query.Count();

            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<Employee>(items, totalCount);
        }

        public async Task<Employee?> GetByIdAsync(Guid employeeId)
        {
            return _employees.FirstOrDefault(e => e.Id == employeeId);
        }
    }
}

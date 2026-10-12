using Clinic.Application.Common.Models;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Interfaces
{
    public interface IEmployeeRepository
    {
        Task AddAsync(Employee employee);
        Task<Employee?> GetByIdAsync(Guid employeeId);
        Task<PagedResult<Employee>> GetPagedAsync(string? search, string sortBy, bool descending, List<string>? roles, Gender? gender, EmployeeStatus? status, int page, int pageSize);
    }
}

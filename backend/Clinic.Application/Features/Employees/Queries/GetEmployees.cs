using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Employees.Queries
{
    // Use-case: Get a list of employees with optional search, sorting, and pagination
    public record GetEmployeesQuery(string? Search = null,
        string SortBy = "fullName",
        bool Descending = false,
        List<string>? Roles = null,
        Gender? Gender = null,
        EmployeeStatus? Status = null,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<EmployeeSummaryDto>>;
    public class GetEmployeesHandler : IRequestHandler<GetEmployeesQuery, PaginationResponse<EmployeeSummaryDto>>
    {
        private readonly IEmployeeRepository _employeeRepository;
        public GetEmployeesHandler(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        public async Task<PaginationResponse<EmployeeSummaryDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
        {
            var employees = await _employeeRepository.GetPagedAsync(
                request.Search,
                request.SortBy,
                request.Descending,
                request.Roles,
                request.Gender,
                request.Status,
                request.PageNumber,
                request.PageSize);
            var employeeDtos = employees.Items.Select(e => new EmployeeSummaryDto
            {
                Id = e.Id,
                FullName = e.Person.FullName,
                PhoneNumber = e.Person.PhoneNumber,
                Email = e.Person.Email,
                Gender = e.Person.Gender,
                DateOfBirth = e.Person.DateOfBirth,
                Status = e.Status,
                Roles = e.Person.User.UserRoles.Select(r => r.Role.Name).ToList()
            }).ToList();
            return new PaginationResponse<EmployeeSummaryDto>
            {
                Items = employeeDtos,
                TotalCount = employees.TotalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}

using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Employees.Queries
{
    public record GetEmployeeByIdQuery(Guid EmployeeId) : IRequest<EmployeeDetailDto>;
    public class GetEmployeeByIdHandler : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
    {
        private readonly IEmployeeRepository _employeeRepository;
        public GetEmployeeByIdHandler(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        public async Task<EmployeeDetailDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }

            return new EmployeeDetailDto
            {
                Id = employee.Id,
                FullName = employee.Person.FullName,
                PhoneNumber = employee.Person.PhoneNumber,
                Email = employee.Person.Email,
                Gender = employee.Person.Gender,
                DateOfBirth = employee.Person.DateOfBirth,
                Address = employee.Person.Address,
                ManagerName = employee.Manager != null ? employee.Manager.Person.FullName : null,
                Status = employee.Status,
                Roles = employee.Person.User.UserRoles.Select(r => r.Role.Name).ToList()
            };
        }
    }
}

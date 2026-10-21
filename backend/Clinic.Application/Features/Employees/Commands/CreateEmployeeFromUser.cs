using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Employees.Commands
{
    // Use-case: Create a new employee from an existing user
    public record CreateEmployeeFromUserCommand(
        Guid UserId,
        DateOnly HireDate,
        List<string> Roles,
        string? Email = null,
        string? Address = null
    ) : IRequest<EmployeeSummaryDto>;
    public class CreateEmployeeFromUserCommandHandler : IRequestHandler<CreateEmployeeFromUserCommand, EmployeeSummaryDto>
    {
        private readonly IPersonRepository _personRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateEmployeeFromUserCommandHandler(
            IPersonRepository personRepository,
            IEmployeeRepository employeeRepository,
            IUnitOfWork unitOfWork)
        {
            _personRepository = personRepository;
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<EmployeeSummaryDto> Handle(CreateEmployeeFromUserCommand request, CancellationToken cancellationToken)
        {
            var person = await _personRepository.GetByIdentityUserIdAsync(request.UserId);
            if (person == null)
            {
                throw new ArgumentException($"User with ID '{request.UserId}' does not exist.");
            }
            if (string.IsNullOrWhiteSpace(person.Email) && string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Employee must have an email address.");
            }
            if (string.IsNullOrWhiteSpace(person.Address) && string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Employee must have an address.");
            }
            if (person.Employee != null)
            {
                throw new InvalidOperationException($"User with ID '{request.UserId}' is already an employee.");
            }
            if (request.Roles == null || request.Roles.Count == 0)
            {
                throw new ArgumentException("At least one role must be assigned to the employee.");
            }
            person.UpdateDetails(
                email: request.Email,
                gender: person.Gender,
                address: request.Address
            );
            var employee = new Employee(person, request.HireDate);

            // Account roles are owned by Identity. Clinic only creates the business profile.
            if (request.Roles.Any(role => string.Equals(role.Trim(), "Doctor", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Doctor role is not allowed in this endpoint. Use POST /doctors/from-user instead.");
            if (person.Patient == null)
                person.Patient = new Patient(person, null, null);
            await _employeeRepository.AddAsync(employee);
            await _unitOfWork.SaveChangesAsync();
            return new EmployeeSummaryDto
            {
                Id = employee.Id,
                PatientId = person.Patient?.Id,
                FullName = employee.Person.FullName,
                PhoneNumber = employee.Person.PhoneNumber,
                Email = employee.Person.Email,
                Gender = employee.Person.Gender,
                DateOfBirth = employee.Person.DateOfBirth,
                Status = employee.Status,
                Roles = request.Roles.Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };
        }
    }
}

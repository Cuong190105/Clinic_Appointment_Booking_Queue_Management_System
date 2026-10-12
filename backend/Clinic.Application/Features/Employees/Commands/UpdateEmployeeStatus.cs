using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Employees.Commands
{
    // Use-case: Update an existing employee
    public record UpdateEmployeeStatusCommand(Guid EmployeeId, EmployeeStatus Status) : IRequest<Guid>;
    public class UpdateEmployeeStatusCommandHandler : IRequestHandler<UpdateEmployeeStatusCommand, Guid>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateEmployeeStatusCommandHandler(IEmployeeRepository employeeRepository, IUnitOfWork unitOfWork)
        {
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(UpdateEmployeeStatusCommand request, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }
            // Update the employee's properties
            employee.UpdateStatus(request.Status);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return employee.Id;
        }
    }
}

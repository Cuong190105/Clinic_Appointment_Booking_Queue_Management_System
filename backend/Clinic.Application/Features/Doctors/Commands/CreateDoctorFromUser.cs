using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Create a new doctor from an existing user
    public record CreateDoctorFromUserCommand(
        Guid UserId,
        DateOnly HireDate,
        string LicenseNumber,
        string Qualification,
        string? Biography,
        int ExperienceYears,
        DoctorStatus Status,
        Guid SpecialtyId,
        string? Email = null,
        string? Address = null
    ) : IRequest<DoctorSummaryDto>;
    public class CreateDoctorFromUserCommandHandler : IRequestHandler<CreateDoctorFromUserCommand, DoctorSummaryDto>
    {
        private readonly IPersonRepository _personRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateDoctorFromUserCommandHandler(
            IPersonRepository personRepository,
            IEmployeeRepository employeeRepository,
            IDoctorRepository doctorRepository,
            ISpecialtyRepository specialtyRepository,
            IUnitOfWork unitOfWork)
        {
            _personRepository = personRepository;
            _employeeRepository = employeeRepository;
            _doctorRepository = doctorRepository;
            _specialtyRepository = specialtyRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<DoctorSummaryDto> Handle(CreateDoctorFromUserCommand request, CancellationToken cancellationToken)
        {
            var person = await _personRepository.GetByIdentityUserIdAsync(request.UserId);
            if (person == null)
            {
                throw new ArgumentException($"User with ID '{request.UserId}' does not exist.");
            }
            if (person.Employee?.Doctor != null)
            {
                throw new InvalidOperationException($"User with ID '{request.UserId}' is already a doctor.");
            }
            if (string.IsNullOrWhiteSpace(person.Email) && string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Employee must have an email address.");
            }
            if (string.IsNullOrWhiteSpace(person.Address) && string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Employee must have an address.");
            }
            var specialty = await _specialtyRepository.GetByIdAsync(request.SpecialtyId);
            if (specialty == null)
            {
                throw new ArgumentException($"Specialty with ID '{request.SpecialtyId}' does not exist.");
            }
            // Doctor role ownership and assignment are handled by Identity Service.
            person.UpdateDetails(
                email: string.IsNullOrWhiteSpace(request.Email) ? person.Email : request.Email,
                gender: person.Gender,
                address: string.IsNullOrWhiteSpace(request.Address) ? person.Address : request.Address
            );
            var employee = person.Employee;
            if (employee == null)
            {
                employee = new Employee(person, request.HireDate);
                await _employeeRepository.AddAsync(employee);
            }
            var doctor = new Doctor(employee, request.LicenseNumber, request.Qualification, specialty, request.ExperienceYears, request.Biography, request.Status);
            if (person.Patient == null)
            {
                var patient = new Patient(person, null, null);
                // Patient role assignment is handled by Identity Service.
                person.Patient = patient;
            }
            await _doctorRepository.AddAsync(doctor);
            await _unitOfWork.SaveChangesAsync();
            return new DoctorSummaryDto
            {
                Id = doctor.Id,
                EmployeeId = employee.Id,
                PatientId = person.Patient?.Id,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber,
                Email = person.Email,
                Gender = person.Gender,
                LicenseNumber = request.LicenseNumber
            };
        }
    }
}

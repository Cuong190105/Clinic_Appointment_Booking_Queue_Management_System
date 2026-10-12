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
        private readonly IUserRepository _userRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateDoctorFromUserCommandHandler(
            IUserRepository userRepository,
            IEmployeeRepository employeeRepository,
            IDoctorRepository doctorRepository,
            ISpecialtyRepository specialtyRepository,
            IRoleRepository roleRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _employeeRepository = employeeRepository;
            _doctorRepository = doctorRepository;
            _specialtyRepository = specialtyRepository;
            _roleRepository = roleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<DoctorSummaryDto> Handle(CreateDoctorFromUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID '{request.UserId}' does not exist.");
            }
            if (user.Person.Employee?.Doctor != null)
            {
                throw new InvalidOperationException($"User with ID '{request.UserId}' is already a doctor.");
            }
            if (string.IsNullOrWhiteSpace(user.Person.Email) && string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Employee must have an email address.");
            }
            if (string.IsNullOrWhiteSpace(user.Person.Address) && string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Employee must have an address.");
            }
            var specialty = await _specialtyRepository.GetByIdAsync(request.SpecialtyId);
            if (specialty == null)
            {
                throw new ArgumentException($"Specialty with ID '{request.SpecialtyId}' does not exist.");
            }
            var role = await _roleRepository.GetByNameAsync("Doctor");
            if (role == null)
            {
                throw new ArgumentException("Role 'Doctor' does not exist");
            }
            user.AssignRole(role);
            user.Person.UpdateDetails(
                email: string.IsNullOrWhiteSpace(request.Email) ? user.Person.Email : request.Email,
                gender: user.Person.Gender,
                address: string.IsNullOrWhiteSpace(request.Address) ? user.Person.Address : request.Address
            );
            var employee = user.Person.Employee;
            if (employee == null)
            {
                employee = new Employee(user.Person, request.HireDate);
                await _employeeRepository.AddAsync(employee);
            }
            var doctor = new Doctor(employee, request.LicenseNumber, request.Qualification, specialty, request.ExperienceYears, request.Biography, request.Status);
            if (user.Person.Patient == null)
            {
                var patient = new Patient(user.Person, null, null);
                var patientRole = await _roleRepository.GetByNameAsync("Patient");
                if (patientRole == null)
                {
                    throw new ArgumentException("Role 'Patient' does not exist");
                }
                user.AssignRole(patientRole);
                user.Person.Patient = patient;
            }
            await _doctorRepository.AddAsync(doctor);
            await _unitOfWork.SaveChangesAsync();
            return new DoctorSummaryDto
            {
                Id = doctor.Id,
                FullName = user.Person.FullName,
                PhoneNumber = user.Person.PhoneNumber,
                Email = user.Person.Email,
                Gender = user.Person.Gender,
                LicenseNumber = request.LicenseNumber
            };
        }
    }
}

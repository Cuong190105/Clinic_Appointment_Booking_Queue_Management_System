using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace Clinic.Application.Features.Patients.Commands
{
    public record CreatePatientCommand(
        string FullName,
        string? PhoneNumber,
        string? Email,
        DateOnly DateOfBirth,
        Gender Gender,
        string Address,
        string? InsuranceNumber,
        string? EmergencyContact
        ) : IRequest<PatientDto>;
    public class CreatePatientCommandHandler : IRequestHandler<CreatePatientCommand, PatientDto>
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IPersonService _personService;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePatientCommandHandler(IPatientRepository patientRepository, IPersonService personService, IUnitOfWork unitOfWork)
        {
            _patientRepository = patientRepository;
            _personService = personService;
            _unitOfWork = unitOfWork;
        }
        public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken cancellationToken)
        {
            var person = await _personService.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address);
            if (person.Patient != null)
            {
                throw new ValidationException("Patient already exists.");
            }
            var patient = new Patient(person, request.InsuranceNumber, request.EmergencyContact);
            await _patientRepository.AddAsync(patient);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PatientDto
            {
                Id = patient.Id,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber,
                Email = person.Email,
                Address = person.Address,
                DateOfBirth = person.DateOfBirth,
                Gender = person.Gender,
                InsuranceNumber = patient.InsuranceNumber,
                EmergencyContact = patient.EmergencyContact
            };
        }
    }
}

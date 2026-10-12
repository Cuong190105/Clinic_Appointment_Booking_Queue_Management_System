using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Patients.Queries
{
    public record GetPatientByIdQuery(Guid PatientId) : IRequest<PatientDto?>;
    public class GetPatientByIdQueryHandler : IRequestHandler<GetPatientByIdQuery, PatientDto?>
    {
        private readonly IPatientRepository _patientRepository;

        public GetPatientByIdQueryHandler(IPatientRepository patientRepository)
        {
            _patientRepository = patientRepository;
        }

        public async Task<PatientDto?> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
        {
            var patient = await _patientRepository.GetByIdAsync(request.PatientId);
            if (patient == null)
            {
                throw new NotFoundException("Patient not found.");
            }
            return new PatientDto
            {
                Id = patient.Id,
                FullName = patient.Person.FullName,
                PhoneNumber = patient.Person.PhoneNumber,
                Email = patient.Person.Email,
                Address = patient.Person.Address,
                DateOfBirth = patient.Person.DateOfBirth,
                Gender = patient.Person.Gender,
                InsuranceNumber = patient.InsuranceNumber,
                EmergencyContact = patient.EmergencyContact
            };
        }
    }
}

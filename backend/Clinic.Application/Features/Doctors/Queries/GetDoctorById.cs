using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Doctors.Queries
{
    public record GetDoctorQuery(Guid DoctorId) : IRequest<DoctorDetailDto>;
    public class GetDoctorHandler : IRequestHandler<GetDoctorQuery, DoctorDetailDto>
    {
        private readonly IDoctorRepository _doctorRepository;
        public GetDoctorHandler(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
        }
        public async Task<DoctorDetailDto> Handle(GetDoctorQuery request, CancellationToken cancellationToken)
        {
            var doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null) throw new NotFoundException("Doctor not found");

            return new DoctorDetailDto
            {
                Id = doctor.Id,
                FullName = doctor.Employee.Person.FullName,
                PhoneNumber = doctor.Employee.Person.PhoneNumber,
                Email = doctor.Employee.Person.Email,
                Gender = doctor.Employee.Person.Gender,
                DateOfBirth = doctor.Employee.Person.DateOfBirth,
                Address = doctor.Employee.Person.Address,
                HireDate = doctor.Employee.HireDate,
                LicenseNumber = doctor.LicenseNumber,
                Qualification = doctor.Qualification,
                CurrentSpecialty = doctor.WorkHistories.FirstOrDefault(wh => wh.EndDate == null)?.Specialty?.Name ?? "No specialty",
                ExperienceYears = doctor.ExperienceYears,
                Biography = doctor.Biography,
            };
        }
    }
}

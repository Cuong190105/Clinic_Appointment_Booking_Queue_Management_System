using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Doctors.Queries
{
    // Use-case: Patient gets a list of doctors available for booking
    public record GetDoctorsForBookingQuery(
        Guid? SpecialtyId = null
    ) : IRequest<List<BookingDoctorDto>>;
    public class GetDoctorsForBookingQueryHandler : IRequestHandler<GetDoctorsForBookingQuery, List<BookingDoctorDto>>
    {
        private readonly IDoctorRepository _doctorRepository;
        public GetDoctorsForBookingQueryHandler(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
        }
        public async Task<List<BookingDoctorDto>> Handle(GetDoctorsForBookingQuery request, CancellationToken cancellationToken)
        {
            var doctors = await _doctorRepository.GetActiveDoctorsBySpecialty(request.SpecialtyId);
            var bookingDoctors = doctors.Select(d => new BookingDoctorDto
            {
                Id = d.Id,
                FullName = d.Employee.Person.FullName,
                Qualification = d.Qualification,
                ExperienceYears = d.ExperienceYears
            }).ToList();
            return bookingDoctors;
        }
    }
}

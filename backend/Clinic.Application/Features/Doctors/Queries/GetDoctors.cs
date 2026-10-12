using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Doctors.Queries
{
    public record GetDoctorsQuery(
        string? Search = null,

        string? SortBy = "fullName",
        string? Qualification = null,
        DoctorStatus? Status = null,
        Guid? SpecialtyId = null,
        bool Descending = false,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<DoctorSummaryDto>>;
    public class GetDoctorsHandler : IRequestHandler<GetDoctorsQuery, PaginationResponse<DoctorSummaryDto>>
    {
        private readonly IDoctorRepository _doctorRepository;
        public GetDoctorsHandler(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
        }

        public async Task<PaginationResponse<DoctorSummaryDto>> Handle(GetDoctorsQuery request, CancellationToken cancellationToken)
        {
            var doctors = await _doctorRepository.GetPagedAsync(
                request.Search,
                request.SortBy,
                request.Qualification,
                request.Status,
                request.SpecialtyId,
                request.Descending,
                request.PageNumber,
                request.PageSize
            );
            var doctorDtos = doctors.Items.Select(d => new DoctorSummaryDto
            {
                Id = d.Id,
                FullName = d.Employee.Person.FullName,
                PhoneNumber = d.Employee.Person.PhoneNumber,
                Email = d.Employee.Person.Email,
                Gender = d.Employee.Person.Gender,
                LicenseNumber = d.LicenseNumber,
                Qualification = d.Qualification,
                CurrentSpecialty = d.WorkHistories.FirstOrDefault(wh => wh.EndDate == null)?.Specialty?.Name ?? "No specialty",
                ExperienceYears = d.ExperienceYears,
                Status = d.Status,
            }).ToList();
            return new PaginationResponse<DoctorSummaryDto>
            {
                Items = doctorDtos,
                TotalCount = doctors.TotalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }

}
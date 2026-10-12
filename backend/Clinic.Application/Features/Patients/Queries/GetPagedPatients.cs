using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;


namespace Clinic.Application.Features.Patients.Queries
{
    public record GetPagedPatientsQuery(
        string? Search = null,
        string? SortBy = "fullName",
        bool? Descending = false,
        int PageNumber = 1,
        int PageSize = 10) : IRequest<PaginationResponse<PatientDto>>;
    public class GetPagedPatientsQueryHandler : IRequestHandler<GetPagedPatientsQuery, PaginationResponse<PatientDto>>
    {
        private readonly IPatientRepository _patientRepository;
        public GetPagedPatientsQueryHandler(IPatientRepository patientRepository)
        {
            _patientRepository = patientRepository;
        }
        public async Task<PaginationResponse<PatientDto>> Handle(GetPagedPatientsQuery request, CancellationToken cancellationToken)
        {
            var patients = await _patientRepository.GetPagedAsync(request.Search, request.SortBy, request.Descending ?? false, request.PageNumber, request.PageSize);
            return new PaginationResponse<PatientDto> (
                items: patients.Items.Select(p => new PatientDto
                {
                    Id = p.Id,
                    FullName = p.Person.FullName,
                    PhoneNumber = p.Person.PhoneNumber,
                    Email = p.Person.Email,
                    Address = p.Person.Address,
                    DateOfBirth = p.Person.DateOfBirth,
                    Gender = p.Person.Gender,
                }).ToList(),
                totalCount: patients.TotalCount,
                pageNumber: request.PageNumber,
                pageSize: request.PageSize
            );
        }
    }
}

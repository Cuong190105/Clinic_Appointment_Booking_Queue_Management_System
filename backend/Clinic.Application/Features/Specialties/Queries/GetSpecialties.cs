using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Specialties.Queries
{
    // Use-case: Get a list of specialties with optional search, sorting, and pagination
    public record GetSpecialtiesQuery(string? Search = null,
        string SortBy = "name",
        bool Descending = false,
        int Page = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<SpecialtyDto>>;
    public class GetSpecialtiesQueryHandler : IRequestHandler<GetSpecialtiesQuery, PaginationResponse<SpecialtyDto>>
    {
        private readonly ISpecialtyRepository _specialtyRepository;
        public GetSpecialtiesQueryHandler(ISpecialtyRepository specialtyRepository)
        {
            _specialtyRepository = specialtyRepository;
        }

        public async Task<PaginationResponse<SpecialtyDto>> Handle(GetSpecialtiesQuery request, CancellationToken cancellationToken)
        {
            var specialties = await _specialtyRepository.GetPagedAsync(request.Search, request.SortBy, request.Descending, request.Page, request.PageSize);
            var specialtyDtos = specialties.Items.Select(s => new SpecialtyDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                EstablishedDate = s.EstablishedDate,
            }).ToList();
            return new PaginationResponse<SpecialtyDto>
            {
                Items = specialtyDtos,
                TotalCount = specialties.TotalCount,
                PageNumber = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}

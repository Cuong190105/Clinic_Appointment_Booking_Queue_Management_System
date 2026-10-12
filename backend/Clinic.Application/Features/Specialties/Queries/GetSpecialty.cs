using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Specialties.Queries
{
    public record GetSpecialtyQuery(Guid Id) : IRequest<SpecialtyDto?>;
    public class GetSpecialtyQueryHandler : IRequestHandler<GetSpecialtyQuery, SpecialtyDto?>
    {
        private readonly ISpecialtyRepository _specialtyRepository;
        public GetSpecialtyQueryHandler(ISpecialtyRepository specialtyRepository)
        {
            _specialtyRepository = specialtyRepository;
        }
        public async Task<SpecialtyDto?> Handle(GetSpecialtyQuery request, CancellationToken cancellationToken)
        {
            var specialty = await _specialtyRepository.GetByIdAsync(request.Id);
            if (specialty == null)
            {
                throw new NotFoundException("Specialty not found");
            }
            return new SpecialtyDto
            {
                Id = specialty.Id,
                Name = specialty.Name,
                Description = specialty.Description,
                EstablishedDate = specialty.EstablishedDate,
            };
        }
    }
}

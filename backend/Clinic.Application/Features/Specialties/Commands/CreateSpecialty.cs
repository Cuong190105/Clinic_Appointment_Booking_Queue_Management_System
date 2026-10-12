using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.Specialties.Commands
{
    // Use-case: Create a new specialty
    public record CreateSpecialtyCommand(string Name, string Description, DateOnly EstablishedDate) : IRequest<SpecialtyDto>;
    public class CreateSpecialtyCommandHandler : IRequestHandler<CreateSpecialtyCommand, SpecialtyDto>
    {
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IUnitOfWork _unitOfWork;
        public CreateSpecialtyCommandHandler(ISpecialtyRepository specialtyRepository, IUnitOfWork unitOfWork)
        {
            _specialtyRepository = specialtyRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<SpecialtyDto> Handle(CreateSpecialtyCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Specialty name cannot be empty.");
            }
            if (await _specialtyRepository.GetByNameAsync(request.Name) is not null)
            {
                throw new ArgumentException($"Specialty {request.Name} already exists.");
            }
            var specialty = new Specialty(request.Name, request.Description, request.EstablishedDate);
            await _specialtyRepository.AddAsync(specialty);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SpecialtyDto
            {
                Id = specialty.Id,
                Name = specialty.Name,
                Description = specialty.Description,
                EstablishedDate = specialty.EstablishedDate
            };
        }
    }
}

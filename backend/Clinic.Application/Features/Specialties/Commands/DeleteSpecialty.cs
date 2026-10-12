using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Specialties.Commands
{
    // Use-case: Delete a specialty
    public record DeleteSpecialtyCommand(Guid Id) : IRequest<Guid>;
    public class DeleteSpecialtyCommandHandler : IRequestHandler<DeleteSpecialtyCommand, Guid>
    {
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IUnitOfWork _unitOfWork;
        public DeleteSpecialtyCommandHandler(ISpecialtyRepository specialtyRepository, IUnitOfWork unitOfWork)
        {
            _specialtyRepository = specialtyRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(DeleteSpecialtyCommand request, CancellationToken cancellationToken)
        {
            var specialty = await _specialtyRepository.GetByIdAsync(request.Id);
            if (specialty == null)
            {
                throw new NotFoundException("Specialty not found.");
            }
            specialty.Delete();
            await _specialtyRepository.UpdateAsync(specialty);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return specialty.Id;
        }
    }
}

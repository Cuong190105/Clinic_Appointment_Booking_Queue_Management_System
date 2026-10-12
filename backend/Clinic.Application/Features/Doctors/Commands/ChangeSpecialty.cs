using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Change the specialty of an existing doctor
    public record ChangeSpecialtyCommand(
        Guid DoctorId,
        Guid NewSpecialtyId) : IRequest<Guid>;
    public class ChangeSpecialtyCommandHandler : IRequestHandler<ChangeSpecialtyCommand, Guid>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly ISpecialtyRepository _specialtyRepository;
        private readonly IUnitOfWork _unitOfWork;
        public ChangeSpecialtyCommandHandler(IDoctorRepository doctorRepository, ISpecialtyRepository specialtyRepository, IUnitOfWork unitOfWork)
        {
            _doctorRepository = doctorRepository;
            _specialtyRepository = specialtyRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(ChangeSpecialtyCommand request, CancellationToken cancellationToken)
        {
            var doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new ArgumentException("Doctor not found.");
            }
            var newSpecialty = await _specialtyRepository.GetByIdAsync(request.NewSpecialtyId);
            if (newSpecialty == null)
            {
                throw new ArgumentException("Specialty not found.");
            }
            doctor.ChangeSpecialty(newSpecialty);
            await _unitOfWork.SaveChangesAsync();
            return doctor.Id; 
        }
    }
}

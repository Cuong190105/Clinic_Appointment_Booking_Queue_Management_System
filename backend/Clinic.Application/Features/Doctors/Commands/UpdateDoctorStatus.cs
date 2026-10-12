using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Doctors.Commands
{
    // Use-case: Update an existing doctor
    public record UpdateDoctorStatusCommand(Guid DoctorId, DoctorStatus Status) : IRequest<Guid>;
    public class UpdateDoctorStatusCommandHandler : IRequestHandler<UpdateDoctorStatusCommand, Guid>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateDoctorStatusCommandHandler(IDoctorRepository doctorRepository, IUnitOfWork unitOfWork)
        {
            _doctorRepository = doctorRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(UpdateDoctorStatusCommand request, CancellationToken cancellationToken)
        {
            var doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new NotFoundException("Doctor not found.");
            }
            // Update the doctor's properties
            doctor.UpdateStatus(request.Status);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return doctor.Id;
        }
    }
}

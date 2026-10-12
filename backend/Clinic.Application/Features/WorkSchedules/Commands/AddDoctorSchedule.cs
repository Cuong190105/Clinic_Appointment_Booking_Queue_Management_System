using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Commands
{
    // Use-case: Admin adds a new work schedule for a doctor
    public record AddDoctorScheduleCommand(Guid DoctorId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, int PatientLimit) : IRequest<WorkScheduleDto>;

    public class AddDoctorScheduleCommandHandler : IRequestHandler<AddDoctorScheduleCommand, WorkScheduleDto>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public AddDoctorScheduleCommandHandler(IDoctorRepository doctorRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _doctorRepository = doctorRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<WorkScheduleDto> Handle(AddDoctorScheduleCommand request, CancellationToken cancellationToken)
        {
            var doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new NotFoundException("Doctor not found.");
            }
            if (await _workScheduleRepository.HasOverlappingWorkSchedule(request.DoctorId, request.Date, request.StartTime, request.EndTime))
            {
                throw new InvalidOperationException("The doctor has an overlapping work schedule in the specified time range.");
            }
            var workSchedule = new WorkSchedule
            (
                doctor: doctor,
                date: request.Date,
                shiftStart: request.StartTime,
                shiftEnd: request.EndTime,
                patientLimit: request.PatientLimit
            );
            doctor.AddWorkSchedule(workSchedule);
            await _workScheduleRepository.AddWorkScheduleAsync(workSchedule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new WorkScheduleDto
            {
                Id = workSchedule.Id,
                DoctorId = doctor.Id,
                Date = request.Date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                PatientLimit = request.PatientLimit,
                Status = workSchedule.Status
            };
        }
    }
}

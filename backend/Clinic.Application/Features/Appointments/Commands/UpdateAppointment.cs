using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.Appointments.Commands
{
    // Use-case: Patient or Receptionist updates an existing appointment
    public record UpdateAppointmentCommand(
        Guid UserId,
        Guid AppointmentId,
        Guid NewWorkScheduleId,
        TimeOnly TimeSlot,
        string Reason
    ) : IRequest<AppointmentDto>;
    public class UpdateAppointmentCommandHandler : IRequestHandler<UpdateAppointmentCommand, AppointmentDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUnitOfWork _unitOfWork;
        public UpdateAppointmentCommandHandler(IAppointmentRepository appointmentRepository, IUserRepository userRepository, IWorkScheduleRepository workScheduleRepository, IUnitOfWork unitOfWork)
        {
            _appointmentRepository = appointmentRepository;
            _userRepository = userRepository;
            _workScheduleRepository = workScheduleRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<AppointmentDto> Handle(UpdateAppointmentCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException($"User not found.");
            }
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException($"Appointment not found.");
            }
            if (!(user.UserRoles.Any(ur => ur.Role.Name == "Receptionist") || appointment.PatientId == user.Person.Patient?.Id))
            {
                throw new ForbiddenException($"You are not authorized to update this appointment.");
            }
            // Begin transaction
            await _unitOfWork.InitializeTransactionLockAsync(cancellationToken);
            try
            {
                var workSchedule = await _workScheduleRepository.GetWorkScheduleByIdAsync(request.NewWorkScheduleId);
                if (workSchedule == null)
                {
                    throw new NotFoundException($"Work schedule not found.");
                }
                appointment.Update(workSchedule, request.TimeSlot, request.Reason, request.UserId);
                workSchedule.AddAppointment(appointment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return new AppointmentDto
                {
                    Id = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = workSchedule.DoctorId,
                    PatientName = appointment.Patient.Person.FullName,
                    DoctorName = workSchedule.Doctor.Employee.Person.FullName,
                    TimeSlot = request.TimeSlot,
                    Date = workSchedule.Date,
                    Status = appointment.Status,
                    CreatedAt = appointment.CreatedAt,
                    Reason = request.Reason
                };
            }
            catch (Exception ex)
            {
                // For unique constraint violations as 2 appointments cannot be created for the same time slot
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                if (ex is ConflictException)
                {
                    throw new ConflictException("An appointment already exists for this time slot.");
                }
                throw;
            }
        }
    }
}

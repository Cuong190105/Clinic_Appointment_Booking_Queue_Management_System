using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using Clinic.Domain.Common.Exceptions;

namespace Clinic.Application.Features.Appointments.Commands
{
    // Use-case: Patient or Receptionist creates an appointment
    public record CreateAppointmentCommand(
        Guid? UserId,
        Guid WorkScheduleId,
        TimeOnly TimeSlot,
        string Reason,
        bool IsWalkIn,
        Guid? PatientId = null
    ) : IRequest<AppointmentDto>;
    public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, AppointmentDto>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationQueue _notificationQueue;
        public CreateAppointmentCommandHandler(
            IWorkScheduleRepository workScheduleRepository,
            IPatientRepository patientRepository,
            IAppointmentRepository appointmentRepository,
            IUnitOfWork unitOfWork,
            INotificationQueue notificationQueue)
        {
            _workScheduleRepository = workScheduleRepository;
            _patientRepository = patientRepository;
            _appointmentRepository = appointmentRepository;
            _unitOfWork = unitOfWork;
            _notificationQueue = notificationQueue;
        }
        public async Task<AppointmentDto> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
        {
            if (request.UserId == null)
            {
                throw new UnauthorizedAccessException();
            }
            var patient = request.PatientId == null ? await _patientRepository.GetPatientByUserIdAsync((Guid)request.UserId) : await _patientRepository.GetByIdAsync(request.PatientId);
            if (patient == null) {
                throw new NotFoundException($"Patient not found.");
            }
            // Begin transaction lock to prevent race conditions (active appointment count > patientLimit) when creating appointments
            await _unitOfWork.InitializeTransactionLockAsync(cancellationToken);
            try
            {
                var workSchedule = await _workScheduleRepository.GetByIdAsync(request.WorkScheduleId);
                if (workSchedule == null)
                {
                    throw new NotFoundException($"Work schedule not found.");
                }
                var appointment = new Appointment(patient, workSchedule, request.TimeSlot, request.Reason, (Guid)request.UserId, request.IsWalkIn);
                workSchedule.AddAppointment(appointment);
                await _appointmentRepository.AddAsync(appointment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return new AppointmentDto
                {
                    Id = appointment.Id,
                    PatientId = patient.Id,
                    DoctorId = workSchedule.DoctorId,
                    PatientName = patient.Person.FullName,
                    DoctorName = workSchedule.Doctor.Employee.Person.FullName,
                    TimeSlot = request.TimeSlot,
                    Reason = request.Reason,
                    Date = workSchedule.Date,
                    Status = appointment.Status,
                    CreatedAt = appointment.CreatedAt
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

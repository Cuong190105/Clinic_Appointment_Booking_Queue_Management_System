using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Appointments.Commands
{
    // Use-case: Patient or Receptionist creates an appointment
    public record CreateAppointmentCommand(
        Guid? UserId,
        Guid WorkScheduleId,
        TimeOnly TimeSlot,
        string Reason,
        bool IsWalkIn,
        Guid? PatientId = null,
        string? IdempotencyKey = null
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

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _appointmentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existing != null)
                {
                    return new AppointmentDto
                    {
                        Id = existing.Id,
                        PatientId = existing.PatientId,
                        DoctorId = existing.WorkSchedule?.DoctorId ?? Guid.Empty,
                        PatientName = existing.Patient?.Person?.FullName ?? string.Empty,
                        DoctorName = existing.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                        TimeSlot = existing.TimeSlot,
                        Reason = existing.Reason,
                        Date = existing.WorkSchedule?.Date ?? default,
                        Status = existing.Status,
                        CreatedAt = existing.CreatedAt
                    };
                }
            }

            var patient = request.PatientId == null
                ? await _patientRepository.GetPatientByUserIdAsync((Guid)request.UserId)
                : await _patientRepository.GetByIdAsync(request.PatientId);

            if (patient == null)
            {
                throw new NotFoundException($"Patient not found.");
            }

            await _unitOfWork.InitializeTransactionLockAsync(cancellationToken);
            try
            {
                // Lock the work schedule row to serialize concurrent slot bookings without table-level range locks
                await _workScheduleRepository.LockAsync(request.WorkScheduleId, cancellationToken);

                var workSchedule = await _workScheduleRepository.GetByIdAsync(request.WorkScheduleId);
                if (workSchedule == null)
                {
                    throw new NotFoundException($"Work schedule not found.");
                }

                var appointment = new Appointment(patient, workSchedule, request.TimeSlot, request.Reason, (Guid)request.UserId, request.IsWalkIn)
                {
                    IdempotencyKey = request.IdempotencyKey
                };

                // Online/walk-in appointment creation in monolith is immediately confirmed
                appointment.Confirm((Guid)request.UserId);

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
                    DoctorName = workSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    TimeSlot = request.TimeSlot,
                    Reason = request.Reason,
                    Date = workSchedule.Date,
                    Status = appointment.Status,
                    CreatedAt = appointment.CreatedAt
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                if (ex is ConflictException)
                {
                    throw;
                }
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                    (ex.InnerException?.Message.Contains("IdempotencyKey") == true ||
                     ex.Message.Contains("IdempotencyKey")))
                {
                    var existing = await _appointmentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                    if (existing != null)
                    {
                        return new AppointmentDto
                        {
                            Id = existing.Id,
                            PatientId = existing.PatientId,
                            DoctorId = existing.WorkSchedule?.DoctorId ?? Guid.Empty,
                            PatientName = existing.Patient?.Person?.FullName ?? string.Empty,
                            DoctorName = existing.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                            TimeSlot = existing.TimeSlot,
                            Reason = existing.Reason,
                            Date = existing.WorkSchedule?.Date ?? default,
                            Status = existing.Status,
                            CreatedAt = existing.CreatedAt
                        };
                    }
                }
                if (ex.InnerException?.Message.Contains("IX_Appointments_TimeSlot_WorkScheduleId") == true ||
                    ex.Message.Contains("IX_Appointments_TimeSlot_WorkScheduleId"))
                {
                    throw new ConflictException("An appointment already exists for this time slot.");
                }
                throw;
            }
        }
    }
}

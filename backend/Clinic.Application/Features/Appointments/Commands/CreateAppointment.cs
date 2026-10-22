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
        private readonly ISchedulingServiceClient? _schedulingClient;

        public CreateAppointmentCommandHandler(
            IWorkScheduleRepository workScheduleRepository,
            IPatientRepository patientRepository,
            IAppointmentRepository appointmentRepository,
            IUnitOfWork unitOfWork,
            INotificationQueue notificationQueue,
            ISchedulingServiceClient? schedulingClient = null)
        {
            _workScheduleRepository = workScheduleRepository;
            _patientRepository = patientRepository;
            _appointmentRepository = appointmentRepository;
            _unitOfWork = unitOfWork;
            _notificationQueue = notificationQueue;
            _schedulingClient = schedulingClient;
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

            if (_schedulingClient != null)
            {
                // MICROSERVICE FLOW
                if (!request.IsWalkIn)
                {
                    // Case 1: Online Booking (IsWalkIn = false)
                    // 1. ReserveSlot with IdempotencyKey
                    var idempotencyKey = request.IdempotencyKey ?? $"booking-{patient.Id}-{request.WorkScheduleId}-{request.TimeSlot}";
                    var reservation = await _schedulingClient.ReserveSlotAsync(
                        request.WorkScheduleId,
                        request.TimeSlot,
                        patient.Id,
                        idempotencyKey,
                        cancellationToken);

                    if (reservation == null)
                    {
                        throw new ConflictException("Could not reserve slot.");
                    }

                    // 2. Create Appointment (Pending)
                    var appointment = new Appointment(
                        patient,
                        request.WorkScheduleId,
                        reservation.DoctorId,
                        reservation.DoctorName ?? string.Empty,
                        reservation.SpecialtyName,
                        reservation.ScheduleDate,
                        request.TimeSlot,
                        request.Reason,
                        (Guid)request.UserId,
                        false);

                    try
                    {
                        var localWs = await _workScheduleRepository.GetByIdAsync(request.WorkScheduleId);
                        if (localWs != null)
                        {
                            localWs.AddAppointment(appointment);
                        }
                        await _appointmentRepository.AddAsync(appointment);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);

                        // 3. Confirm Reservation in Scheduling Service
                        var confirmed = await _schedulingClient.ConfirmReservationAsync(request.WorkScheduleId, reservation.Id, cancellationToken);
                        if (!confirmed)
                        {
                            throw new ConflictException("Failed to confirm slot reservation with Scheduling service.");
                        }

                        return new AppointmentDto
                        {
                            Id = appointment.Id,
                            PatientId = patient.Id,
                            DoctorId = appointment.DoctorId,
                            PatientName = patient.Person.FullName,
                            DoctorName = appointment.DoctorName,
                            TimeSlot = request.TimeSlot,
                            Reason = request.Reason,
                            Date = appointment.ScheduleDate,
                            Status = appointment.Status,
                            CreatedAt = appointment.CreatedAt
                        };
                    }
                    catch
                    {
                        // 4. Compensation: Release Reservation on error
                        await _schedulingClient.ReleaseReservationAsync(request.WorkScheduleId, reservation.Id, cancellationToken);
                        throw;
                    }
                }
                else
                {
                    // Case 2: Walk-In Booking (IsWalkIn = true)
                    // Skip ReserveSlot online, allowed to exceed RecommendedCapacity
                    var scheduleInfo = await _schedulingClient.GetScheduleAsync(request.WorkScheduleId, cancellationToken);
                    if (scheduleInfo == null)
                    {
                        // Fallback to local repository if available
                        var localWs = await _workScheduleRepository.GetByIdAsync(request.WorkScheduleId);
                        if (localWs == null) throw new NotFoundException("Work schedule not found.");
                        scheduleInfo = new ScheduleInfoResult(
                            localWs.Id,
                            localWs.DoctorId,
                            localWs.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                            localWs.Doctor?.WorkHistories?.FirstOrDefault(wh => !wh.IsDeleted && wh.EndDate == null)?.Specialty?.Name,
                            localWs.Date,
                            localWs.ShiftStart,
                            localWs.ShiftEnd,
                            localWs.RecommendedCapacity,
                            localWs.Status.ToString()
                        );
                    }

                    var appointment = new Appointment(
                        patient,
                        request.WorkScheduleId,
                        scheduleInfo.DoctorId,
                        scheduleInfo.DoctorName,
                        scheduleInfo.SpecialtyName,
                        scheduleInfo.Date,
                        request.TimeSlot,
                        request.Reason,
                        (Guid)request.UserId,
                        true);

                    // Walk-in is immediately confirmed
                    appointment.Confirm((Guid)request.UserId);
                    await _appointmentRepository.AddAsync(appointment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    return new AppointmentDto
                    {
                        Id = appointment.Id,
                        PatientId = patient.Id,
                        DoctorId = appointment.DoctorId,
                        PatientName = patient.Person.FullName,
                        DoctorName = appointment.DoctorName,
                        TimeSlot = request.TimeSlot,
                        Reason = request.Reason,
                        Date = appointment.ScheduleDate,
                        Status = appointment.Status,
                        CreatedAt = appointment.CreatedAt
                    };
                }
            }

            // LEGACY / MONOLITH FALLBACK (when ISchedulingServiceClient is not injected)
            // Begin transaction lock to prevent race conditions (active appointment count > RecommendedCapacity) when creating appointments
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

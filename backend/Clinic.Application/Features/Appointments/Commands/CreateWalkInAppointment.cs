using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Appointments.Commands
{
    public record CreateWalkInAppointmentCommand(
        Guid UserId,
        Guid PatientId,
        Guid SpecialtyId,
        Guid? DoctorId,
        string? Reason,
        bool IsUrgent,
        string? IdempotencyKey
    ) : IRequest<WalkInResultDto>;

    public class CreateWalkInAppointmentHandler : IRequestHandler<CreateWalkInAppointmentCommand, WalkInResultDto>
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateWalkInAppointmentHandler(
            IPatientRepository patientRepository,
            IWorkScheduleRepository workScheduleRepository,
            IAppointmentRepository appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _patientRepository = patientRepository;
            _workScheduleRepository = workScheduleRepository;
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<WalkInResultDto> Handle(CreateWalkInAppointmentCommand request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _appointmentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existing != null)
                {
                    var existingTicket = existing.QueueTicket ?? await _queueTicketRepository.GetByAppointmentIdAsync(existing.Id);
                    var existingWh = existing.WorkSchedule?.Doctor?.WorkHistories.FirstOrDefault(wh => wh.EndDate == null);
                    return new WalkInResultDto
                    {
                        AppointmentId = existing.Id,
                        PatientId = existing.PatientId,
                        PatientName = existing.Patient?.Person?.FullName ?? string.Empty,
                        DoctorId = existing.WorkSchedule?.DoctorId ?? Guid.Empty,
                        DoctorName = existing.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                        SpecialtyName = existingWh?.Specialty?.Name ?? string.Empty,
                        QueueTicketId = existingTicket?.Id,
                        QueueNumber = existingTicket?.QueueNumber,
                        IsUrgent = existingTicket?.Priority ?? request.IsUrgent,
                        CheckInTime = existingTicket?.CheckInTime ?? existing.CreatedAt,
                        Status = existing.Status.ToString()
                    };
                }
            }

            var patient = await _patientRepository.GetByIdAsync(request.PatientId)
                ?? throw new NotFoundException($"Patient '{request.PatientId}' not found.");

            var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            // Find matching work schedule today
            var candidates = (await _workScheduleRepository.GetDoctorSchedulesWithAppointmentByDateAsync(request.DoctorId, todayLocal)).ToList();
            if (candidates.Count == 0 && todayLocal != todayUtc)
            {
                candidates = (await _workScheduleRepository.GetDoctorSchedulesWithAppointmentByDateAsync(request.DoctorId, todayUtc)).ToList();
            }

            if (request.DoctorId == null)
            {
                candidates = candidates
                    .Where(ws => ws.Doctor != null && ws.Doctor.WorkHistories.Any(wh => wh.EndDate == null && wh.SpecialtyId == request.SpecialtyId))
                    .ToList();
            }

            var activeCandidates = candidates
                .Where(ws => ws.Status == WorkScheduleStatus.Active && !ws.IsDeleted)
                .OrderBy(ws => ws.Appointments.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))
                .ToList();

            WorkSchedule? selectedSchedule = activeCandidates.FirstOrDefault();

            if (selectedSchedule == null)
            {
                // Fallback: search available slots or any active schedule for this doctor / specialty
                var fallbackSlots = await _workScheduleRepository.GetAvailableSlotsAsync(request.DoctorId, request.SpecialtyId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1));
                selectedSchedule = fallbackSlots.FirstOrDefault(ws => ws.Status == WorkScheduleStatus.Active);
            }

            if (selectedSchedule == null)
            {
                throw new ConflictException("Không tìm thấy ca trực nào của bác sĩ/chuyên khoa này hôm nay.");
            }

            var timeSlot = nowTime >= selectedSchedule.ShiftStart && nowTime < selectedSchedule.ShiftEnd
                ? nowTime
                : selectedSchedule.ShiftStart;

            var appointment = new Appointment(
                patient,
                selectedSchedule,
                timeSlot,
                string.IsNullOrWhiteSpace(request.Reason) ? "Walk-in" : request.Reason.Trim(),
                request.UserId,
                isWalkIn: true)
            {
                IdempotencyKey = request.IdempotencyKey
            };

            appointment.Confirm(request.UserId);
            selectedSchedule.AddAppointment(appointment);
            await _appointmentRepository.AddAsync(appointment);

            var checkInTime = DateTime.UtcNow;
            var queueDate = selectedSchedule.Date;
            var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(selectedSchedule.DoctorId, queueDate.ToDateTime(TimeOnly.MinValue), cancellationToken);

            var queueTicket = appointment.CheckIn(checkInTime, queueNumber);
            queueTicket.QueueDate = queueDate;
            if (request.IsUrgent)
            {
                queueTicket.SetPriority(true);
            }
            queueTicket.PatientName = patient.Person.FullName;
            queueTicket.DoctorName = selectedSchedule.Doctor?.Employee?.Person?.FullName;

            await _queueTicketRepository.AddAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var activeWh = selectedSchedule.Doctor?.WorkHistories.FirstOrDefault(wh => wh.EndDate == null);

            return new WalkInResultDto
            {
                AppointmentId = appointment.Id,
                PatientId = patient.Id,
                PatientName = patient.Person.FullName,
                DoctorId = selectedSchedule.DoctorId,
                DoctorName = selectedSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                SpecialtyName = activeWh?.Specialty?.Name ?? string.Empty,
                QueueTicketId = queueTicket.Id,
                QueueNumber = queueTicket.QueueNumber,
                IsUrgent = queueTicket.Priority,
                CheckInTime = checkInTime,
                Status = appointment.Status.ToString()
            };
        }
    }
}

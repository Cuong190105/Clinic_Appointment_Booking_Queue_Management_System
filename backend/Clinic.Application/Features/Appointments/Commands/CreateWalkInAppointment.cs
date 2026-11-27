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

            var timeConverter = new Clinic.Domain.Common.TimeConverter();
            var todayLocal = timeConverter.Today;
            var nowTime = timeConverter.CurrentTime;

            // Find matching active work schedule for today only (no fallback to yesterday/tomorrow/ended shifts)
            var candidates = (await _workScheduleRepository.GetDoctorSchedulesWithAppointmentByDateAsync(request.DoctorId, todayLocal)).ToList();

            if (request.DoctorId == null)
            {
                candidates = candidates
                    .Where(ws => ws.Doctor != null && ws.Doctor.WorkHistories.Any(wh => wh.EndDate == null && wh.SpecialtyId == request.SpecialtyId))
                    .ToList();
            }

            var activeCandidates = candidates
                .Where(ws => ws.Date == todayLocal && ws.ShiftEnd > nowTime && ws.Status == WorkScheduleStatus.Active && !ws.IsDeleted)
                .OrderBy(ws => ws.Appointments.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))
                .ToList();

            var selectedSchedule = activeCandidates.FirstOrDefault();

            if (selectedSchedule == null)
            {
                throw new ConflictException("Không tìm thấy ca trực nào đang hoạt động của bác sĩ/chuyên khoa này hôm nay.");
            }

            var trackedSchedule = await _workScheduleRepository.GetByIdAsync(selectedSchedule.Id)
                ?? throw new ConflictException("Không tìm thấy ca trực nào của bác sĩ/chuyên khoa này hôm nay.");

            var timeSlot = nowTime >= trackedSchedule.ShiftStart && nowTime < trackedSchedule.ShiftEnd
                ? nowTime
                : trackedSchedule.ShiftStart;

            var appointment = new Appointment(
                patient,
                trackedSchedule,
                timeSlot,
                string.IsNullOrWhiteSpace(request.Reason) ? "Walk-in" : request.Reason.Trim(),
                request.UserId,
                isWalkIn: true)
            {
                IdempotencyKey = request.IdempotencyKey
            };

            appointment.Confirm(request.UserId);
            trackedSchedule.AddAppointment(appointment);
            await _appointmentRepository.AddAsync(appointment);

            var checkInTime = DateTime.UtcNow;
            var queueDate = trackedSchedule.Date;
            var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(trackedSchedule.DoctorId, queueDate.ToDateTime(TimeOnly.MinValue), cancellationToken);

            var queueTicket = appointment.CheckIn(checkInTime, queueNumber);
            queueTicket.QueueDate = queueDate;
            if (request.IsUrgent)
            {
                queueTicket.SetPriority(true);
            }
            queueTicket.PatientName = patient.Person.FullName;
            queueTicket.DoctorName = trackedSchedule.Doctor?.Employee?.Person?.FullName;

            await _queueTicketRepository.AddAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var activeWh = trackedSchedule.Doctor?.WorkHistories.FirstOrDefault(wh => wh.EndDate == null)
                ?? selectedSchedule.Doctor?.WorkHistories.FirstOrDefault(wh => wh.EndDate == null);

            return new WalkInResultDto
            {
                AppointmentId = appointment.Id,
                PatientId = patient.Id,
                PatientName = patient.Person.FullName,
                DoctorId = trackedSchedule.DoctorId,
                DoctorName = trackedSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
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

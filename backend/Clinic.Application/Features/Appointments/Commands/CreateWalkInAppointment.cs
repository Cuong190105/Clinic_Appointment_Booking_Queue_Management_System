using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.ComponentModel.DataAnnotations;
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
        bool IsUrgent
    ) : IRequest<ReceptionistWalkInResultDto>;

    public class CreateWalkInAppointmentHandler : IRequestHandler<CreateWalkInAppointmentCommand, ReceptionistWalkInResultDto>
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

        public async Task<ReceptionistWalkInResultDto> Handle(CreateWalkInAppointmentCommand request, CancellationToken cancellationToken)
        {
            var patient = await _patientRepository.GetByIdAsync(request.PatientId)
                ?? throw new NotFoundException($"Patient '{request.PatientId}' not found.");

            var today = new TimeConverter().ToLocalDate(DateTime.UtcNow);
            var activeSchedules = await _workScheduleRepository.GetActiveWorkSchedulesForDateAsync(today, request.SpecialtyId, cancellationToken);

            if (!activeSchedules.Any())
            {
                throw new ValidationException("Hiện không có bác sĩ nào thuộc chuyên khoa này đang có ca trực hôm nay.");
            }

            WorkSchedule selectedSchedule;
            if (request.DoctorId.HasValue && request.DoctorId.Value != Guid.Empty)
            {
                selectedSchedule = activeSchedules.FirstOrDefault(ws => ws.DoctorId == request.DoctorId.Value)
                    ?? throw new ValidationException("Bác sĩ được chọn hiện không có ca trực nào trong ngày hôm nay.");
            }
            else
            {
                // Tự động phân bổ bác sĩ có ít lượt khám nhất trong ca hôm nay
                selectedSchedule = activeSchedules
                    .OrderBy(ws => ws.Appointments?.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled) ?? 0)
                    .First();
            }

            var nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);
            var timeSlot = (nowTime >= selectedSchedule.ShiftStart && nowTime < selectedSchedule.ShiftEnd)
                ? nowTime
                : selectedSchedule.ShiftStart;

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Khám bệnh tiếp đón tại quầy (Walk-in)" : request.Reason.Trim();

            // Tạo appointment walk-in
            var appointment = new Appointment(
                patient,
                selectedSchedule,
                timeSlot,
                reason,
                request.UserId,
                isWalkIn: true
            );

            // Xác nhận ngay
            appointment.Confirm(request.UserId);

            selectedSchedule.AddAppointment(appointment);
            await _appointmentRepository.AddAsync(appointment);

            // Tự động check-in và sinh số thứ tự
            var checkInTime = DateTime.UtcNow;
            var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(selectedSchedule.DoctorId, checkInTime.Date, cancellationToken);
            var queueTicket = appointment.CheckIn(checkInTime, queueNumber, request.IsUrgent);

            await _queueTicketRepository.AddAsync(queueTicket);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var doctorName = selectedSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty;
            var specialtyName = selectedSchedule.Doctor?.WorkHistories?.FirstOrDefault(wh => !wh.IsDeleted && wh.EndDate == null)?.Specialty?.Name ?? string.Empty;

            return new ReceptionistWalkInResultDto
            {
                AppointmentId = appointment.Id,
                PatientId = patient.Id,
                PatientName = patient.Person?.FullName ?? string.Empty,
                DoctorId = selectedSchedule.DoctorId,
                DoctorName = doctorName,
                SpecialtyName = specialtyName,
                QueueTicketId = queueTicket.Id,
                QueueNumber = queueNumber,
                IsUrgent = queueTicket.Priority,
                CheckInTime = checkInTime,
                Status = appointment.Status.ToString(),
                WorkScheduleId = selectedSchedule.Id,
                ScheduleDate = selectedSchedule.Date,
                TimeSlot = timeSlot,
                Reason = reason
            };
        }
    }
}


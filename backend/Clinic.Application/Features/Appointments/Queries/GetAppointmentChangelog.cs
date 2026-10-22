using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetAppointmentChangelogQuery(Guid AppointmentId) : IRequest<AppointmentChangelogDto>;
    public class GetAppointmentChangelogHandler : IRequestHandler<GetAppointmentChangelogQuery, AppointmentChangelogDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPersonRepository _personRepository;
        public GetAppointmentChangelogHandler(IAppointmentRepository appointmentRepository, IPersonRepository personRepository)
        {
            _appointmentRepository = appointmentRepository;
            _personRepository = personRepository;
        }
        public async Task<AppointmentChangelogDto> Handle(GetAppointmentChangelogQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetAppointmentChangelogAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }
            List<AppointmentHistoryDto> changelog = new List<AppointmentHistoryDto>();
            var currentUpdater = await _personRepository.GetByIdentityUserIdAsync(appointment.UpdatedByUserId);
            changelog.Add(new AppointmentHistoryDto
            {
                DoctorId = appointment.DoctorId != Guid.Empty ? appointment.DoctorId : (appointment.WorkSchedule?.DoctorId ?? Guid.Empty),
                DoctorName = !string.IsNullOrEmpty(appointment.DoctorName) ? appointment.DoctorName : (appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty),
                TimeSlot = appointment.TimeSlot,
                Date = appointment.ScheduleDate != default ? appointment.ScheduleDate : (appointment.WorkSchedule?.Date ?? default),
                Reason = appointment.Reason,
                Status = appointment.Status,
                UpdatedAt = appointment.UpdatedAt ?? appointment.CreatedAt,
                UpdatedByUserId = appointment.UpdatedByUserId,
                UpdatorName = currentUpdater?.FullName ?? "Unknown"
            });
            var sortedHistory = appointment.Snapshots.OrderByDescending(s => s.UpdatedAt).ToList();
            foreach (var snapshot in sortedHistory)
            {
                var updater = await _personRepository.GetByIdentityUserIdAsync(snapshot.UpdatedByUserId);
                changelog.Add(new AppointmentHistoryDto
                {
                    DoctorId = snapshot.OldWorkSchedule?.DoctorId ?? Guid.Empty,
                    DoctorName = snapshot.OldWorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    TimeSlot = snapshot.TimeSlot,
                    Date = snapshot.OldWorkSchedule?.Date ?? default,
                    Reason = snapshot.Reason,
                    Status = snapshot.Status,
                    UpdatedAt = snapshot.UpdatedAt,
                    UpdatedByUserId = snapshot.UpdatedByUserId,
                    UpdatorName = updater?.FullName ?? "Unknown"
                });
            }
            return new AppointmentChangelogDto
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient?.Person?.FullName ?? string.Empty,
                Changelog = changelog
            };
        }
    }
}

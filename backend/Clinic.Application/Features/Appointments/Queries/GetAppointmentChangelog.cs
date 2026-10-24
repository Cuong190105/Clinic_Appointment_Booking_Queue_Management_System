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
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.DoctorName,
                TimeSlot = appointment.TimeSlot,
                Date = appointment.ScheduleDate != default ? appointment.ScheduleDate : DateOnly.FromDateTime(appointment.CreatedAt),
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
                    DoctorId = appointment.DoctorId,
                    DoctorName = appointment.DoctorName,
                    TimeSlot = snapshot.TimeSlot,
                    Date = appointment.ScheduleDate != default ? appointment.ScheduleDate : DateOnly.FromDateTime(appointment.CreatedAt),
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

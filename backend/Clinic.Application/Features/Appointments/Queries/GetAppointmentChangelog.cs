using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetAppointmentChangelogQuery(Guid AppointmentId) : IRequest<AppointmentChangelogDto>;
    public class GetAppointmentChangelogHandler : IRequestHandler<GetAppointmentChangelogQuery, AppointmentChangelogDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        public GetAppointmentChangelogHandler(IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
        }
        public async Task<AppointmentChangelogDto> Handle(GetAppointmentChangelogQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetAppointmentChangelogAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }
            List<AppointmentHistoryDto> changelog = new List<AppointmentHistoryDto>();
            changelog.Add(new AppointmentHistoryDto
            {
                DoctorId = appointment.WorkSchedule.DoctorId,
                DoctorName = appointment.WorkSchedule.Doctor.Employee.Person.FullName,
                TimeSlot = appointment.TimeSlot,
                Date = appointment.WorkSchedule.Date,
                Reason = appointment.Reason,
                Status = appointment.Status,
                UpdatedAt = appointment.UpdatedAt ?? appointment.CreatedAt,
                UpdatedByUserId = appointment.UpdatedByUserId,
                UpdatorName = appointment.Updator.Person.FullName
            });
            var sortedHistory = appointment.Snapshots.OrderByDescending(s => s.UpdatedAt).ToList();
            foreach (var snapshot in sortedHistory)
            {
                changelog.Add(new AppointmentHistoryDto
                {
                    DoctorId = snapshot.OldWorkSchedule.DoctorId,
                    DoctorName = snapshot.OldWorkSchedule.Doctor.Employee.Person.FullName,
                    TimeSlot = snapshot.TimeSlot,
                    Date = snapshot.OldWorkSchedule.Date,
                    Reason = snapshot.Reason,
                    Status = snapshot.Status,
                    UpdatedAt = snapshot.UpdatedAt,
                    UpdatedByUserId = snapshot.UpdatedByUserId,
                    UpdatorName = snapshot.Updator.Person.FullName
                });
            }
            return new AppointmentChangelogDto
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.Person.FullName,
                Changelog = changelog
            };
        }
    }
}

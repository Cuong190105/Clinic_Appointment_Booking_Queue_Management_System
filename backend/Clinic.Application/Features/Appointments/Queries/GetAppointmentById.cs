using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetAppointmentByIdQuery(
        Guid AppointmentId,
        Guid UserId
    ) : IRequest<AppointmentDetailDto>;
    public class GetAppointmentByIdQueryHandler : IRequestHandler<GetAppointmentByIdQuery, AppointmentDetailDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;

        public GetAppointmentByIdQueryHandler(IAppointmentRepository appointmentRepository, ICurrentUser currentUser)
        {
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
        }

        public async Task<AppointmentDetailDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }

            var doctorId = appointment.DoctorId;
            var doctorName = appointment.DoctorName;
            var date = appointment.ScheduleDate != default
                ? appointment.ScheduleDate
                : DateOnly.FromDateTime(appointment.CreatedAt);

            // Only the receptionist, the patient who owns the appointment, or the doctor assigned to the appointment can access it
            if (_currentUser.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException("Unauthorized.");
            }

            if (!_currentUser.HasRole("Receptionist") && !_currentUser.HasRole("Admin") &&
                !(_currentUser.HasRole("Patient") && _currentUser.PatientId == appointment.PatientId) &&
                !(_currentUser.HasRole("Doctor") && _currentUser.DoctorId == doctorId)
            )
            {
                throw new ForbiddenException("Forbidden.");
            }

            return new AppointmentDetailDto
            {
                Id = appointment.Id,
                PatientId = appointment.PatientId,
                DoctorId = doctorId,
                PatientName = appointment.Patient.Person.FullName,
                DoctorName = doctorName,
                TimeSlot = appointment.TimeSlot,
                Date = date,
                Reason = appointment.Reason,
                Status = appointment.Status,
                CreatedAt = appointment.CreatedAt
            };
        }
    }
}

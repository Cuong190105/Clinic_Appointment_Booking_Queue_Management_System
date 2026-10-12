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
        private readonly IUserRepository _userRepository;

        public GetAppointmentByIdQueryHandler(IAppointmentRepository appointmentRepository, IUserRepository userRepository)
        {
            _appointmentRepository = appointmentRepository;
            _userRepository = userRepository;
        }

        public async Task<AppointmentDetailDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }

            // Only the receptionist, the patient who owns the appointment, or the doctor assigned to the appointment can access it
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Unauthorized.");
            }

            if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist" || ur.Role.Name == "Admin") && // Not a receptionist or admin
                (!user.UserRoles.Any(ur => ur.Role.Name == "Patient") || user.Person.Patient.Id != appointment.PatientId) && // Not a patient, or not the patient who owns the appointment
                (!user.UserRoles.Any(ur => ur.Role.Name == "Doctor") || user.Person.Employee.Doctor.Id != appointment.WorkSchedule.DoctorId) // Not a doctor, or not the doctor assigned to the appointment
            )
            {
                throw new ForbiddenException("Forbidden.");
            }

            return new AppointmentDetailDto
            {
                Id = appointment.Id,
                PatientId = appointment.PatientId,
                DoctorId = appointment.WorkSchedule.DoctorId,
                PatientName = appointment.Patient.Person.FullName,
                DoctorName = appointment.WorkSchedule.Doctor.Employee.Person.FullName,
                TimeSlot = appointment.TimeSlot,
                Date = appointment.WorkSchedule.Date,
                Reason = appointment.Reason,
                Status = appointment.Status,
                CreatedAt = appointment.CreatedAt
            };
        }
    }
}

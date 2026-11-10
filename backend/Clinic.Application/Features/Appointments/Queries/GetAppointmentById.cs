using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ICurrentUser? _currentUser;

        public GetAppointmentByIdQueryHandler(
            IAppointmentRepository appointmentRepository,
            IUserRepository userRepository,
            ICurrentUser? currentUser = null)
        {
            _appointmentRepository = appointmentRepository;
            _userRepository = userRepository;
            _currentUser = currentUser;
        }

        public async Task<AppointmentDetailDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }

            var isStaff = _currentUser != null && (_currentUser.IsInRole("Receptionist") || _currentUser.IsInRole("Admin"));
            var isPatientOwner = _currentUser?.PatientId.HasValue == true && appointment.PatientId == _currentUser.PatientId.Value;
            var isAssignedDoctor = _currentUser?.DoctorId.HasValue == true && appointment.WorkSchedule.DoctorId == _currentUser.DoctorId.Value;

            if (!isStaff && !isPatientOwner && !isAssignedDoctor)
            {
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    throw new UnauthorizedAccessException("Unauthorized.");
                }

                if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist" || ur.Role.Name == "Admin") &&
                    (!user.UserRoles.Any(ur => ur.Role.Name == "Patient") || user.Person.Patient?.Id != appointment.PatientId) &&
                    (!user.UserRoles.Any(ur => ur.Role.Name == "Doctor") || user.Person.Employee?.Doctor?.Id != appointment.WorkSchedule.DoctorId))
                {
                    throw new ForbiddenException("Forbidden.");
                }
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

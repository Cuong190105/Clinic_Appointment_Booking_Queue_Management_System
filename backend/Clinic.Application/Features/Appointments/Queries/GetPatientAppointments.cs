using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Appointments.Queries
{
    // Use-case: Patient gets their appointments
    public record GetPatientAppointmentsQuery(
        Guid UserId,
        string Category = "upcoming",
        Guid? PatientId = null
    ) : IRequest<List<AppointmentDto>>;

    public class GetPatientAppointmentsQueryHandler : IRequestHandler<GetPatientAppointmentsQuery, List<AppointmentDto>>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUser? _currentUser;

        public GetPatientAppointmentsQueryHandler(
            IAppointmentRepository appointmentRepository,
            IPatientRepository patientRepository,
            IUserRepository userRepository,
            ICurrentUser? currentUser = null)
        {
            _appointmentRepository = appointmentRepository;
            _patientRepository = patientRepository;
            _userRepository = userRepository;
            _currentUser = currentUser;
        }

        public async Task<List<AppointmentDto>> Handle(GetPatientAppointmentsQuery request, CancellationToken cancellationToken)
        {
            var isStaff = _currentUser != null && (_currentUser.IsInRole("Receptionist") || _currentUser.IsInRole("Admin"));

            if (request.PatientId != null && !isStaff)
            {
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    throw new UnauthorizedAccessException();
                }
                if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist" || ur.Role.Name == "Admin"))
                {
                    throw new ForbiddenException("You do not have permission to view other patients' appointments.");
                }
            }

            Guid targetPatientId;
            if (request.PatientId.HasValue)
            {
                var patient = await _patientRepository.GetByIdAsync(request.PatientId.Value);
                if (patient == null)
                {
                    throw new NotFoundException("Patient not found.");
                }
                targetPatientId = patient.Id;
            }
            else
            {
                if (_currentUser?.PatientId.HasValue == true)
                {
                    targetPatientId = _currentUser.PatientId.Value;
                }
                else
                {
                    var user = await _userRepository.GetByIdAsync(request.UserId);
                    if (user == null)
                    {
                        throw new UnauthorizedAccessException();
                    }
                    if (user.Person?.Patient == null)
                    {
                        throw new NotFoundException("Patient not found.");
                    }
                    targetPatientId = user.Person.Patient.Id;
                }
            }

            var appointments = await _appointmentRepository.GetAppointmentsByPatientIdAsync(targetPatientId, request.Category);
            return appointments.Items.Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                DoctorId = a.WorkSchedule.DoctorId,
                PatientName = a.Patient?.Person?.FullName ?? string.Empty,
                DoctorName = a.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                TimeSlot = a.TimeSlot,
                Date = a.WorkSchedule?.Date ?? DateOnly.MinValue,
                Reason = a.Reason,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            }).ToList();
        }
    }
}

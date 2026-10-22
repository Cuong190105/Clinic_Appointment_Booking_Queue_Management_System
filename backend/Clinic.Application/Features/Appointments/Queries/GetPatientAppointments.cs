using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly ICurrentUser _currentUser;

        public GetPatientAppointmentsQueryHandler(IAppointmentRepository appointmentRepository, IPatientRepository patientRepository, ICurrentUser currentUser)
        {
            _appointmentRepository = appointmentRepository;
            _patientRepository = patientRepository;
            _currentUser = currentUser;
        }

        public async Task<List<AppointmentDto>> Handle(GetPatientAppointmentsQuery request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId != request.UserId)
            {
                throw new UnauthorizedAccessException();
            }
            if (!_currentUser.HasRole("Receptionist") && !_currentUser.HasPermission("patient.view.any") &&
                request.PatientId != null && request.PatientId != _currentUser.PatientId)
            {
                throw new ForbiddenException("You do not have permission to view other patients' appointments.");
            }
            var patientId = request.PatientId ?? _currentUser.PatientId;
            var patient = patientId == null ? null : await _patientRepository.GetByIdAsync(patientId.Value);
            if (patient == null)
            {
                throw new NotFoundException("Patient not found.");
            }

            var appointments = await _appointmentRepository.GetAppointmentsByPatientIdAsync(patient.Id, request.Category);
            return appointments.Items.Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                DoctorId = a.DoctorId != Guid.Empty ? a.DoctorId : (a.WorkSchedule?.DoctorId ?? Guid.Empty),
                PatientName = a.Patient?.Person?.FullName ?? string.Empty,
                DoctorName = !string.IsNullOrEmpty(a.DoctorName) ? a.DoctorName : (a.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty),
                TimeSlot = a.TimeSlot,
                Date = a.ScheduleDate != default ? a.ScheduleDate : (a.WorkSchedule?.Date ?? default),
                Reason = a.Reason,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            }).ToList();
        }
    }
}

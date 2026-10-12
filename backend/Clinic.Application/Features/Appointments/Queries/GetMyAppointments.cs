using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetMyAppointmentsQuery : IRequest<List<AppointmentDto>>;

    public class GetMyAppointmentsHandler : IRequestHandler<GetMyAppointmentsQuery, List<AppointmentDto>>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;

        public GetMyAppointmentsHandler(IAppointmentRepository appointmentRepository, ICurrentUser currentUser)
        {
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
        }

        public async Task<List<AppointmentDto>> Handle(GetMyAppointmentsQuery request, CancellationToken cancellationToken)
        {
            if (_currentUser.PatientId is null)
            {
                throw new ForbiddenException("Only a patient account has appointments to view here.");
            }

            var appointments = await _appointmentRepository.GetAppointmentsByPatientIdAsync(_currentUser.PatientId.Value, "upcoming");

            return appointments.Items.Select(a => new AppointmentDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = a.WorkSchedule.DoctorId,
                DoctorName = a.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                TimeSlot = a.TimeSlot,
                Status = a.Status,
                Reason = a.Reason,
                IsWalkIn = a.IsWalkIn
            }).ToList();
        }
    }
}
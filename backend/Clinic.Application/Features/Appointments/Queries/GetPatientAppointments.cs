using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;

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

        public GetPatientAppointmentsQueryHandler(IAppointmentRepository appointmentRepository, IPatientRepository patientRepository, IUserRepository userRepository)
        {
            _appointmentRepository = appointmentRepository;
            _patientRepository = patientRepository;
            _userRepository = userRepository;
        }

        public async Task<List<AppointmentDto>> Handle(GetPatientAppointmentsQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            if (!user.UserRoles.Any(ur => ur.Role.Name == "Receptionist") && request.PatientId != null)
            {
                throw new ForbiddenException("You do not have permission to view other patients' appointments.");
            }
            var patient = request.PatientId == null ? user.Person.Patient : await _patientRepository.GetByIdAsync(request.PatientId);
            if (patient == null)
            {
                throw new NotFoundException("Patient not found.");
            }

            var appointments = await _appointmentRepository.GetAppointmentsByPatientIdAsync(patient.Id, request.Category);
            return appointments.Items.Select(a => new AppointmentDto
                {
                    Id = a.Id,
                    PatientId = a.PatientId,
                    DoctorId = a.WorkSchedule.DoctorId,
                    PatientName = a.Patient.Person.FullName,
                    DoctorName = a.WorkSchedule.Doctor.Employee.Person.FullName,
                    TimeSlot = a.TimeSlot,
                    Date = a.WorkSchedule.Date,
                    Reason = a.Reason,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt
                }).ToList();
        }
    }
}
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Admin.Queries
{
    public record GetDashboardDataQuery : IRequest<DashboardDataDto>;
    public class GetDashboardDataHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly IPatientRepository _patientRepository;

        public GetDashboardDataHandler(IAppointmentRepository appointmentRepository, IDoctorRepository doctorRepository, IPatientRepository patientRepository)
        {
            _appointmentRepository = appointmentRepository;
            _doctorRepository = doctorRepository;
            _patientRepository = patientRepository;
        }

        public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var appointmentData = await _appointmentRepository.GetDashboardData();
            var doctorData = await _doctorRepository.GetDashboardData();
            var patientData = await _patientRepository.GetDashboardData();

            return new DashboardDataDto
            {
                TotalAppointments = appointmentData["TotalAppointmentsLast30Days"],
                TotalPatients = patientData["TotalPatients"],
                TotalDoctors = doctorData["TotalDoctors"],
                ActiveDoctor = doctorData["ActiveDoctors"],
                CompletionRate = appointmentData["CompletionRateLast30Days"],
                AverageWaitingMinute = appointmentData["AverageWaitingTimeLast30Days"]
            };
        }
    }
}

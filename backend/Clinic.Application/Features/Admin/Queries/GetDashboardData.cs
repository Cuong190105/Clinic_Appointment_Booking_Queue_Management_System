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
        private readonly ISchedulingServiceClient _schedulingClient;
        private readonly IPatientRepository _patientRepository;

        public GetDashboardDataHandler(IAppointmentRepository appointmentRepository, ISchedulingServiceClient schedulingClient, IPatientRepository patientRepository)
        {
            _appointmentRepository = appointmentRepository;
            _schedulingClient = schedulingClient;
            _patientRepository = patientRepository;
        }

        public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var appointmentData = await _appointmentRepository.GetDashboardData();
            var doctorStats = await _schedulingClient.GetDoctorDashboardStatsAsync(cancellationToken);
            var patientData = await _patientRepository.GetDashboardData();

            return new DashboardDataDto
            {
                TotalAppointments = appointmentData["TotalAppointmentsLast30Days"],
                TotalPatients = patientData["TotalPatients"],
                TotalDoctors = new ValueWithChange<int>
                {
                    Value = doctorStats?.TotalDoctors ?? 0,
                    Change = doctorStats?.TotalDoctorsChange ?? 0
                },
                ActiveDoctor = new ValueWithChange<int>
                {
                    Value = doctorStats?.ActiveDoctors ?? 0,
                    Change = doctorStats?.ActiveDoctorsChange ?? 0
                },
                CompletionRate = appointmentData["CompletionRateLast30Days"],
                AverageWaitingMinute = appointmentData["AverageWaitingTimeLast30Days"]
            };
        }
    }
}
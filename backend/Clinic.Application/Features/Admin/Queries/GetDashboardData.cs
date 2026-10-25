using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Clinic.Contracts.Analytics;

namespace Clinic.Application.Features.Admin.Queries
{
    public record GetDashboardDataQuery(
        AppointmentDashboardMetrics AppointmentMetrics,
        QueueDashboardMetrics QueueMetrics) : IRequest<DashboardDataDto>;
    public class GetDashboardDataHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
    {
        private readonly ISchedulingServiceClient _schedulingClient;
        private readonly IPatientRepository _patientRepository;

        public GetDashboardDataHandler(ISchedulingServiceClient schedulingClient, IPatientRepository patientRepository)
        {
            _schedulingClient = schedulingClient;
            _patientRepository = patientRepository;
        }

        public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var doctorStats = await _schedulingClient.GetDoctorDashboardStatsAsync(cancellationToken);
            var patientData = await _patientRepository.GetDashboardData();

            return new DashboardDataDto
            {
                TotalAppointments = new ValueWithChange<int>
                {
                    Value = request.AppointmentMetrics.TotalAppointments,
                    Change = request.AppointmentMetrics.TotalAppointmentsChange
                },
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
                CompletionRate = new ValueWithChange<double>
                {
                    Value = request.AppointmentMetrics.CompletionRate,
                    Change = request.AppointmentMetrics.CompletionRateChange
                },
                AverageWaitingMinute = new ValueWithChange<double>
                {
                    Value = request.QueueMetrics.AverageWaitingMinutes,
                    Change = request.QueueMetrics.Change
                }
            };
        }
    }
}

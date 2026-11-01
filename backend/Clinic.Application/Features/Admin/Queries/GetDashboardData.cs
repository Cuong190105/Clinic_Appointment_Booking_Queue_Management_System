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
        QueueDashboardMetrics QueueMetrics,
        int TotalPatients) : IRequest<DashboardDataDto>;
    public class GetDashboardDataHandler : IRequestHandler<GetDashboardDataQuery, DashboardDataDto>
    {
        private readonly ISchedulingServiceClient _schedulingClient;
        public GetDashboardDataHandler(ISchedulingServiceClient schedulingClient)
        {
            _schedulingClient = schedulingClient;
        }

        public async Task<DashboardDataDto> Handle(GetDashboardDataQuery request, CancellationToken cancellationToken)
        {
            var doctorStats = await _schedulingClient.GetDoctorDashboardStatsAsync(cancellationToken);

            return new DashboardDataDto
            {
                TotalAppointments = new ValueWithChange<int>
                {
                    Value = request.AppointmentMetrics.TotalAppointments,
                    Change = request.AppointmentMetrics.TotalAppointmentsChange
                },
                TotalPatients = new ValueWithChange<int> { Value = request.TotalPatients },
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

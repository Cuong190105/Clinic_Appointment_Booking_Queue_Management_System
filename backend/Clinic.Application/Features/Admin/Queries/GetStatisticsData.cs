using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Admin.Queries
{
    public record GetStatisticsDataQuery(Period Period) : IRequest<StatisticsDataDto>;
    public enum Period
    {
        Week,
        Month
    }
    public class GetStatisticsDataHandler : IRequestHandler<GetStatisticsDataQuery, StatisticsDataDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        public GetStatisticsDataHandler(IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
        }

        public async Task<StatisticsDataDto> Handle(GetStatisticsDataQuery request, CancellationToken cancellationToken)
        {
            var result = await _appointmentRepository.GetStatistics(request.Period == Period.Week);
            return result;
        }
    }
}

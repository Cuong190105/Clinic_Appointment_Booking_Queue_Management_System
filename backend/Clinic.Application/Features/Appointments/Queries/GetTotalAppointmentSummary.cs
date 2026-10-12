using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetTotalAppointmentSummaryQuery : IRequest<TotalAppointmentSummaryDto>
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public Guid DoctorId { get; set; }
        public Guid SpecialtyId { get; set; }
    }
    public class GetTotalAppointmentSummaryQueryHandler : IRequestHandler<GetTotalAppointmentSummaryQuery, TotalAppointmentSummaryDto>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        public GetTotalAppointmentSummaryQueryHandler(IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
        }

        public async Task<TotalAppointmentSummaryDto> Handle(GetTotalAppointmentSummaryQuery request, CancellationToken cancellationToken)
        {
            if (request.StartDate > request.EndDate)
            {
                throw new ArgumentException("StartDate cannot be later than EndDate.");
            }
            if (request.DoctorId != Guid.Empty && request.SpecialtyId != Guid.Empty)
            {
                throw new ArgumentException("Cannot filter by both DoctorId and SpecialtyId at the same time.");
            }
            var summary = await _appointmentRepository.GetTotalAppointmentSummaryAsync(request.StartDate, request.EndDate, request.DoctorId, request.SpecialtyId);
            return summary;
        }
    }
}

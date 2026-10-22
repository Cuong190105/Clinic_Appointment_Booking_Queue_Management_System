using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clinic.Application.Features.Appointments.Queries
{
    public record GetPendingAppointmentsQuery(
        string Search = "",
        string SortBy = "createdAt",
        bool Descending = false,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<AppointmentDto>>;
    public class GetPendingAppointmentsHandler : IRequestHandler<GetPendingAppointmentsQuery, PaginationResponse<AppointmentDto>>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        public GetPendingAppointmentsHandler(IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
        }

        public async Task<PaginationResponse<AppointmentDto>> Handle(GetPendingAppointmentsQuery request, CancellationToken cancellationToken)
        {
            var result = await _appointmentRepository.GetPendingAppointmentsAsync(request.Search, request.SortBy, request.Descending, request.PageNumber, request.PageSize);
            return new PaginationResponse<AppointmentDto>(
                items: result.Items.Select(a => new AppointmentDto
                {
                    Id = a.Id,
                    PatientId = a.PatientId,
                    PatientName = a.Patient?.Person?.FullName ?? string.Empty,
                    DoctorId = a.DoctorId != Guid.Empty ? a.DoctorId : (a.WorkSchedule?.DoctorId ?? Guid.Empty),
                    DoctorName = !string.IsNullOrEmpty(a.DoctorName) ? a.DoctorName : (a.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty),
                    Date = a.ScheduleDate != default ? a.ScheduleDate : (a.WorkSchedule?.Date ?? default),
                    TimeSlot = a.TimeSlot,
                    Reason = a.Reason,
                    IsWalkIn = a.IsWalkIn,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt
                }).ToList(),
                totalCount: result.TotalCount,
                pageNumber: request.PageNumber,
                pageSize: request.PageSize
            );
        }
    }
}

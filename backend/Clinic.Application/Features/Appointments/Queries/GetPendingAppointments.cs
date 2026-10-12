using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
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
                    PatientName = a.Patient.Person.FullName,
                    DoctorId = a.WorkSchedule.DoctorId,
                    DoctorName = a.WorkSchedule.Doctor.Employee.Person.FullName,
                    Date = a.WorkSchedule.Date,
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

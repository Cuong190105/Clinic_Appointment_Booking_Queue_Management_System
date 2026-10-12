using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Admin.Queries
{
    public record GetAppointmentByDateQuery(
        DateOnly Date,
        int PageSize = 10,
        int PageNumber = 1) : IRequest<PaginationResponse<AppointmentExtended>>;
    public class GetAppointmentsByDateHandler : IRequestHandler<GetAppointmentByDateQuery, PaginationResponse<AppointmentExtended>>
    {
        private readonly IAppointmentRepository _appointmentsRepository;
        public GetAppointmentsByDateHandler(IAppointmentRepository appointmentsRepository)
        {
            _appointmentsRepository = appointmentsRepository;
        }

        public async Task<PaginationResponse<AppointmentExtended>> Handle(GetAppointmentByDateQuery request, CancellationToken cancellationToken)
        {
            var apps = await _appointmentsRepository.GetAppointmentsByDateAsync(request.Date, request.PageNumber, request.PageSize);
            return new PaginationResponse<AppointmentExtended>(
                items: apps.Items.Select(a => new AppointmentExtended
                {
                    Id = a.Id,
                    PatientId = a.PatientId,
                    PatientName = a.Patient.Person.FullName,
                    DoctorId = a.WorkSchedule.DoctorId,
                    DoctorName = a.WorkSchedule.Doctor.Employee.Person.FullName,
                    SpecialtyId = a.WorkSchedule.Doctor.WorkHistories.FirstOrDefault()?.SpecialtyId ?? Guid.Empty,
                    SpecialtyName = a.WorkSchedule.Doctor.WorkHistories.FirstOrDefault()?.Specialty?.Name ?? "Unknown",
                    Date = a.WorkSchedule.Date,
                    TimeSlot = a.TimeSlot,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    IsWalkIn = a.IsWalkIn,
                    Reason = a.Reason,
                }).ToList(),
                totalCount: apps.TotalCount,
                pageNumber: request.PageNumber,
                pageSize: request.PageSize);
        }
    }
}

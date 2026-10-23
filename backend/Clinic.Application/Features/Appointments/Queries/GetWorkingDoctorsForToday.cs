using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.Appointments.Queries
{
    public class WorkingDoctorDto
    {
        public Guid DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public Guid? SpecialtyId { get; set; }
        public string? SpecialtyName { get; set; }
        public Guid WorkScheduleId { get; set; }
        public TimeOnly ShiftStart { get; set; }
        public TimeOnly ShiftEnd { get; set; }
        public int ActiveAppointmentsCount { get; set; }
    }

    public record GetWorkingDoctorsForTodayQuery(Guid? SpecialtyId = null) : IRequest<List<WorkingDoctorDto>>;

    public class GetWorkingDoctorsForTodayHandler : IRequestHandler<GetWorkingDoctorsForTodayQuery, List<WorkingDoctorDto>>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;

        public GetWorkingDoctorsForTodayHandler(IWorkScheduleRepository workScheduleRepository)
        {
            _workScheduleRepository = workScheduleRepository;
        }

        public async Task<List<WorkingDoctorDto>> Handle(GetWorkingDoctorsForTodayQuery request, CancellationToken cancellationToken)
        {
            var today = new TimeConverter().ToLocalDate(DateTime.UtcNow);
            var schedules = await _workScheduleRepository.GetActiveWorkSchedulesForDateAsync(today, request.SpecialtyId, cancellationToken);

            var result = schedules.Select(ws =>
            {
                var specialty = ws.Doctor?.WorkHistories?.FirstOrDefault(wh => !wh.IsDeleted && wh.EndDate == null)?.Specialty;
                var activeCount = ws.Appointments?.Count(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled) ?? 0;

                return new WorkingDoctorDto
                {
                    DoctorId = ws.DoctorId,
                    FullName = ws.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    SpecialtyId = specialty?.Id,
                    SpecialtyName = specialty?.Name ?? string.Empty,
                    WorkScheduleId = ws.Id,
                    ShiftStart = ws.ShiftStart,
                    ShiftEnd = ws.ShiftEnd,
                    ActiveAppointmentsCount = activeCount
                };
            }).ToList();

            return result;
        }
    }
}


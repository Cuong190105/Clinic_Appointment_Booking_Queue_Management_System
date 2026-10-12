using Clinic.Application.Common.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Queries
{
    public record GetDoctorsWeeklySchedulesQuery(
        Guid? SpecialtyId,
        string? SearchTerm,
        DateOnly StartDate,
        DateOnly EndDate,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<DoctorScheduleWithSpecialtyDto<WorkScheduleDto>>>;

    public class GetDoctorsWeeklySchedulesHandler : IRequestHandler<GetDoctorsWeeklySchedulesQuery, PaginationResponse<DoctorScheduleWithSpecialtyDto<WorkScheduleDto>>>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;

        public GetDoctorsWeeklySchedulesHandler(IDoctorRepository doctorRepository, IWorkScheduleRepository workScheduleRepository)
        {
            _doctorRepository = doctorRepository;
            _workScheduleRepository = workScheduleRepository;
        }

        public async Task<PaginationResponse<DoctorScheduleWithSpecialtyDto<WorkScheduleDto>>> Handle(GetDoctorsWeeklySchedulesQuery request, CancellationToken cancellationToken)
        {
            if (request.StartDate.AddDays(7) < request.EndDate)
            {
                throw new ArgumentException("The date range should not exceed 7 days.");
            }
            var doctors = await _workScheduleRepository.GetWeeklyWorkSchedules(request.StartDate, request.EndDate, request.PageNumber, request.PageSize, request.SpecialtyId, request.SearchTerm);
            var response = new PaginationResponse<DoctorScheduleWithSpecialtyDto<WorkScheduleDto>>
            {
                Items = doctors.Items.Select(d => new DoctorScheduleWithSpecialtyDto<WorkScheduleDto>
                {
                    DoctorId = d.Id,
                    DoctorName = d.Employee.Person.FullName,
                    Schedules = d.WorkSchedules.Select(ws => new WorkScheduleDto
                    {
                        Id = ws.Id,
                        DoctorId = ws.DoctorId,
                        Date = ws.Date,
                        StartTime = ws.ShiftStart,
                        EndTime = ws.ShiftEnd,
                        PatientLimit = ws.PatientLimit,
                        Status = ws.Status
                    }).ToList(),
                    startDate = request.StartDate,
                    endDate = request.EndDate,
                    SpecialtyId = d.WorkHistories.FirstOrDefault()?.Specialty.Id ?? Guid.Empty,
                    SpecialtyName = d.WorkHistories.FirstOrDefault()?.Specialty.Name ?? string.Empty
                }).ToList(),
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = doctors.TotalCount
            };
            return response;
        }
    }
}

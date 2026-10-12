using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Queries
{
    // Changed in another branch

    // Use-case: Get a list of doctor schedules
    public record GetDoctorSchedulesQuery(
        Guid? DoctorId,
        DateOnly StartDate,
        DateOnly EndDate
    ) : IRequest<DoctorScheduleDto<WorkScheduleDto>>;
    public class GetDoctorSchedulesHandler : IRequestHandler<GetDoctorSchedulesQuery, DoctorScheduleDto<WorkScheduleDto>>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        public GetDoctorSchedulesHandler(IDoctorRepository doctorRepository, IWorkScheduleRepository workScheduleRepository)
        {
            _doctorRepository = doctorRepository;
            _workScheduleRepository = workScheduleRepository;
        }

        public async Task<DoctorScheduleDto<WorkScheduleDto>> Handle(GetDoctorSchedulesQuery request, CancellationToken cancellationToken)
        {
            if (request.StartDate.AddMonths(1) < request.EndDate)
            {
                throw new ArgumentException("The date range cannot exceed one month");
            }
            var doctor = await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new NotFoundException("Doctor not found");
            }
            var schedules = await _workScheduleRepository.GetPlannedSchedulesByDoctorIdAsync(request.DoctorId, request.StartDate, request.EndDate);

            // Map the doctor's schedules to the DTO
            var scheduleDto = new DoctorScheduleDto<WorkScheduleDto>
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Employee.Person.FullName,
                Schedules = schedules.Select(ws => new WorkScheduleDto
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
                endDate = request.EndDate
            };

            return scheduleDto;
        }
    }
}

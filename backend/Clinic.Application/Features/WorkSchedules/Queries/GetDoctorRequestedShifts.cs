using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.WorkSchedules.Queries
{
    // Changed in another branch


    // Use-case: Get a list of doctor schedules
    public record GetDoctorRequestedShiftsQuery(
        Guid? UserId,
        DateOnly StartDate,
        DateOnly EndDate,
        Guid? DoctorId = null
    ) : IRequest<DoctorScheduleDto<RequestedShiftDto>>;
    public class GetDoctorRequestedShiftsHandler : IRequestHandler<GetDoctorRequestedShiftsQuery, DoctorScheduleDto<RequestedShiftDto>>
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IUserRepository _userRepository;
        public GetDoctorRequestedShiftsHandler(IDoctorRepository doctorRepository, IWorkScheduleRepository workScheduleRepository, IUserRepository userRepository)
        {
            _doctorRepository = doctorRepository;
            _workScheduleRepository = workScheduleRepository;
            _userRepository = userRepository;
        }

        public async Task<DoctorScheduleDto<RequestedShiftDto>> Handle(GetDoctorRequestedShiftsQuery request, CancellationToken cancellationToken)
        {
            if (request.StartDate.AddMonths(1) < request.EndDate)
            {
                throw new ArgumentException("The date range cannot exceed one month");
            }
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new UnauthorizedAccessException();
            }
            if (!(user.UserRoles.Any(ur => ur.Role.Name == "Admin") || (user.UserRoles.Any(ur => ur.Role.Name == "Doctor") && request.DoctorId == null)))
            {
                throw new ForbiddenException("You are not authorized to make this request");
            }
            var doctor = request.DoctorId == null ? user.Person.Employee.Doctor : await _doctorRepository.GetInfoByIdAsync(request.DoctorId);
            if (doctor == null)
            {
                throw new NotFoundException("Doctor not found");
            }
            var schedules = await _workScheduleRepository.GetRequestedSchedulesByDoctorIdAsync(doctor.Id, request.StartDate, request.EndDate);

            // Map the doctor's schedules to the DTO
            var scheduleDto = new DoctorScheduleDto<RequestedShiftDto>
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Employee.Person.FullName,
                Schedules = schedules.Select(sr => new RequestedShiftDto
                {
                    Id = sr.Id,
                    DoctorId = sr.DoctorId,
                    Date = sr.Date,
                    StartTime = sr.ShiftStart,
                    EndTime = sr.ShiftEnd,
                    PatientLimit = sr.PatientLimit,
                    Reason = sr.Reason,
                    Status = sr.Status
                }).ToList(),
                startDate = request.StartDate,
                endDate = request.EndDate
            };

            return scheduleDto;
        }
    }
}

using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Doctors.Commands;
using Clinic.Application.Features.Doctors.Queries;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Features.Queue.Commands;
using Clinic.Application.Features.Queue.Queries;
using Clinic.Application.Features.WorkSchedules.Commands;
using Clinic.Application.Features.WorkSchedules.Queries;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/doctors")]
    public class DoctorsController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IdentityAccountClient _identityAccounts;

        public DoctorsController(IMapper mapper, ISender sender, ICurrentUser currentUser, IdentityAccountClient identityAccounts)
        {
            _mapper = mapper;
            _sender = sender;
            _currentUser = currentUser;
            _identityAccounts = identityAccounts;
        }

        // Public: bệnh nhân (kể cả chưa đăng nhập) cần xem được danh sách bác sĩ/chuyên khoa
        // để chọn bác sĩ trước khi đặt lịch - cùng cách SpecialtiesController đang làm.
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PaginationResponse<DoctorSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll([FromQuery] DoctorsQueryRequest request)
        {
            var query = _mapper.Map<GetDoctorsQuery>(request);
            var result = await _sender.Send(query);
            return Ok(result);
        }

        [HttpGet("available")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<BookingDoctorDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAvailableDoctors([FromQuery] Guid? specialtyId)
        {
            var query = _mapper.Map<GetDoctorsForBookingQuery>(new { SpecialtyId = specialtyId });
            var result = await _sender.Send(query);
            return Ok(result);
        }

        [HttpGet("shifts")]
        //[Authorize(Policy = "Permission:shift.manage")]
        [ProducesResponseType(typeof(PaginationResponse<DoctorScheduleWithSpecialtyDto<WorkScheduleDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllShiftsForAdmin([FromQuery] AdminGetShiftsQuery query)
        {
            var command = _mapper.Map<GetDoctorsWeeklySchedulesQuery>(query);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        // Endpoint này cho phép anonymous truy cập (bệnh nhân chưa đăng nhập cần xem thông tin
        // bác sĩ trước khi đặt lịch), nên KHÔNG được trả các trường nhạy cảm/nội bộ của bác sĩ
        // như DateOfBirth, Address, HireDate (những trường này chỉ có trong DoctorDetailDto).
        // Vì vậy phải map sang DoctorPublicDetailDto (loại bỏ các trường trên) trước khi trả về.
        // Thông tin đầy đủ (DoctorDetailDto) chỉ được trả qua GET /doctors/me, endpoint yêu cầu
        // đăng nhập và chỉ trả về hồ sơ của chính bác sĩ đó.
        [HttpGet("{doctorId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(DoctorPublicDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid doctorId)
        {
            var query = _mapper.Map<GetDoctorQuery>(new { DoctorId = doctorId });
            var result = await _sender.Send(query);
            if (result == null)
            {
                return NotFound();
            }

            var publicResult = new DoctorPublicDetailDto
            {
                Id = result.Id,
                FullName = result.FullName,
                PhoneNumber = result.PhoneNumber,
                Email = result.Email,
                Gender = result.Gender,
                LicenseNumber = result.LicenseNumber,
                Qualification = result.Qualification,
                CurrentSpecialty = result.CurrentSpecialty,
                ExperienceYears = result.ExperienceYears,
                Status = result.Status,
                Biography = result.Biography,
            };

            return Ok(publicResult);
        }

        [HttpGet("{doctorId}/available")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<WorkSchedulesBookingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAvailableSchedule([FromRoute] Guid doctorId, [FromQuery] DateOnly? date)
        {
            var query = _mapper.Map<GetDoctorSchedulesForBookingQuery>(new { DoctorId = doctorId, Date = date ?? DateOnly.FromDateTime(DateTime.Today) });
            var result = await _sender.Send(query);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpGet("me")]
        [Authorize(Policy = "Permission:doctor.view.own")]
        [ProducesResponseType(typeof(DoctorDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetOwnProfile()
        {
            var doctorId = _currentUser.DoctorId;
            var query = _mapper.Map<GetDoctorQuery>(new { DoctorId = doctorId });
            var result = await _sender.Send(query);
            if (result == null)
            {
                return Unauthorized();
            }
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:doctor.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateDoctorRequest request)
        {
            var profileIds = await _sender.Send(new EnsureIdentityRegistrationProfileCommand(request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address), HttpContext.RequestAborted);
            var identityUserId = Guid.NewGuid();
            var roles = new[] { "Doctor", "Patient" };
            var provisioned = await _identityAccounts.ProvisionAsync(identityUserId, profileIds.PersonId,
                request.Username, request.Password, request.FullName, request.PhoneNumber, request.Email,
                request.DateOfBirth, request.Gender, request.Address, roles, profileIds.PatientId, HttpContext.RequestAborted);
            if (provisioned is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity could not provision the doctor account." });
            identityUserId = provisioned.UserId;
            DoctorSummaryDto result;
            try
            {
                if (profileIds.DoctorId is Guid existingDoctorId)
                {
                    var existing = await _sender.Send(new GetDoctorQuery(existingDoctorId), HttpContext.RequestAborted);
                    result = new DoctorSummaryDto
                    {
                        Id = existing.Id, EmployeeId = existing.EmployeeId, FullName = existing.FullName,
                        PhoneNumber = existing.PhoneNumber, Email = existing.Email, Gender = existing.Gender,
                        LicenseNumber = existing.LicenseNumber, Qualification = existing.Qualification,
                        CurrentSpecialty = existing.CurrentSpecialty, ExperienceYears = existing.ExperienceYears,
                        Status = existing.Status
                    };
                }
                else if (profileIds.EmployeeId is not null)
                {
                    result = await _sender.Send(new CreateDoctorFromUserCommand(
                        identityUserId, request.HireDate, request.LicenseNumber, request.Qualification,
                        request.Biography, request.ExperienceYears, request.Status, request.SpecialtyId,
                        request.Email, request.Address), HttpContext.RequestAborted);
                }
                else
                {
                    var command = _mapper.Map<CreateDoctorCommand>(request) with { IdentityUserId = identityUserId, IdentityPatientId = profileIds.PatientId };
                    result = await _sender.Send(command, HttpContext.RequestAborted);
                }
            }
            catch
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The account is inactive and pending doctor profile provisioning." });
            }
            if (!await _identityAccounts.UpdateRolesAsync(identityUserId, roles, profileIds.PatientId, result.EmployeeId, result.Id, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The doctor profile was created, but Identity activation is pending." });
            return CreatedAtAction(nameof(GetById), new { doctorId = result.Id }, result);
        }

        [HttpPost("from-user")]
        [Authorize(Policy = "Permission:doctor.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFromUser([FromBody] CreateDoctorFromUserRequest request)
        {
            var current = await _identityAccounts.GetRolesAsync(request.UserId, HttpContext.RequestAborted);
            if (current is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity account could not be loaded." });
            var roles = current.Value.Roles.Append("Doctor").Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!await _identityAccounts.RolesExistAsync(roles, HttpContext.RequestAborted)) return BadRequest(new { message = "Doctor role is not defined in Identity." });
            var command = _mapper.Map<CreateDoctorFromUserCommand>(request);
            var result = await _sender.Send(command, HttpContext.RequestAborted);
            if (!await _identityAccounts.UpdateRolesAsync(request.UserId, roles, result.PatientId ?? current.Value.PatientId, result.EmployeeId, result.Id, HttpContext.RequestAborted))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The doctor profile was created, but Identity role update is pending." });
            return CreatedAtAction(nameof(GetById), new { doctorId = result.Id }, result);
        }

        [HttpPut("{doctorId}")]
        [Authorize(Policy = "Permission:doctor.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Update([FromRoute] Guid doctorId, [FromBody] UpdateDoctorRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new UpdateDoctorCommand(userId, request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address, request.LicenseNumber, request.Qualification, request.ExperienceYears, request.Biography, doctorId);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:doctor.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateDoctorRequest request)
        {
            var userId = _currentUser.UserId;
            var command = new UpdateDoctorCommand(userId, request.FullName, request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address, request.LicenseNumber, request.Qualification, request.ExperienceYears, request.Biography);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPatch("{doctorId}/status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> UpdateStatus([FromRoute] Guid doctorId, [FromBody] DoctorStatus status)
        {
            var command = new UpdateDoctorStatusCommand(doctorId, status);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpPatch("{doctorId}/specialty")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ChangeSpecialty([FromRoute] Guid doctorId, [FromBody] Guid newSpecialtyId)
        {
            var command = new ChangeSpecialtyCommand(doctorId, newSpecialtyId);
            await _sender.Send(command);
            return NoContent();
        }

        [HttpGet("{doctorId}/shifts")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(DoctorScheduleDto<WorkScheduleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAllShifts([FromRoute] Guid doctorId, [FromQuery] GetShiftsQuery query)
        {
            var command = new GetDoctorSchedulesQuery(doctorId, query.StartDate, query.EndDate);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpPost("{doctorId}/shifts")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(WorkScheduleDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateShift([FromRoute] Guid doctorId, [FromBody] CreateShiftRequest request)
        {
            // In another branch
            var command = new AddDoctorScheduleCommand(doctorId, request.Date, request.StartTime, request.EndTime, request.RecommendedCapacity);
            var result = await _sender.Send(command);
            return Created((string?)null, result);
        }

        [HttpGet("{doctorId}/suggestions")]
        [Authorize(Policy = "Permission:shift.suggestion.manage")]
        [ProducesResponseType(typeof(DoctorScheduleDto<RequestedShiftDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllSuggestions([FromRoute] Guid doctorId, [FromQuery] GetShiftsQuery query)
        {
            var userId = _currentUser.UserId;
            var command = new GetDoctorRequestedShiftsQuery(userId, query.StartDate, query.EndDate, doctorId);
            var schedules = await _sender.Send(command);
            return Ok(schedules);
        }

        [HttpGet("{doctorId}/queue")]
        [Authorize(Policy = "Permission:doctor.queue.view,queue.view")]
        [ProducesResponseType(typeof(List<QueueTicketDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetQueue([FromRoute] Guid doctorId)
        {
            var result = await _sender.Send(new GetQueueByDoctorQuery(doctorId));
            return Ok(result);
        }

        [HttpPost("{doctorId}/queue/next")]
        [Authorize(Policy = "Permission:queue.call-next")]
        [ProducesResponseType(typeof(QueueTicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
        {
            var result = await _sender.Send(new CallNextQueueCommand(doctorId));
            return Ok(result);
        }
    }
}

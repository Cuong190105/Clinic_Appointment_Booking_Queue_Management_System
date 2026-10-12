using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Admin.Queries;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Users.Queries;
using MapsterMapper;
using MediatR;
using Clinic.Application.Features;
using Clinic.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;

        public AdminController(ISender sender, IMapper mapper)
        {
            _sender = sender;
            _mapper = mapper;
        }

        [HttpGet("ping")]
        [Authorize(Roles = "Admin")]
        public IActionResult Ping()
        {
            return Ok(new { message = "pong" });
        }

        [HttpGet("roles")]
        [Authorize(Policy = "Permission:role.manage")]
        public IActionResult Roles()
        {
            // placeholder: in real app return roles/permissions management data
            return Ok(new { message = "roles endpoint - requires role.manage permission" });
        }

        [HttpGet("users")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(PaginationResponse<UserSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPagedUsers([FromQuery] PagedUsersQueryRequest request)
        {
            var command = _mapper.Map<GetPagedUsersQuery>(request);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("users/all-brief")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(List<UserBriefDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _sender.Send(new GetAllUsersQuery());
            return Ok(result);
        }

        [HttpGet("users/{userId}")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            var command = new GetUserQuery(userId);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("appointments/summary")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(TotalAppointmentSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAppointmentSummary([FromQuery] GetAppointmentSummaryQueryRequest request)
        {
            var command = _mapper.Map<GetAppointmentSummaryQueryRequest, GetTotalAppointmentSummaryQuery>(request);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("appointments/{appointmentId}/history")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AppointmentChangelogDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAppointmentChangelog(Guid appointmentId)
        {
            var command = new GetAppointmentChangelogQuery(appointmentId);
            var result = await _sender.Send(command);
            return Ok(result);
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDashboardData()
        {
            // placeholder: implement dashboard data retrieval
            var result = await _sender.Send(new GetDashboardDataQuery());
            return Ok(result);
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStatistics([FromQuery] Period period)
        {
            // placeholder: implement dashboard data retrieval
            var result = await _sender.Send(new GetStatisticsDataQuery(period));
            return Ok(result);
        }

        [HttpGet("appointments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAppointmentsByDate([FromQuery] GetAppointmentsByDateRequest request)
        {
            // placeholder: implement dashboard data retrieval
            var command = _mapper.Map<GetAppointmentByDateQuery>(request);
            var result = await _sender.Send(command);
            return Ok(result);
        }
    }
}

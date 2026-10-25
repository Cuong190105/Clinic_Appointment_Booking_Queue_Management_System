using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Admin.Queries;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.Features.Users.Queries;
using MapsterMapper;
using MediatR;
using Clinic.Application.Features;
using Clinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly HttpClient _identityClient;
        private readonly string? _identityServiceToken;
        private readonly IdentityAccountClient _identityAccounts;
        private readonly AppointmentServiceClient _appointmentClient;
        private readonly SchedulingManagementClient _scheduling;

        public AdminController(ISender sender, IMapper mapper, IHttpClientFactory httpClientFactory, IConfiguration configuration, IdentityAccountClient identityAccounts, AppointmentServiceClient appointmentClient, SchedulingManagementClient scheduling)
        {
            _sender = sender;
            _mapper = mapper;
            _identityClient = httpClientFactory.CreateClient("Identity");
            _identityServiceToken = configuration["Identity:ServiceToken"];
            _identityAccounts = identityAccounts;
            _appointmentClient = appointmentClient;
            _scheduling = scheduling;
        }

        [HttpGet("ping")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Ping()
        {
            return Ok(new { message = "pong" });
        }

        [HttpGet("roles")]
        [Authorize(Policy = "Permission:role.manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Roles()
        {
            var roles = await _identityAccounts.GetRoleCatalogAsync(null, HttpContext.RequestAborted);
            return roles is null ? StatusCode(StatusCodes.Status503ServiceUnavailable) : Ok(roles);
        }

        [HttpGet("users")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(PaginationResponse<UserSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPagedUsers([FromQuery] PagedUsersQueryRequest request)
        {
            var query = $"?search={Uri.EscapeDataString(request.Search ?? string.Empty)}&role={Uri.EscapeDataString(request.Role ?? string.Empty)}" +
                $"&sortBy={Uri.EscapeDataString(request.SortBy ?? string.Empty)}&gender={Uri.EscapeDataString(request.Gender?.ToString() ?? string.Empty)}" +
                $"&orderBy={Uri.EscapeDataString(request.OrderBy ?? string.Empty)}&pageNumber={request.PageNumber ?? 1}&pageSize={request.PageSize ?? 10}";
            using var response = await SendIdentityRequest(HttpMethod.Get, $"v1/internal/users{query}");
            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) return StatusCode(503);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<IdentityPagedAdminUsers>(HttpContext.RequestAborted);
            if (page is null) return StatusCode(502);
            return Ok(new PaginationResponse<UserSummaryDto>
            {
                Items = page.Items.Select(ToSummary).ToList(),
                TotalCount = page.TotalCount,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            });
        }

        [HttpGet("users/all-brief")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(List<UserBriefDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = new List<UserBriefDto>();
            var pageNumber = 1;
            while (true)
            {
                using var response = await SendIdentityRequest(HttpMethod.Get, $"v1/internal/users?pageNumber={pageNumber}&pageSize=100&sortBy=username");
                if (!response.IsSuccessStatusCode) return StatusCode(response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ? 503 : (int)response.StatusCode);
                var page = await response.Content.ReadFromJsonAsync<IdentityPagedAdminUsers>(HttpContext.RequestAborted);
                if (page is null) return StatusCode(502);
                users.AddRange(page.Items.Select(x => new UserBriefDto { Id = x.UserId, Username = x.Username, FullName = x.FullName }));
                if (users.Count >= page.TotalCount || page.Items.Count == 0) break;
                pageNumber++;
            }
            return Ok(users);
        }

        [HttpGet("doctors/brief")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDoctorBriefs()
        {
            var response = await _scheduling.SendAsync(HttpContext, HttpMethod.Get, "doctors/brief", null, HttpContext.RequestAborted);
            return response.ToActionResult();
        }

        [HttpGet("users/{userId}")]
        [Authorize(Policy = "Permission:user.manage")]
        [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(Guid userId)
        {
            using var response = await SendIdentityRequest(HttpMethod.Get, $"v1/internal/users/{userId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return NotFound();
            if (!response.IsSuccessStatusCode) return StatusCode(response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ? 503 : (int)response.StatusCode);
            var user = await response.Content.ReadFromJsonAsync<IdentityAdminUserDetail>(HttpContext.RequestAborted);
            if (user is null) return StatusCode(502);
            return Ok(new UserDetailDto
            {
                UserId = user.UserId, PersonId = user.PersonId, Username = user.Username,
                FullName = user.FullName, PhoneNumber = user.PhoneNumber, Email = user.Email,
                Gender = Enum.TryParse<Gender>(user.Gender, true, out var gender) ? gender : Gender.Other,
                Address = user.Address, DateOfBirth = user.DateOfBirth, Roles = user.Roles.ToList(),
                IsActive = user.IsActive, CreatedAt = user.CreatedAt
            });
        }

        [HttpGet("appointments/summary")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(TotalAppointmentSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAppointmentSummary([FromQuery] GetAppointmentSummaryQueryRequest request)
        {
            var queryString = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value : string.Empty;
            return await _appointmentClient.ProxyAsync(HttpContext, $"admin/appointments/summary{queryString}", HttpMethod.Get);
        }

        [HttpGet("appointments/{appointmentId}/history")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AppointmentChangelogDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAppointmentChangelog(Guid appointmentId)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, $"appointments/{appointmentId}/history", HttpMethod.Get);
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(DashboardDataDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDashboardData()
        {
            // placeholder: implement dashboard data retrieval
            var result = await _sender.Send(new GetDashboardDataQuery());
            return Ok(result);
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(StatisticsDataDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics([FromQuery] Period period)
        {
            // placeholder: implement dashboard data retrieval
            var result = await _sender.Send(new GetStatisticsDataQuery(period));
            return Ok(result);
        }

        private async Task<HttpResponseMessage> SendIdentityRequest(HttpMethod method, string path)
        {
            if (string.IsNullOrWhiteSpace(_identityServiceToken))
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Identity-Service-Token", _identityServiceToken);
            try { return await _identityClient.SendAsync(request, HttpContext.RequestAborted); }
            catch (HttpRequestException) { return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable); }
        }

        private static UserSummaryDto ToSummary(IdentityAdminUserSummary user) => new()
        {
            UserId = user.UserId, Username = user.Username, FullName = user.FullName,
            PhoneNumber = user.PhoneNumber, Email = user.Email,
            Gender = Enum.TryParse<Gender>(user.Gender, true, out var gender) ? gender : Gender.Other,
            Roles = user.Roles.ToList(), IsActive = user.IsActive, CreatedAt = user.CreatedAt
        };

        [HttpGet("appointments")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PaginationResponse<AppointmentExtended>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAppointmentsByDate([FromQuery] GetAppointmentsByDateRequest request)
        {
            var queryString = HttpContext.Request.QueryString.HasValue ? HttpContext.Request.QueryString.Value : string.Empty;
            return await _appointmentClient.ProxyAsync(HttpContext, $"admin/appointments{queryString}", HttpMethod.Get);
        }
    }
}

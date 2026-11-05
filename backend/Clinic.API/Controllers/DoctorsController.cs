namespace Clinic.API.Controllers;

using Clinic.API.Models;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly SchedulingManagementClient _scheduling;
    private readonly QueueServiceClient _queueClient;

    public DoctorsController(
        SchedulingManagementClient scheduling,
        QueueServiceClient queueClient)
    {
        _scheduling = scheduling;
        _queueClient = queueClient;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors", HttpMethod.Get);
    }

    [HttpGet("available")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailable()
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/available", HttpMethod.Get);
    }

    [HttpGet("dashboard-stats")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats()
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/dashboard-stats", HttpMethod.Get);
    }

    [HttpGet("shifts")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShifts()
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/shifts", HttpMethod.Get);
    }

    [HttpGet("me")]
    [Authorize(Policy = "Permission:doctor.view.own")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/me", HttpMethod.Get);
    }

    [HttpGet("{doctorId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}", HttpMethod.Get);
    }

    [HttpGet("{doctorId:guid}/available")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSlots([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/available", HttpMethod.Get);
    }

    [HttpGet("{doctorId:guid}/shifts")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorShifts([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/shifts", HttpMethod.Get);
    }

    [HttpGet("{doctorId:guid}/suggestions")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorSuggestions([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/suggestions", HttpMethod.Get);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:doctor.create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] CreateDoctorRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors", HttpMethod.Post);
    }

    [HttpPost("from-user")]
    [Authorize(Policy = "Permission:doctor.create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateFromUser([FromBody] CreateDoctorFromUserRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/from-user", HttpMethod.Post);
    }

    [HttpPost("{doctorId:guid}/shifts")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateShift([FromRoute] Guid doctorId, [FromBody] CreateShiftRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/shifts", HttpMethod.Post);
    }

    [HttpPut("{doctorId:guid}")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid doctorId, [FromBody] UpdateDoctorRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}", HttpMethod.Put);
    }

    [HttpPut("me")]
    [Authorize(Policy = "Permission:doctor.edit.own")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateDoctorRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "doctors/me", HttpMethod.Put);
    }

    [HttpPatch("{doctorId:guid}/status")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PatchStatus([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/status", new HttpMethod("PATCH"));
    }

    [HttpPost("{doctorId:guid}/status")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PostStatus([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/status", HttpMethod.Post);
    }

    [HttpPatch("{doctorId:guid}/specialty")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PatchSpecialty([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/specialty", new HttpMethod("PATCH"));
    }

    [HttpPost("{doctorId:guid}/specialty")]
    [Authorize(Policy = "Permission:doctor.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PostSpecialty([FromRoute] Guid doctorId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"doctors/{doctorId}/specialty", HttpMethod.Post);
    }

    [HttpGet("{doctorId:guid}/queue")]
    [Authorize(Policy = "Permission:doctor.queue.view,queue.view")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue([FromRoute] Guid doctorId)
    {
        var queryStr = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return await _queueClient.ProxyAsync(HttpContext, $"/doctors/{doctorId}/queue{queryStr}", HttpMethod.Get);
    }

    [HttpPost("{doctorId:guid}/queue/next")]
    [Authorize(Policy = "Permission:queue.call-next")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CallNext([FromRoute] Guid doctorId)
    {
        return await _queueClient.ProxyAsync(HttpContext, $"/doctors/{doctorId}/queue/next", HttpMethod.Post);
    }
}

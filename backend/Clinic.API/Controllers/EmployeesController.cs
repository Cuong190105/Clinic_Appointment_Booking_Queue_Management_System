namespace Clinic.API.Controllers;

using Clinic.API.Models;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("/employees")]
public class EmployeesController : ControllerBase
{
    private readonly SchedulingManagementClient _scheduling;

    public EmployeesController(SchedulingManagementClient scheduling)
    {
        _scheduling = scheduling;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:employee.view,employee.view.all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return await _scheduling.ProxyAsync(HttpContext, "employees", HttpMethod.Get);
    }

    [HttpGet("me")]
    [Authorize(Policy = "Permission:employee.view.own")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        return await _scheduling.ProxyAsync(HttpContext, "employees/me", HttpMethod.Get);
    }

    [HttpGet("{employeeId:guid}")]
    [Authorize(Policy = "Permission:employee.view")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid employeeId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"employees/{employeeId}", HttpMethod.Get);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:employee.create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "employees", HttpMethod.Post);
    }

    [HttpPost("from-user")]
    [Authorize(Policy = "Permission:employee.create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateFromUser([FromBody] CreateEmployeeFromUserRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "employees/from-user", HttpMethod.Post);
    }

    [HttpPut("{employeeId:guid}")]
    [Authorize(Policy = "Permission:employee.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid employeeId, [FromBody] AdminUpdateEmployeeRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"employees/{employeeId}", HttpMethod.Put);
    }

    [HttpPut("me")]
    [Authorize(Policy = "Permission:employee.edit.own")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateEmployeeRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "employees/me", HttpMethod.Put);
    }

    [HttpPost("{employeeId:guid}/status")]
    [Authorize(Policy = "Permission:employee.edit.any")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus([FromRoute] Guid employeeId)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"employees/{employeeId}/status", HttpMethod.Post);
    }
}

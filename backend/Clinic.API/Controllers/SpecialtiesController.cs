namespace Clinic.API.Controllers;

using Clinic.API.Models;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("/specialties")]
public class SpecialtiesController : ControllerBase
{
    private readonly SchedulingManagementClient _scheduling;

    public SpecialtiesController(SchedulingManagementClient scheduling)
    {
        _scheduling = scheduling;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return await _scheduling.ProxyAsync(HttpContext, "specialties", HttpMethod.Get);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"specialties/{id}", HttpMethod.Get);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:specialty.manage")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateSpecialtyRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "specialties", HttpMethod.Post);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:specialty.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSpecialtyRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"specialties/{id}", HttpMethod.Put);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Permission:specialty.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchStatus([FromRoute] Guid id)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"specialties/{id}/status", new HttpMethod("PATCH"));
    }
}

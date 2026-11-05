namespace Clinic.API.Controllers;

using Clinic.API.Models;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("/shifts")]
public class ShiftsController : ControllerBase
{
    private readonly SchedulingManagementClient _scheduling;

    public ShiftsController(SchedulingManagementClient scheduling)
    {
        _scheduling = scheduling;
    }

    [HttpGet]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return await _scheduling.ProxyAsync(HttpContext, "shifts", HttpMethod.Get);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateShiftRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/{id}", HttpMethod.Put);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, [FromBody] CancelShiftRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/{id}/cancel", HttpMethod.Post);
    }

    [HttpGet("suggestions")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions()
    {
        return await _scheduling.ProxyAsync(HttpContext, "shifts/suggestions", HttpMethod.Get);
    }

    [HttpPost("suggestions")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSuggestion([FromBody] CreateShiftSuggestionRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, "shifts/suggestions", HttpMethod.Post);
    }

    [HttpPatch("suggestions/{id:guid}")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchSuggestion([FromRoute] Guid id, [FromBody] UpdateShiftSuggestionRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/suggestions/{id}", new HttpMethod("PATCH"));
    }

    [HttpPost("suggestions/{id:guid}/cancel")]
    [Authorize(Roles = "Doctor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSuggestion([FromRoute] Guid id, [FromBody] CancelShiftRequest request)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/suggestions/{id}/cancel", HttpMethod.Post);
    }

    [HttpPost("suggestions/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSuggestion([FromRoute] Guid id)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/suggestions/{id}/approve", HttpMethod.Post);
    }

    [HttpPost("suggestions/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSuggestion([FromRoute] Guid id)
    {
        return await _scheduling.ProxyAsync(HttpContext, $"shifts/suggestions/{id}/reject", HttpMethod.Post);
    }
}

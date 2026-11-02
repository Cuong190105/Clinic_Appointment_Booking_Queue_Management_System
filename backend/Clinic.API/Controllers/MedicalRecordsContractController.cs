using Clinic.API.Models;
using Clinic.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/medical-records")]
public sealed class MedicalRecordsContractController : ControllerBase
{
    private readonly MedicalRecordsServiceClient _medicalRecordsClient;

    public MedicalRecordsContractController(MedicalRecordsServiceClient medicalRecordsClient)
    {
        _medicalRecordsClient = medicalRecordsClient;
    }

    [HttpPost("encounters/{appointmentId:guid}/draft")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraft([FromRoute] Guid appointmentId, [FromBody] DraftMedicalRecordRequest request)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/encounters/{appointmentId}/draft",
            HttpMethod.Post,
            request,
            HttpContext.RequestAborted);
    }

    [HttpGet("encounters/{appointmentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByEncounter([FromRoute] Guid appointmentId)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/encounters/{appointmentId}",
            HttpMethod.Get,
            ct: HttpContext.RequestAborted);
    }

    [HttpPost("reports/{reportId:guid}/finalize")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finalize(
        [FromRoute] Guid reportId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/{reportId}/finalize",
            HttpMethod.Post,
            ct: HttpContext.RequestAborted);
    }

    [HttpPost("reports/{reportId:guid}/amendments")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Amend(
        [FromRoute] Guid reportId,
        [FromBody] AmendmentMedicalRecordRequest request)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/{reportId}/amendments",
            HttpMethod.Post,
            request,
            HttpContext.RequestAborted);
    }
}

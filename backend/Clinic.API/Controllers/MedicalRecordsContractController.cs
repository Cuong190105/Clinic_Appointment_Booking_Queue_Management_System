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

    [HttpPost("encounters/{visitId:guid}/draft")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraft([FromRoute] Guid visitId, [FromBody] DraftMedicalRecordRequest request)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/encounters/{visitId}/draft",
            HttpMethod.Post,
            request,
            HttpContext.RequestAborted);
    }

    [HttpGet("encounters/{visitId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByEncounter([FromRoute] Guid visitId)
    {
        return await _medicalRecordsClient.ProxyAsync(
            HttpContext,
            $"medical-records/encounters/{visitId}",
            HttpMethod.Get,
            ct: HttpContext.RequestAborted);
    }

    [HttpPost("reports/{reportId:guid}/finalize")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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

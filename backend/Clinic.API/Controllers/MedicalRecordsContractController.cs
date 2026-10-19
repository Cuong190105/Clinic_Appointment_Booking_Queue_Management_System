using Clinic.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers;

[ApiController]
[Route("/medical-records")]
public sealed class MedicalRecordsContractController : ControllerBase
{
    [HttpPost("encounters/{visitId:guid}/draft")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult SaveDraft([FromRoute] Guid visitId, [FromBody] DraftMedicalRecordRequest request)
    {
        return StatusCode(StatusCodes.Status201Created,
            new MedicalRecordContractResponse(Guid.Empty, visitId, "Draft", DateTime.UtcNow));
    }

    [HttpGet("encounters/{visitId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetByEncounter([FromRoute] Guid visitId)
    {
        return Ok(new MedicalRecordContractResponse(Guid.Empty, visitId, "Draft", DateTime.UtcNow));
    }

    [HttpPost("reports/{reportId:guid}/finalize")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Finalize(
        [FromRoute] Guid reportId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        return Ok(new MedicalRecordContractResponse(reportId, Guid.Empty, "Finalized", DateTime.UtcNow));
    }

    [HttpPost("reports/{reportId:guid}/amendments")]
    [Authorize]
    [ProducesResponseType(typeof(MedicalRecordContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Amend(
        [FromRoute] Guid reportId,
        [FromBody] AmendmentMedicalRecordRequest request)
    {
        return StatusCode(StatusCodes.Status201Created,
            new MedicalRecordContractResponse(reportId, Guid.Empty, "Amended", DateTime.UtcNow));
    }
}

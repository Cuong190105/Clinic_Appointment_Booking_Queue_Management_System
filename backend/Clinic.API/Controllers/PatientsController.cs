using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/patients")]
    public class PatientsController : ControllerBase
    {
        private readonly MedicalRecordsServiceClient _medicalRecordsClient;
        private readonly AppointmentServiceClient _appointmentClient;

        public PatientsController(
            MedicalRecordsServiceClient medicalRecordsClient,
            AppointmentServiceClient appointmentClient)
        {
            _medicalRecordsClient = medicalRecordsClient;
            _appointmentClient = appointmentClient;
        }

        [HttpGet]
        [Authorize(Policy = "Permission:patient.view.any")]
        [ProducesResponseType(typeof(PaginationResponse<PatientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPaged([FromQuery] GetPagedPatientsRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "patients", HttpMethod.Get);
        }

        [HttpGet("{patientId}")]
        [Authorize(Policy = "Permission:patient.view.any,patient.view.own")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPatient([FromRoute] Guid patientId)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, $"patients/{patientId}", HttpMethod.Get);
        }

        [HttpGet("{patientId}/appointments")]
        [HttpGet("{patientId}/appointments/upcoming")]
        [Authorize(Policy = "Permission:patient.view.any")]
        [ProducesResponseType(typeof(List<AppointmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPatientAppointments([FromRoute] Guid patientId, [FromQuery] string? category)
        {
            var query = string.IsNullOrWhiteSpace(category) ? "upcoming" : Uri.EscapeDataString(category);
            return await _appointmentClient.ProxyAsync(
                HttpContext,
                $"patients/{patientId}/appointments?category={query}",
                HttpMethod.Get,
                ct: HttpContext.RequestAborted);
        }

        [HttpGet("me")]
        [Authorize(Policy = "Permission:patient.view.own")]
        [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOwnPatient()
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "patients/me", HttpMethod.Get);
        }

        [HttpPut("me")]
        [Authorize(Policy = "Permission:patient.edit.own")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateOwnPatient([FromBody] UpdatePatientRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "patients/me", HttpMethod.Put, request);
        }

        [HttpPost]
        [Authorize(Policy = "Permission:patient.create.any")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreatePatientRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "patients", HttpMethod.Post, request);
        }

        [HttpPost("add")]
        [Authorize(Policy = "Permission:patient.create.any")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateViaAdd([FromBody] CreatePatientRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "patients/add", HttpMethod.Post, request);
        }

        [HttpPut("{patientId}")]
        [Authorize(Policy = "Permission:patient.edit.any")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePatient([FromRoute] Guid patientId, [FromBody] UpdatePatientRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, $"patients/{patientId}", HttpMethod.Put, request);
        }
    }
}

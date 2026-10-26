using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/medical-reports")]
    public class MedicalReportsController : ControllerBase
    {
        private readonly MedicalRecordsServiceClient _medicalRecordsClient;

        public MedicalReportsController(MedicalRecordsServiceClient medicalRecordsClient)
        {
            _medicalRecordsClient = medicalRecordsClient;
        }

        /// <summary>
        /// Tìm kiếm và duyệt danh sách hồ sơ khám bệnh (phân trang, lọc theo từ khóa, trạng thái, ngày).
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own")]
        [ProducesResponseType(typeof(PaginationResponse<MedicalReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetReports([FromQuery] GetMedicalReportsRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "medical-records", HttpMethod.Get);
        }

        /// <summary>
        /// Tạo mới hồ sơ khám bệnh (Draft hoặc Finalized) cho ca khám hiện tại.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "Permission:medical-report.create")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Save([FromBody] SaveMedicalReportRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                "medical-records",
                HttpMethod.Post,
                request,
                HttpContext.RequestAborted);
        }

        /// <summary>
        /// Cập nhật hồ sơ khám bệnh bản nháp (Draft) hoặc chốt hồ sơ (Finalize).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = "Permission:medical-report.create")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDraft([FromRoute] Guid id, [FromBody] UpdateMedicalReportRequest request)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/{id}",
                HttpMethod.Put,
                request,
                HttpContext.RequestAborted);
        }

        /// <summary>
        /// Lấy chi tiết hồ sơ khám bệnh theo ID.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = "Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/{id}",
                HttpMethod.Get,
                ct: HttpContext.RequestAborted);
        }

        /// <summary>
        /// Lấy hồ sơ khám bệnh theo mã lịch hẹn (AppointmentId).
        /// </summary>
        [HttpGet("appointment/{appointmentId}")]
        [Authorize(Policy = "Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByAppointment([FromRoute] Guid appointmentId)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/appointment/{appointmentId}",
                HttpMethod.Get,
                ct: HttpContext.RequestAborted);
        }

        /// <summary>
        /// Chốt hồ sơ khám bệnh và hoàn tất ca khám.
        /// </summary>
        [HttpPost("{id}/finalize")]
        [Authorize(Policy = "Permission:medical-report.create")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FinalizeReport([FromRoute] Guid id)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/{id}/finalize",
                HttpMethod.Post,
                ct: HttpContext.RequestAborted);
        }

        /// <summary>
        /// Bổ sung amendment cho hồ sơ đã Finalized.
        /// </summary>
        [HttpPost("appointments/{appointmentId}/amendments")]
        [Authorize(Policy = "Permission:medical-report.create")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateAmendment([FromRoute] Guid appointmentId)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/appointments/{appointmentId}/amendments",
                HttpMethod.Post,
                ct: HttpContext.RequestAborted);
        }

        /// <summary>
        /// Tra cứu nhật ký kiểm toán truy cập y tế (Audit Trail) cho Admin.
        /// </summary>
        [HttpGet("appointments/{appointmentId}/audits")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAudits([FromRoute] Guid appointmentId)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"medical-records/appointments/{appointmentId}/audits",
                HttpMethod.Get,
                ct: HttpContext.RequestAborted);
        }

        /// <summary>
        /// Bác sĩ hoặc Admin tra cứu lịch sử các lần khám trước của một bệnh nhân.
        /// </summary>
        [HttpGet("/patients/{patientId}/medical-history")]
        [Authorize(Policy = "Permission:patient-history.view.related,patient-history.view.any")]
        [ProducesResponseType(typeof(List<MedicalReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPatientMedicalHistory([FromRoute] Guid patientId)
        {
            return await _medicalRecordsClient.ProxyAsync(
                HttpContext,
                $"patients/{patientId}/medical-history",
                HttpMethod.Get,
                ct: HttpContext.RequestAborted);
        }
    }
}

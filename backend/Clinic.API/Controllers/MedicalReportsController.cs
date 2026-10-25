using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.MedicalReports.Commands;
using Clinic.Application.Features.MedicalReports.Queries;
using MediatR;
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
        private readonly ISender _sender;
        private readonly AppointmentServiceClient _appointmentClient;
        private readonly QueueServiceClient _queueClient;

        public MedicalReportsController(ISender sender, AppointmentServiceClient appointmentClient, QueueServiceClient queueClient)
        {
            _sender = sender;
            _appointmentClient = appointmentClient;
            _queueClient = queueClient;
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
            var query = new GetMedicalReportsQuery(
                request.Search,
                request.Status,
                request.FromDate,
                request.ToDate,
                request.PageNumber ?? 1,
                request.PageSize ?? 10
            );

            var result = await _sender.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Tạo mới hoặc cập nhật hồ sơ khám bệnh (Draft hoặc Finalized) cho ca khám hiện tại.
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
            var ct = HttpContext.RequestAborted;
            var appointmentLookup = await _appointmentClient.GetResultAsync<AppointmentReportSnapshot>(
                HttpContext,
                $"appointments/{request.AppointmentId}",
                ct);
            if (appointmentLookup.StatusCode != System.Net.HttpStatusCode.OK || appointmentLookup.Value is null)
                return StatusCode((int)appointmentLookup.StatusCode);

            var queueLookup = await _queueClient.GetTicketByAppointmentAsync(HttpContext, request.AppointmentId, ct);
            if (queueLookup.StatusCode is not (System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.NotFound))
                return StatusCode((int)queueLookup.StatusCode);

            var command = new SaveMedicalReportCommand(
                request.AppointmentId,
                request.Symptoms,
                request.Diagnosis,
                request.Prescription,
                request.Notes,
                request.IsFinalize,
                appointmentLookup.Value,
                queueLookup.Value?.Status,
                queueLookup.Value?.ExamStartedAt
            );

            var result = await _sender.Send(command);
            if (request.IsFinalize)
            {
                var completionResult = await CompleteQueueExamIfInProgress(request.AppointmentId, ct);
                if (completionResult is not null) return completionResult;
            }
            return Ok(result);
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
            var existingReport = await _sender.Send(new GetMedicalReportByIdQuery(id), HttpContext.RequestAborted);
            var command = new UpdateDraftMedicalReportCommand(
                id,
                request.Symptoms,
                request.Diagnosis,
                request.Prescription,
                request.Notes,
                request.IsFinalize
            );

            var result = await _sender.Send(command);
            if (request.IsFinalize)
            {
                var completionResult = await CompleteQueueExamIfInProgress(existingReport.AppointmentId, HttpContext.RequestAborted);
                if (completionResult is not null) return completionResult;
            }
            return Ok(result);
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
            var result = await _sender.Send(new GetMedicalReportByIdQuery(id));
            return Ok(result);
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
            var result = await _sender.Send(new GetMedicalReportByAppointmentQuery(appointmentId));
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
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
            var existingReport = await _sender.Send(new GetMedicalReportByIdQuery(id), HttpContext.RequestAborted);
            var result = await _sender.Send(new FinalizeMedicalReportCommand(id));
            var completionResult = await CompleteQueueExamIfInProgress(existingReport.AppointmentId, HttpContext.RequestAborted);
            if (completionResult is not null) return completionResult;
            return Ok(result);
        }

        private async Task<IActionResult?> CompleteQueueExamIfInProgress(Guid appointmentId, CancellationToken ct)
        {
            ServiceReadResult<QueueTicketLookup> lookup;
            try
            {
                lookup = await _queueClient.GetTicketByAppointmentAsync(HttpContext, appointmentId, ct);
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (lookup.StatusCode == System.Net.HttpStatusCode.NotFound || lookup.Value is null)
                return null;
            if (lookup.StatusCode != System.Net.HttpStatusCode.OK)
                return StatusCode((int)lookup.StatusCode);
            if (!string.Equals(lookup.Value.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var result = await _queueClient.ProxyAsync(HttpContext, $"queue/{lookup.Value.Id}/complete-exam", HttpMethod.Post, ct: ct);
                if (result is ObjectResult objectResult && objectResult.StatusCode is >= 400)
                    return result;
                if (result is ContentResult contentResult && contentResult.StatusCode is >= 400)
                    return result;
                return null;
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
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
            var result = await _sender.Send(new GetPatientMedicalHistoryForDoctorQuery(patientId));
            return Ok(result);
        }
    }
}

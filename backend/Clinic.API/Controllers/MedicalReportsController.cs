using Clinic.API.Models;
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

        public MedicalReportsController(ISender sender)
        {
            _sender = sender;
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
            var command = new SaveMedicalReportCommand(
                request.QueueTicketId,
                request.Symptoms,
                request.Diagnosis,
                request.Prescription,
                request.Notes,
                request.IsFinalize
            );

            var result = await _sender.Send(command);
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
        /// Lấy hồ sơ khám bệnh theo mã vé hàng đợi (QueueTicketId).
        /// </summary>
        [HttpGet("ticket/{queueTicketId}")]
        [Authorize(Policy = "Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own")]
        [ProducesResponseType(typeof(MedicalReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByTicket([FromRoute] Guid queueTicketId)
        {
            var result = await _sender.Send(new GetMedicalReportByTicketQuery(queueTicketId));
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
            var result = await _sender.Send(new FinalizeMedicalReportCommand(id));
            return Ok(result);
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

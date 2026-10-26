using Clinic.API.Models;
using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Patients.Commands;
using Clinic.Application.Interfaces;
using MapsterMapper;
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
    [Route("/receptionist")]
    public class ReceptionistController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;
        private readonly AppointmentServiceClient _appointmentClient;

        public ReceptionistController(
            ISender sender,
            IMapper mapper,
            ICurrentUser currentUser,
            AppointmentServiceClient appointmentClient)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
            _appointmentClient = appointmentClient;
        }

        // Lấy danh sách bác sĩ đang có ca trực hôm nay (theo chuyên khoa)
        [HttpGet("working-doctors")]
        [Authorize(Roles = "Receptionist,Admin")]
        [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkingDoctors([FromQuery] Guid? specialtyId)
        {
            var path = specialtyId.HasValue
                ? $"receptionist/working-doctors?specialtyId={specialtyId.Value}"
                : "receptionist/working-doctors";
            return await _appointmentClient.ProxyAsync(HttpContext, path, HttpMethod.Get);
        }

        [HttpGet("queue-doctors")]
        [Authorize(Roles = "Receptionist,Admin")]
        [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetQueueDoctors()
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/queue-doctors", HttpMethod.Get);
        }

        // Đăng ký khám ngay (Walk-in) cho bệnh nhân tại quầy: tự động chọn bác sĩ ít tải nhất nếu không chọn, xác nhận, tự động check-in và sinh số thứ tự
        [HttpPost("appointments/walk-in")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateWalkInAppointment([FromBody] ReceptionistWalkInRequest request)
        {
            if (!Request.Headers.ContainsKey("Idempotency-Key"))
                Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString("N");
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/appointments/walk-in", HttpMethod.Post, request);
        }

        // For receptionist only
        [HttpPost("appointments")]
        [Authorize(Policy = "Permission:appointment.create")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateAppointmentForPatient([FromBody] ReceptionistCreateAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/appointments", HttpMethod.Post, request);
        }

        [HttpGet("appointments/pending")]
        [Authorize(Roles = "Receptionist")]
        [ProducesResponseType(typeof(List<AppointmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPendingAppointments([FromQuery] PendingAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, "receptionist/appointments/pending", HttpMethod.Get);
        }

        // For receptionist only
        [HttpPatch("appointments/{appointmentId}")]
        [Authorize(Policy = "Permission:appointment.update")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Patch([FromRoute] Guid appointmentId, [FromBody] UpdateAppointmentRequest request)
        {
            return await _appointmentClient.ProxyAsync(HttpContext, $"receptionist/appointments/{appointmentId}", new HttpMethod("PATCH"), request);
        }


    }
}

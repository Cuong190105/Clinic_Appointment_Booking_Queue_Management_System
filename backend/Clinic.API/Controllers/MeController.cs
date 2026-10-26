using Clinic.API.Services;
using Clinic.Application.Contracts;
using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    /// <summary>
    /// Các API tự-phục-vụ (self-service) cho bệnh nhân đang đăng nhập: theo dõi vị trí hàng đợi
    /// và tra cứu lịch sử khám bệnh của chính mình.
    /// </summary>
    [ApiController]
    [Route("/me")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly QueueServiceClient _queueClient;
        private readonly MedicalRecordsServiceClient _medicalRecordsClient;

        public MeController(ISender sender, QueueServiceClient queueClient, MedicalRecordsServiceClient medicalRecordsClient)
        {
            _sender = sender;
            _queueClient = queueClient;
            _medicalRecordsClient = medicalRecordsClient;
        }

        /// <summary>
        /// Trạng thái hàng đợi hiện tại của bệnh nhân (vị trí, thời gian chờ ước tính).
        /// Trả về mảng - có thể có nhiều phần tử nếu bệnh nhân đã check-in nhiều hơn 1 lịch hẹn
        /// trong cùng ngày. Trả về 204 nếu bệnh nhân chưa check-in lịch hẹn nào trong ngày hôm nay.
        /// </summary>
        [HttpGet("queue-status")]
        [ProducesResponseType(typeof(List<MyQueueStatusDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> GetMyQueueStatus()
        {
            return await _queueClient.ProxyAsync(HttpContext, "/me/queue-status", HttpMethod.Get);
        }

        /// <summary>Lịch sử khám bệnh (các hồ sơ đã chốt) của bệnh nhân đang đăng nhập.</summary>
        [HttpGet("medical-history")]
        [ProducesResponseType(typeof(List<MedicalReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyMedicalHistory()
        {
            return await _medicalRecordsClient.ProxyAsync(HttpContext, "me/medical-history", HttpMethod.Get);
        }

        [Authorize]
        [HttpGet("notifications")]
        [ProducesResponseType(typeof(PaginationResponse<NotificationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNotifications([FromQuery] DateTime createdBefore, [FromQuery] int limit)
        {
            var result = await _sender.Send(new GetNotificationsQuery{ CreatedBefore = createdBefore, Limit = limit });
            return Ok(result);
        }
    }
}

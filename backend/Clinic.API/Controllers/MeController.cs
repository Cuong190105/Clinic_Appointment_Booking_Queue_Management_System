using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.Features.Queue.Queries;
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

        public MeController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Trạng thái hàng đợi hiện tại của bệnh nhân (vị trí, thời gian chờ ước tính).
        /// Trả về mảng - có thể có nhiều phần tử nếu bệnh nhân đã check-in nhiều hơn 1 lịch hẹn
        /// trong cùng ngày. Trả về 204 nếu bệnh nhân chưa check-in lịch hẹn nào trong ngày hôm nay.
        /// </summary>
        [HttpGet("queue-status")]
        public async Task<IActionResult> GetMyQueueStatus()
        {
            var result = await _sender.Send(new GetMyQueueStatusQuery());
            return result.Count == 0 ? NoContent() : Ok(result);
        }

        /// <summary>Lịch sử khám bệnh (các hồ sơ đã chốt) của bệnh nhân đang đăng nhập.</summary>
        [HttpGet("medical-history")]
        public async Task<IActionResult> GetMyMedicalHistory()
        {
            var result = await _sender.Send(new GetMyMedicalHistoryQuery());
            return Ok(result);
        }

        [Authorize]
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] DateTime createdBefore, [FromQuery] int limit)
        {
            var result = await _sender.Send(new GetNotificationsQuery{ CreatedBefore = createdBefore, Limit = limit });
            return Ok(result);
        }
    }
}
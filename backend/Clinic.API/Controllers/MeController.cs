using Clinic.Application.Features.MedicalReports.Queries;
using Clinic.Application.Features.Queue.Queries;
using Clinic.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    /// <summary>
    /// Các API t?-ph?c-v? (self-service) cho b?nh nhân dang dang nh?p: theo dõi v? trí hàng d?i
    /// và tra c?u l?ch s? khám b?nh c?a chính mình.
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
        /// Tr?ng thái hàng d?i hi?n t?i c?a b?nh nhân (v? trí, th?i gian ch? u?c tính).
        /// Tr? v? m?ng - có th? có nhi?u ph?n t? n?u b?nh nhân dã check-in nhi?u hon 1 l?ch h?n
        /// trong cùng ngày. Tr? v? 204 n?u b?nh nhân chua check-in l?ch h?n nào trong ngày hôm nay.
        /// </summary>
        [HttpGet("queue-status")]
        public async Task<IActionResult> GetMyQueueStatus()
        {
            var result = await _sender.Send(new GetMyQueueStatusQuery());
            return result.Count == 0 ? NoContent() : Ok(result);
        }

        /// <summary>L?ch s? khám b?nh (các h? so dã ch?t) c?a b?nh nhân dang dang nh?p.</summary>
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
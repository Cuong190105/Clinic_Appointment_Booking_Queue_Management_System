using Clinic.Application.Features.Slots.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/slots")]
    public class SlotsController : ControllerBase
    {
        private readonly ISender _sender;

        public SlotsController(ISender sender)
        {
            _sender = sender;
        }

        // Public: bệnh nhân cần xem khung giờ trống để chọn trước khi đăng nhập/đặt lịch.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var result = await _sender.Send(new GetAvailableSlotsQuery(doctorId, specialtyId, fromDate, toDate));
            return Ok(result);
        }
    }
}
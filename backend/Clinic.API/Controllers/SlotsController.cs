using Clinic.Application.Features.Slots.Queries;
using Clinic.Application.Contracts;
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

        // Phase 0 contract endpoint. Availability ownership moves to Scheduling Service;
        // this stub documents the request and response shape without calling Application.
        [HttpGet("/schedules/availability")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<SlotDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public IActionResult GetAvailability(
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate)
        {
            return Ok(Array.Empty<SlotDto>());
        }
    }
}

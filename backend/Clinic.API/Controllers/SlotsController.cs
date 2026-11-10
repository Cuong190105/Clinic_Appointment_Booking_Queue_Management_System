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

        // Public: bá»‡nh nhÃ¢n cáº§n xem khung giá» trá»‘ng Ä‘á»ƒ chá»n trÆ°á»›c khi Ä‘Äƒng nháº­p/Ä‘áº·t lá»‹ch.
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
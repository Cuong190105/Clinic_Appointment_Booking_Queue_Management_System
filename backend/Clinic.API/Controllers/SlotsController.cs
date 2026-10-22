using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/slots")]
    public class SlotsController : ControllerBase
    {
        private readonly HttpClient _schedulingClient;

        public SlotsController(IHttpClientFactory httpClientFactory)
        {
            _schedulingClient = httpClientFactory.CreateClient("Scheduling");
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
            var query = new List<string>();
            if (doctorId.HasValue) query.Add($"doctorId={doctorId.Value}");
            if (specialtyId.HasValue) query.Add($"specialtyId={specialtyId.Value}");
            if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");

            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            var response = await _schedulingClient.GetAsync($"schedules/availability{queryString}");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [HttpGet("/schedules/availability")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailability(
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? specialtyId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate)
        {
            var query = new List<string>();
            if (doctorId.HasValue) query.Add($"doctorId={doctorId.Value}");
            if (specialtyId.HasValue) query.Add($"specialtyId={specialtyId.Value}");
            if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");

            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            var response = await _schedulingClient.GetAsync($"schedules/availability{queryString}");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
    }
}

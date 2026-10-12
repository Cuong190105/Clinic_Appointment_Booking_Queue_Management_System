using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class GetAppointmentsByDateRequest
    {
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;
        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}

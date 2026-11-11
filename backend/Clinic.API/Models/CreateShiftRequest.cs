using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateShiftRequest
    {
        [Required(ErrorMessage = "Date is required.")]
        public DateOnly Date { get; set; }
        [Required(ErrorMessage = "StartTime is required.")]
        public TimeOnly StartTime { get; set; }
        [Required(ErrorMessage = "EndTime is required.")]
        public TimeOnly EndTime { get; set; }
        [Required(ErrorMessage = "RecommendedCapacity is required."), Range(1, 200, ErrorMessage = "RecommendedCapacity must be between 1 and 200.")]
        public int RecommendedCapacity { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdateShiftRequest
    {
        [Required(ErrorMessage = "Date is required.")]
        public DateOnly Date { get; set; }
        [Required(ErrorMessage = "Start time is required.")]
        public TimeOnly StartTime { get; set; }
        [Required(ErrorMessage = "End time is required.")]
        public TimeOnly EndTime { get; set; }
        [Required(ErrorMessage = "Patient limit per slot is required."), Range(1, 200, ErrorMessage = "Patient limit per slot must be between 1 and 200.")]
        public int RecommendedCapacityPerSlot { get; set; }
    }
}

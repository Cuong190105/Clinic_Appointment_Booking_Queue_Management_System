using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdateShiftSuggestionRequest
    {
        [Required(ErrorMessage = "Date is required.")]
        public DateOnly Date { get; set; }
        [Required(ErrorMessage = "Start time is required.")]
        public TimeOnly StartTime { get; set; }
        [Required(ErrorMessage = "End time is required.")]
        public TimeOnly EndTime { get; set; }
        [Required(ErrorMessage = "Patient limit per slot is required.")]
        public int PatientLimitPerSlot { get; set; }
        [Required(ErrorMessage = "Reason is required.")]
        public string Reason { get; set; }
    }
}

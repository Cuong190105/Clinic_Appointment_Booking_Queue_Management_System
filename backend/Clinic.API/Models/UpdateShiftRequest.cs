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
        [Required(ErrorMessage = "Patient limit per slot is required."), Range(1, 50, ErrorMessage = "Patient limit per slot must be between 1 and 50.")]
        public int PatientLimitPerSlot { get; set; }
    }
}

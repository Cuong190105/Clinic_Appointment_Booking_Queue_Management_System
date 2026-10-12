using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CancelShiftRequest
    {
        [Required(ErrorMessage = "Reason is required."), MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string Reason { get; set; }
    }
}

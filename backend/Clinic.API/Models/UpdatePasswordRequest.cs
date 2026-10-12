using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdatePasswordRequest
    {
        [Required, MinLength(8, ErrorMessage = "Current password must be at least 8 characters long.")]
        public string CurrentPassword { get; set; }

        [Required, MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
        public string NewPassword { get; set; }
    }
}

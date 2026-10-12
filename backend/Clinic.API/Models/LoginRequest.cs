using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class LoginRequest
    {
        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
    }
}

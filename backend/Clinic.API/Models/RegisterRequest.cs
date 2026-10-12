using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class RegisterRequest
    {
        [Required, MaxLength(100), MinLength(8)]
        public string Username { get; set; }

        [Required, MaxLength(100), MinLength(8)]
        public string Password { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; }

        [MaxLength(200), EmailAddress]
        public string? Email { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required]
        public DateOnly DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        public string Address { get; set; }
    }
}

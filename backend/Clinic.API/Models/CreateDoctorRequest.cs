using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateDoctorRequest : CreateDoctorTemplate
    {
        [Required(ErrorMessage = "Username is required."), MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters."), MinLength(6, ErrorMessage = "Username must be at least 6 characters long.")]
        public string Username { get; set; }
        [Required(ErrorMessage = "Password is required."), MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Full name is required."), MaxLength(100, ErrorMessage = "Full name cannot exceed 100 characters."), MinLength(6, ErrorMessage = "Full name must be at least 6 characters long.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Phone number is required."), MaxLength(15, ErrorMessage = "Phone number cannot exceed 15 characters.")]
        public string PhoneNumber { get; set; }
        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly DateOfBirth { get; set; }
        [Required(ErrorMessage = "Gender is required.")]
        public Gender Gender { get; set; }
        [MaxLength(500, ErrorMessage = "Biography cannot exceed 500 characters.")]
        public string? Biography { get; set; }
    }
}

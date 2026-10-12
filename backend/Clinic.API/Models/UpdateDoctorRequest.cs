using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdateDoctorRequest
    {
        [Required(ErrorMessage = "Full name is required."), MaxLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Phone number is required."), MaxLength(15, ErrorMessage = "Phone number cannot exceed 15 characters.")]
        public string PhoneNumber { get; set; }
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly DateOfBirth { get; set; }
        [Required(ErrorMessage = "Gender is required.")]
        public Gender Gender { get; set; }
        [Required(ErrorMessage = "Address is required."), MaxLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
        public string Address { get; set; }
        [Required(ErrorMessage = "License number is required."), MaxLength(50, ErrorMessage = "License number cannot exceed 50 characters.")]
        public string LicenseNumber { get; set; }
        [Required(ErrorMessage = "Qualification is required."), MaxLength(100, ErrorMessage = "Qualification cannot exceed 100 characters.")]
        public string Qualification { get; set; }
        [Required(ErrorMessage = "Experience years is required.")]
        public int ExperienceYears { get; set; }
        [MaxLength(500, ErrorMessage = "Biography cannot exceed 500 characters.")]
        public string? Biography { get; set; } = null;
    }
}

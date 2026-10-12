using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateDoctorTemplate
    {
        [Required(ErrorMessage = "Hire date is required.")]
        public DateOnly HireDate { get; set; }
        [Required(ErrorMessage = "License number is required."), MaxLength(50, ErrorMessage = "License number cannot exceed 50 characters.")]
        public string LicenseNumber { get; set; }
        [Required(ErrorMessage = "Qualification is required."), MaxLength(100, ErrorMessage = "Qualification cannot exceed 100 characters.")]
        public string Qualification { get; set; }
        [MaxLength(500, ErrorMessage = "Biography cannot exceed 500 characters.")]
        public string? Biography { get; set; } = null;
        [Required(ErrorMessage = "Experience years is required.")]
        public int ExperienceYears { get; set; }
        [Required(ErrorMessage = "Status is required.")]
        public DoctorStatus Status { get; set; }
        [Required(ErrorMessage = "Specialty is required.")]
        public Guid SpecialtyId { get; set; }
        [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }
    }
}

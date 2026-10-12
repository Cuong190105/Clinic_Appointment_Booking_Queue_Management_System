using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class ReceptionistCreatePatientRequest
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; }
        [Phone]
        public string PhoneNumber { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public DateOnly DateOfBirth { get; set; }
        [Required]
        public Gender Gender { get; set; }
        [Required]
        public string Address { get; set; }
        [MaxLength(100)]
        public string InsuranceNumber { get; set; }
        [MaxLength(100)]
        public string EmergencyContact { get; set; }
    }
}

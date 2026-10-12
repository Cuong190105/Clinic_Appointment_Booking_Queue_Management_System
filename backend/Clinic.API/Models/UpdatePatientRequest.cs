using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdatePatientRequest
    {
        [EmailAddress]
        public string? Email { get; set; }
        [Required]
        public Gender Gender { get; set; }
        [Required]
        public string Address { get; set; }
        [MaxLength(100)]
        public string? InsuranceNumber { get; set; }
        [MaxLength(100)]
        public string? EmergencyContact { get; set; }
    }
}

using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdateEmployeeRequest
    {
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Phone number is required.")]
        public string PhoneNumber { get; set; }
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly DateOfBirth { get; set; }
        [Required(ErrorMessage = "Gender is required.")]
        public Gender Gender { get; set; }
        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }
    }
}

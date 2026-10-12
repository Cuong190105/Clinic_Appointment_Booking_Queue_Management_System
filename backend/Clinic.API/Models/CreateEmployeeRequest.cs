using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateEmployeeRequest
    {
        [Required(ErrorMessage = "Username is required."), MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters."), MinLength(6, ErrorMessage = "Username must be at least 6 characters long.")]
        public string Username { get; set; }
        [Required(ErrorMessage = "Password is required."), MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Full name is required.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Phone number is required.")]
        public string PhoneNumber { get; set; }
        [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly DateOfBirth { get; set; }
        [Required(ErrorMessage = "Gender is required.")]
        public Gender Gender { get; set; }
        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }
        [Required(ErrorMessage = "Hire date is required.")]
        public DateOnly HireDate { get; set; }
        [Required(ErrorMessage = "At least one role is required.")]
        public List<string> Roles { get; set; }
    }
}

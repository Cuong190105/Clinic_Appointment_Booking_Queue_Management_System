using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateDoctorFromUserRequest : CreateDoctorTemplate
    {
        [Required(ErrorMessage = "UserId is required.")]
        public Guid UserId { get; set; }
    }
}

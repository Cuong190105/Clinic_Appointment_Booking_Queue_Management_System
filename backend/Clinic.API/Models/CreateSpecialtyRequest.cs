using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateSpecialtyRequest
    {
        [Required(ErrorMessage = "Name is required."), MaxLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
        public string Name { get; set; }

        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Established date is required.")]
        public DateOnly EstablishedDate { get; set; }
    }
}

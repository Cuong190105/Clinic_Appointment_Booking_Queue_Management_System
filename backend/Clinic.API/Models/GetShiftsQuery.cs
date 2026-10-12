using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class GetShiftsQuery
    {
        [Required(ErrorMessage = "StartDate is required.")]
        public DateOnly StartDate { get; set; }
        [Required(ErrorMessage = "EndDate is required.")]
        public DateOnly EndDate { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateAppointmentRequest
    {
        [Required(ErrorMessage = "WorkScheduleId is required.")]
        public Guid WorkScheduleId { get; set; }
        [Required(ErrorMessage = "TimeSlot is required.")]
        public TimeOnly TimeSlot { get; set; }
        [Required(ErrorMessage = "Reason is required.")]
        public string Reason { get; set; }
    }
}

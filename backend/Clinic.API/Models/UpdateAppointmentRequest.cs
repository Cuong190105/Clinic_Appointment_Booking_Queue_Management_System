using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class UpdateAppointmentRequest
    {
        [Required(ErrorMessage = "NewWorkScheduleId is required.")]
        public Guid NewWorkScheduleId { get; set; }

        [Required(ErrorMessage = "NewTimeSlot is required.")]
        public TimeOnly TimeSlot { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        public string Reason { get; set; }
    }
}

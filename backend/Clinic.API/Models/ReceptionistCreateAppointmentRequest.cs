using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class ReceptionistCreateAppointmentRequest : CreateAppointmentRequest
    {
        [Required(ErrorMessage = "PatientId is required.")]
        public Guid PatientId { get; set; }
        [Required(ErrorMessage = "IsWalkIn is required.")]
        public bool IsWalkIn { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class SaveMedicalReportRequest
    {
        public Guid? AppointmentId { get; set; }

        public Guid? QueueTicketId { get; set; }

        public string? Symptoms { get; set; }

        public string? Diagnosis { get; set; }

        public string? Prescription { get; set; }

        public string? Notes { get; set; }

        public bool IsFinalize { get; set; } = false;
    }

    public class CreateAmendmentRequest
    {
        [Required(ErrorMessage = "Reason is required.")]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Content is required.")]
        public string Content { get; set; } = string.Empty;
    }
}

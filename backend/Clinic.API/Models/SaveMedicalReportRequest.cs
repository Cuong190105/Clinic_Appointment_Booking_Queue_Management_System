using System;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class SaveMedicalReportRequest
    {
        [Required(ErrorMessage = "QueueTicketId is required.")]
        public Guid QueueTicketId { get; set; }

        public string? Symptoms { get; set; }

        public string? Diagnosis { get; set; }

        public string? Prescription { get; set; }

        public string? Notes { get; set; }

        public bool IsFinalize { get; set; } = false;
    }
}

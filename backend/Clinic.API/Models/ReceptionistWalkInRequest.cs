using System;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class ReceptionistWalkInRequest
    {
        [Required(ErrorMessage = "PatientId is required.")]
        public Guid PatientId { get; set; }

        [Required(ErrorMessage = "SpecialtyId is required.")]
        public Guid SpecialtyId { get; set; }

        public Guid? DoctorId { get; set; }

        public string? Reason { get; set; }

        public bool IsUrgent { get; set; } = false;
    }
}


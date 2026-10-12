using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class ReceptionistUpdatePatientRequest : UpdatePatientRequest
    {
        [Required]
        public Guid? PatientId { get; set; }
    }
}

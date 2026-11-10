using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class CreateShiftRequest
    {
        [Required(ErrorMessage = "Date is required.")]
        public DateOnly Date { get; set; }
        [Required(ErrorMessage = "StartTime is required.")]
        public TimeOnly StartTime { get; set; }
        [Required(ErrorMessage = "EndTime is required.")]
        public TimeOnly EndTime { get; set; }
        public int? RecommendedCapacity { get; set; }
        public int? PatientLimit { get; set; }
        public int? PatientLimitPerSlot { get; set; }

        public int EffectiveCapacity => RecommendedCapacity ?? PatientLimit ?? PatientLimitPerSlot ?? 20;
    }
}

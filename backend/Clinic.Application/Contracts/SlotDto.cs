using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class SlotDto
    {
        [JsonPropertyName("workScheduleId")]
        public Guid WorkScheduleId { get; set; }

        [JsonPropertyName("doctorId")]
        public Guid DoctorId { get; set; }

        [JsonPropertyName("doctorName")]
        public string DoctorName { get; set; } = string.Empty;

        public DateOnly Date { get; set; }

        [JsonPropertyName("shiftStart")]
        public TimeOnly ShiftStart { get; set; }

        [JsonPropertyName("shiftEnd")]
        public TimeOnly ShiftEnd { get; set; }

        [JsonPropertyName("remainingCapacity")]
        public int RemainingCapacity { get; set; }
    }
}
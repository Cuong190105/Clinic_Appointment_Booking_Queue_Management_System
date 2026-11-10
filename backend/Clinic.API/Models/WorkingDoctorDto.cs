using System;
using System.Text.Json.Serialization;

namespace Clinic.API.Models
{
    public class WorkingDoctorDto
    {
        [JsonPropertyName("doctorId")]
        public Guid DoctorId { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("specialtyId")]
        public Guid? SpecialtyId { get; set; }

        [JsonPropertyName("specialtyName")]
        public string? SpecialtyName { get; set; }

        [JsonPropertyName("workScheduleId")]
        public Guid WorkScheduleId { get; set; }

        [JsonPropertyName("shiftStart")]
        public string ShiftStart { get; set; } = string.Empty;

        [JsonPropertyName("shiftEnd")]
        public string ShiftEnd { get; set; } = string.Empty;

        [JsonPropertyName("activeAppointmentsCount")]
        public int ActiveAppointmentsCount { get; set; }
    }
}

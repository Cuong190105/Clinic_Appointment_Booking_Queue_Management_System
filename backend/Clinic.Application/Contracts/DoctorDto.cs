using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class DoctorDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("qualification")]
        public string? Qualification { get; set; }

        [JsonPropertyName("experienceYears")]
        public int? ExperienceYears { get; set; }

        [JsonPropertyName("biography")]
        public string? Biography { get; set; }

        [JsonPropertyName("specialtyId")]
        public Guid? SpecialtyId { get; set; }

        [JsonPropertyName("specialtyName")]
        public string? SpecialtyName { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
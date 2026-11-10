using System;
using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class MedicalReportAmendmentDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("medicalReportId")]
        public Guid MedicalReportId { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("createdByDoctorId")]
        public Guid CreatedByDoctorId { get; set; }

        [JsonPropertyName("doctorName")]
        public string DoctorName { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}

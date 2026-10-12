using Clinic.Domain.Enums;
using System;
using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class MedicalReportDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("queueTicketId")]
        public Guid QueueTicketId { get; set; }

        [JsonPropertyName("patientId")]
        public Guid PatientId { get; set; }

        [JsonPropertyName("patientName")]
        public string PatientName { get; set; } = string.Empty;

        [JsonPropertyName("doctorId")]
        public Guid DoctorId { get; set; }

        [JsonPropertyName("doctorName")]
        public string DoctorName { get; set; } = string.Empty;

        [JsonPropertyName("specialtyName")]
        public string SpecialtyName { get; set; } = string.Empty;

        [JsonPropertyName("examDate")]
        public DateOnly ExamDate { get; set; }

        [JsonPropertyName("symptoms")]
        public string? Symptoms { get; set; }

        [JsonPropertyName("diagnosis")]
        public string? Diagnosis { get; set; }

        [JsonPropertyName("prescription")]
        public string? Prescription { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("examStartTime")]
        public DateTime? ExamStartTime { get; set; }

        [JsonPropertyName("examEndTime")]
        public DateTime? ExamEndTime { get; set; }

        [JsonPropertyName("status")]
        public MedicalReportStatus Status { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
using System.Text.Json.Serialization;

namespace Clinic.Queue.Service.DTOs;

public class QueueTicketDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("patientId")]
    public Guid PatientId { get; set; }

    [JsonPropertyName("doctorId")]
    public Guid DoctorId { get; set; }

    [JsonPropertyName("queueDate")]
    public DateOnly QueueDate { get; set; }

    [JsonPropertyName("queueNumber")]
    public int QueueNumber { get; set; }

    [JsonPropertyName("priority")]
    public bool Priority { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("checkInTime")]
    public DateTime CheckInTime { get; set; }

    [JsonPropertyName("calledAt")]
    public DateTime? CalledAt { get; set; }

    [JsonPropertyName("examStartedAt")]
    public DateTime? ExamStartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("skippedAt")]
    public DateTime? SkippedAt { get; set; }

    [JsonPropertyName("patientName")]
    public string? PatientName { get; set; }

    [JsonPropertyName("doctorName")]
    public string? DoctorName { get; set; }
}

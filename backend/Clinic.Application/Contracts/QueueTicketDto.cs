using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    public class QueueTicketDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("appointmentId")]
        public Guid AppointmentId { get; set; }

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

        [JsonPropertyName("patientName")]
        public string? PatientName { get; set; }
    }
}
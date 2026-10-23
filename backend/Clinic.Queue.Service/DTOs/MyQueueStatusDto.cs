using System.Text.Json.Serialization;

namespace Clinic.Queue.Service.DTOs;

public class MyQueueStatusDto
{
    [JsonPropertyName("queueTicketId")]
    public Guid QueueTicketId { get; set; }

    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("queueNumber")]
    public int QueueNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("doctorName")]
    public string DoctorName { get; set; } = string.Empty;

    [JsonPropertyName("positionInQueue")]
    public int PositionInQueue { get; set; }

    [JsonPropertyName("estimatedWaitMinutes")]
    public int EstimatedWaitMinutes { get; set; }
}

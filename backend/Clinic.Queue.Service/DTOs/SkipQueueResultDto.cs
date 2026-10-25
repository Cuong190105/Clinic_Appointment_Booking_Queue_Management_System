using System.Text.Json.Serialization;

namespace Clinic.Queue.Service.DTOs;

public record SkipQueueResultDto(
    [property: JsonPropertyName("skippedTicket")] QueueTicketDto SkippedTicket,
    [property: JsonPropertyName("nextCalledTicket")] QueueTicketDto? NextCalledTicket
);

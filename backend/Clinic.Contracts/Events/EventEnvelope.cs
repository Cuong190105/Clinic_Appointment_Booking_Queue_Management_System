namespace Clinic.Contracts.Events;

/// <summary>
/// Common envelope for all integration events published through the broker.
/// </summary>
public sealed record EventEnvelope<TPayload>
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required int SchemaVersion { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required Guid CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public required string Producer { get; init; }

    public required TPayload Payload { get; init; }
}

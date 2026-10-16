using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models;

public sealed class ReserveSlotRequest
{
    [Required]
    public TimeOnly TimeSlot { get; set; }

    [Required]
    public Guid PatientId { get; set; }
}

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid ScheduleId,
    TimeOnly TimeSlot,
    DateTime ExpiresAt,
    string Status);

public sealed record ReservationActionResponse(
    Guid ReservationId,
    Guid ScheduleId,
    string Status,
    DateTime UpdatedAt);

public sealed class DraftMedicalRecordRequest
{
    [Required]
    public Guid EncounterId { get; set; }

    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }
}

public sealed class AmendmentMedicalRecordRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;

    public string? Diagnosis { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }
}

public sealed record MedicalRecordContractResponse(
    Guid ReportId,
    Guid VisitId,
    string Status,
    DateTime UpdatedAt);

public sealed record JwksResponse(IReadOnlyList<JwkKeyResponse> Keys);

public sealed record JwkKeyResponse(
    string Kid,
    string Kty,
    string Use,
    string Alg,
    string N,
    string E);

public sealed record CheckInContractResponse(
    Guid AppointmentId,
    Guid VisitId,
    string Status,
    DateTime CheckedInAt);

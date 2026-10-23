namespace Clinic.Appointment.Service.Entities;

public class PatientProjection
{
    public Guid Id { get; set; }
    public Guid? IdentityUserId { get; set; }
    public Guid? PersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


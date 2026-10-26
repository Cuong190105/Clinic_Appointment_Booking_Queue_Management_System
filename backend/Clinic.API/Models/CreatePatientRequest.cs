using System;

namespace Clinic.API.Models;

public class CreatePatientRequest
{
    public Guid? Id { get; set; }
    public Guid? IdentityUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "Other";
    public string? Address { get; set; }
    public string? InsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }
}

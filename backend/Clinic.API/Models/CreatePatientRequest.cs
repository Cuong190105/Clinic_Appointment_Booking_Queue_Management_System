using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Clinic.Contracts.Serialization;

namespace Clinic.API.Models;

public class CreatePatientRequest
{
    public Guid? Id { get; set; }
    public Guid? IdentityUserId { get; set; }
    [Required, MinLength(1)]
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    [Required]
    public DateOnly? DateOfBirth { get; set; }
    [Required]
    [JsonConverter(typeof(GenderStringJsonConverter))]
    public string Gender { get; set; } = null!;
    [Required, MinLength(1)]
    public string Address { get; set; } = null!;
    public string? InsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }
}

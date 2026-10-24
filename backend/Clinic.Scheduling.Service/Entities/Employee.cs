namespace Clinic.Scheduling.Service.Entities;

using Clinic.Scheduling.Service.Enums;

public class Employee
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid? IdentityUserId { get; set; }

    // Projections from Identity Service
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }

    // HR fields
    public DateOnly HireDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public Guid? ManagerId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Employee? Manager { get; set; }
    public Doctor? Doctor { get; set; }

    public void UpdateProfileProjection(string fullName, string? phoneNumber, string? email, string? gender)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        Gender = gender;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(EmployeeStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}


namespace Clinic.MedicalRecords.Service.Entities;

public class Patient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? IdentityUserId { get; set; }
    public Guid? PersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "Other";
    public string? Address { get; set; }
    public string? InsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<MedicalReport> MedicalReports { get; set; } = new List<MedicalReport>();

    public Patient() { }

    public Patient(
        Guid id,
        Guid? identityUserId,
        string fullName,
        string? phoneNumber,
        string? email,
        DateOnly dateOfBirth,
        string gender,
        string? address,
        string? insuranceNumber,
        string? emergencyContact)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        IdentityUserId = identityUserId;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Address = address;
        InsuranceNumber = insuranceNumber;
        EmergencyContact = emergencyContact;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(
        string? email,
        string? address,
        string? insuranceNumber,
        string? emergencyContact,
        string? gender = null)
    {
        if (email != null) Email = email;
        if (address != null) Address = address;
        if (insuranceNumber != null) InsuranceNumber = insuranceNumber;
        if (emergencyContact != null) EmergencyContact = emergencyContact;
        if (gender != null) Gender = gender;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void LinkIdentityUser(Guid identityUserId)
    {
        IdentityUserId = identityUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void LinkPerson(Guid personId)
    {
        PersonId = personId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }


    public void UpdateProjectionFromIdentity(
        string fullName,
        string? phoneNumber,
        string? email,
        string? gender,
        DateOnly? dateOfBirth,
        string? address)
    {
        FullName = fullName;
        if (!string.IsNullOrWhiteSpace(phoneNumber)) PhoneNumber = phoneNumber;
        if (!string.IsNullOrWhiteSpace(email)) Email = email;
        if (!string.IsNullOrWhiteSpace(gender)) Gender = gender;
        if (dateOfBirth.HasValue) DateOfBirth = dateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(address)) Address = address;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

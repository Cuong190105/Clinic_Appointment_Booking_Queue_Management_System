using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Clinic.MedicalRecords.Service.Services;

public interface IMedicalRecordsManager
{
    Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, CancellationToken ct = default);
    Task<PagedResult<PatientDto>> GetPagedPatientsAsync(string? search, string? sortBy, string? orderBy, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken ct = default);
    Task<PatientDto?> GetPatientByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default);
    Task<bool> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request, CancellationToken ct = default);
    Task<bool> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default);
}

public class MedicalRecordsManager : IMedicalRecordsManager
{
    private readonly MedicalRecordsDbContext _db;
    private readonly ILogger<MedicalRecordsManager> _logger;

    public MedicalRecordsManager(MedicalRecordsDbContext db, ILogger<MedicalRecordsManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, CancellationToken ct = default)
    {
        var patientId = Guid.NewGuid();
        var patient = new Patient(
            id: patientId,
            identityUserId: request.IdentityUserId,
            fullName: request.FullName,
            phoneNumber: request.PhoneNumber,
            email: request.Email,
            dateOfBirth: request.DateOfBirth,
            gender: request.Gender,
            address: request.Address,
            insuranceNumber: request.InsuranceNumber,
            emergencyContact: request.EmergencyContact
        );

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Patient created successfully with Id={PatientId}, FullName={FullName}", patient.Id, patient.FullName);
        return MapToDto(patient);
    }

    public async Task<PagedResult<PatientDto>> GetPagedPatientsAsync(
        string? search,
        string? sortBy,
        string? orderBy,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(p =>
                p.FullName.Contains(trimmed) ||
                (p.PhoneNumber != null && p.PhoneNumber.Contains(trimmed)) ||
                (p.InsuranceNumber != null && p.InsuranceNumber.Contains(trimmed)) ||
                (p.Email != null && p.Email.Contains(trimmed)));
        }

        var isDesc = string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant()) switch
        {
            "fullname" => isDesc ? query.OrderByDescending(p => p.FullName) : query.OrderBy(p => p.FullName),
            "dateofbirth" => isDesc ? query.OrderByDescending(p => p.DateOfBirth) : query.OrderBy(p => p.DateOfBirth),
            "createdat" => isDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => isDesc ? query.OrderByDescending(p => p.FullName) : query.OrderBy(p => p.FullName)
        };

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PatientDto(
                p.Id,
                p.FullName,
                p.PhoneNumber,
                p.Email,
                p.Address ?? string.Empty,
                p.DateOfBirth,
                p.Gender,
                p.InsuranceNumber,
                p.EmergencyContact,
                p.IdentityUserId
            ))
            .ToListAsync(ct);

        return new PagedResult<PatientDto>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct);
        return patient is null ? null : MapToDto(patient);
    }

    public async Task<PatientDto?> GetPatientByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId, ct);
        return patient is null ? null : MapToDto(patient);
    }

    public async Task<bool> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct);
        if (patient is null)
        {
            return false;
        }

        patient.UpdateProfile(
            email: request.Email,
            address: request.Address,
            insuranceNumber: request.InsuranceNumber,
            emergencyContact: request.EmergencyContact,
            gender: request.Gender
        );

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Patient updated successfully with Id={PatientId}", patient.Id);
        return true;
    }

    public async Task<bool> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == evt.PersonId || (p.IdentityUserId != null && p.IdentityUserId == evt.PersonId), ct);
        if (patient is null)
        {
            _logger.LogInformation("No matching patient found for UserProfileUpdated PersonId={PersonId}", evt.PersonId);
            return false;
        }

        patient.UpdateProjectionFromIdentity(
            fullName: evt.FullName,
            phoneNumber: evt.PhoneNumber,
            email: evt.Email,
            gender: evt.Gender,
            dateOfBirth: evt.DateOfBirth,
            address: evt.Address
        );

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Synchronized patient projection for PatientId={PatientId} from UserProfileUpdated", patient.Id);
        return true;
    }

    private static PatientDto MapToDto(Patient patient) =>
        new(
            patient.Id,
            patient.FullName,
            patient.PhoneNumber,
            patient.Email,
            patient.Address ?? string.Empty,
            patient.DateOfBirth,
            patient.Gender,
            patient.InsuranceNumber,
            patient.EmergencyContact,
            patient.IdentityUserId
        );
}

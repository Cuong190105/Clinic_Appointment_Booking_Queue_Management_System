using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Clinic.MedicalRecords.Service.Services;

public interface IMedicalRecordsManager
{
    // Patient operations
    Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, Guid actorUserId = default, string actorRole = "System", string? ipAddress = null, CancellationToken ct = default);
    Task<PatientDto> ProvisionIdentityPatientAsync(ProvisionIdentityPatientRequest request, CancellationToken ct = default);
    Task<IdentityRegistrationPatientIds> ResolveRegistrationPatientAsync(string phoneNumber, CancellationToken ct = default);
    Task<int> GetPatientCountAsync(CancellationToken ct = default);
    Task<PagedResult<PatientDto>> GetPagedPatientsAsync(string? search, string? sortBy, string? orderBy, int pageNumber, int pageSize, CancellationToken ct = default);
    Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken ct = default);
    Task<PatientDto?> GetPatientByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default);
    Task<bool> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request, Guid actorUserId = default, string actorRole = "System", string? ipAddress = null, CancellationToken ct = default);
    Task<bool> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default);

    // Medical Records operations
    Task<MedicalReportDto> SaveReportAsync(SaveMedicalReportRequest request, Guid currentUserId, string currentRole, Guid? currentDoctorId, string? currentDoctorName, string? ipAddress = null, CancellationToken ct = default);
    Task<MedicalReportDto> UpdateReportDraftAsync(Guid reportId, UpdateMedicalReportRequest request, Guid currentUserId, string currentRole, Guid? currentDoctorId = null, string? ipAddress = null, CancellationToken ct = default);
    Task<MedicalReportDto> FinalizeReportAsync(Guid reportId, Guid currentUserId, string currentRole, Guid? currentDoctorId = null, string? ipAddress = null, CancellationToken ct = default);
    Task<MedicalReportAmendmentDto> CreateAmendmentAsync(Guid appointmentId, CreateAmendmentRequest request, Guid currentUserId, string currentRole, Guid currentDoctorId, string currentDoctorName, string? ipAddress = null, CancellationToken ct = default);
    Task<MedicalReportDto?> GetReportByIdAsync(Guid reportId, CancellationToken ct = default);
    Task<MedicalReportDto?> GetReportByAppointmentIdAsync(Guid appointmentId, CancellationToken ct = default);
    Task<PagedResult<MedicalReportDto>> GetPagedReportsAsync(string? search, string? status, DateOnly? fromDate, DateOnly? toDate, int pageNumber, int pageSize, Guid? doctorId = null, CancellationToken ct = default);
    Task<List<MedicalReportDto>> GetPatientMedicalHistoryAsync(Guid patientId, Guid? doctorId = null, CancellationToken ct = default);
    Task<List<MedicalReportDto>> GetMyMedicalHistoryAsync(Guid patientId, CancellationToken ct = default);

    // Queue / Encounter event handlers
    Task HandleExamStartedAsync(ExamStarted evt, CancellationToken ct = default);
    Task HandleExamCompletedAsync(ExamCompleted evt, CancellationToken ct = default);

    // Audit operations
    Task RecordAccessAuditAsync(
        Guid actorUserId,
        string actorRole,
        Guid? actorDoctorId,
        Guid? actorPatientId,
        Guid? appointmentId,
        Guid? patientId,
        Guid? medicalReportId,
        string action,
        string outcome = "Success",
        string? ipAddress = null,
        Guid? correlationId = null,
        CancellationToken ct = default);
    Task<List<MedicalAccessAuditDto>> GetAuditsByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
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

    #region Patient Operations

    public async Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, Guid actorUserId = default, string actorRole = "System", string? ipAddress = null, CancellationToken ct = default)
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
        QueuePatientProjection(patient);
        AddAudit(actorUserId, actorRole, null, null, null, patient.Id, null, "CreatePatient", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Patient created successfully with Id={PatientId}", patient.Id);
        return MapPatientToDto(patient);
    }

    public async Task<PatientDto> ProvisionIdentityPatientAsync(ProvisionIdentityPatientRequest request, CancellationToken ct = default)
    {
        var existing = await _db.Patients.FirstOrDefaultAsync(p =>
            p.Id == request.PatientId || p.IdentityUserId == request.IdentityUserId || p.PersonId == request.PersonId, ct);
        if (existing is not null)
        {
            if (existing.Id != request.PatientId ||
                (existing.IdentityUserId.HasValue && existing.IdentityUserId != request.IdentityUserId) ||
                (existing.PersonId.HasValue && existing.PersonId != request.PersonId))
                throw new InvalidOperationException("Identity registration conflicts with an existing patient profile.");
            existing.LinkIdentityUser(request.IdentityUserId);
            existing.LinkPerson(request.PersonId);
            existing.UpdateProjectionFromIdentity(request.FullName, request.PhoneNumber, request.Email,
                request.Gender, request.DateOfBirth, request.Address);
            QueuePatientProjection(existing);
            AddAudit(request.IdentityUserId, "Identity", null, null, null, existing.Id, null, "ProvisionIdentityPatient", "Success", null);
            await _db.SaveChangesAsync(ct);
            return MapPatientToDto(existing);
        }

        var patient = new Patient(request.PatientId, request.IdentityUserId, request.FullName,
            request.PhoneNumber, request.Email, request.DateOfBirth, request.Gender, request.Address,
            null, null) { PersonId = request.PersonId };
        _db.Patients.Add(patient);
        QueuePatientProjection(patient);
        AddAudit(request.IdentityUserId, "Identity", null, null, null, patient.Id, null, "ProvisionIdentityPatient", "Success", null);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Provisioned patient profile for IdentityUserId={IdentityUserId}", request.IdentityUserId);
        return MapPatientToDto(patient);
    }

    public async Task<IdentityRegistrationPatientIds> ResolveRegistrationPatientAsync(string phoneNumber, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => !p.IsDeleted && p.PhoneNumber == phoneNumber.Trim(), ct);
        if (patient is null)
            return new IdentityRegistrationPatientIds(Guid.NewGuid(), Guid.NewGuid());

        if (!patient.PersonId.HasValue)
        {
            patient.PersonId = Guid.NewGuid();
            patient.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new IdentityRegistrationPatientIds(patient.PersonId.Value, patient.Id);
    }

    public Task<int> GetPatientCountAsync(CancellationToken ct = default) =>
        _db.Patients.CountAsync(p => !p.IsDeleted, ct);

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
        return patient is null ? null : MapPatientToDto(patient);
    }

    public async Task<PatientDto?> GetPatientByIdentityUserIdAsync(Guid identityUserId, CancellationToken ct = default)
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId, ct);
        return patient is null ? null : MapPatientToDto(patient);
    }

    public async Task<bool> UpdatePatientAsync(Guid patientId, UpdatePatientRequest request, Guid actorUserId = default, string actorRole = "System", string? ipAddress = null, CancellationToken ct = default)
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

        QueuePatientProjection(patient);
        AddAudit(actorUserId, actorRole, null, patientId, null, patient.Id, null, "UpdatePatient", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Patient updated successfully with Id={PatientId}", patient.Id);
        return true;
    }

    public async Task<bool> HandleUserProfileUpdatedAsync(UserProfileUpdated evt, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p =>
            (evt.PatientId.HasValue && p.Id == evt.PatientId.Value) ||
            (evt.IdentityUserId.HasValue && p.IdentityUserId == evt.IdentityUserId.Value) || p.PersonId == evt.PersonId, ct);
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

        QueuePatientProjection(patient);
        AddAudit(evt.IdentityUserId ?? Guid.Empty, "Identity", null, null, null, patient.Id, null,
            "SyncPatientProfile", "Success", null);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Synchronized patient projection for PatientId={PatientId} from UserProfileUpdated", patient.Id);
        return true;
    }

    private static PatientDto MapPatientToDto(Patient patient) =>
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

    #endregion

    #region Medical Records Operations

    public async Task<MedicalReportDto> SaveReportAsync(
        SaveMedicalReportRequest request,
        Guid currentUserId,
        string currentRole,
        Guid? currentDoctorId,
        string? currentDoctorName,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var existing = await _db.MedicalReports
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.AppointmentId == request.AppointmentId, ct);

        if (existing != null)
        {
            EnsureDoctorOwnsReport(existing, currentRole, currentDoctorId);
            if (existing.Status.Equals("Finalized", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot overwrite a finalized medical report. Use amendments instead.");
            }

            existing.UpdateDraft(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
            if (request.IsFinalize)
            {
                existing.FinalizeReport();
            }

            AddAudit(currentUserId, currentRole, currentDoctorId, null, request.AppointmentId, existing.PatientId,
                existing.Id, request.IsFinalize ? "FinalizeReport" : "SaveDraft", "Success", ipAddress);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated draft report Id={ReportId} for AppointmentId={AppointmentId}, Finalized={IsFinalize}",
                existing.Id, request.AppointmentId, request.IsFinalize);
            return MapReportToDto(existing);
        }

        var patientId = request.PatientId;
        var doctorId = request.DoctorId ?? currentDoctorId ?? Guid.Empty;
        var doctorName = request.DoctorName ?? currentDoctorName;
        DateOnly? scheduleDate = request.ScheduleDate;

        if (currentRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (!currentDoctorId.HasValue || currentDoctorId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("DoctorId claim is required to create a medical report.");
            if (request.DoctorId.HasValue && request.DoctorId.Value != currentDoctorId.Value)
                throw new UnauthorizedAccessException("A doctor can only create reports under their own DoctorId.");
            doctorId = currentDoctorId.Value;
        }

        var encounter = await _db.EncounterContexts.FirstOrDefaultAsync(e => e.AppointmentId == request.AppointmentId, ct);
        if (encounter is null)
            throw new InvalidOperationException("The appointment has no ExamStarted encounter context yet.");
        if (patientId.HasValue && patientId.Value != encounter.PatientId)
            throw new ArgumentException("PatientId does not match the appointment encounter.", nameof(request.PatientId));
        if (currentRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase) && encounter.DoctorId != currentDoctorId)
            throw new UnauthorizedAccessException("This appointment belongs to another doctor.");
        patientId = encounter.PatientId;
        if (doctorId == Guid.Empty) doctorId = encounter.DoctorId;
        doctorName ??= encounter.DoctorName;
        scheduleDate ??= encounter.QueueDate;
        if (doctorId != encounter.DoctorId)
        {
            throw new ArgumentException("DoctorId does not match the appointment encounter.", nameof(request.DoctorId));
        }

        if (!patientId.HasValue || patientId.Value == Guid.Empty)
        {
            if (encounter != null)
            {
                patientId = encounter.PatientId;
                if (doctorId == Guid.Empty) doctorId = encounter.DoctorId;
                doctorName ??= encounter.DoctorName;
                scheduleDate ??= encounter.QueueDate;
            }
        }

        if (!patientId.HasValue || patientId.Value == Guid.Empty)
        {
            throw new ArgumentException("PatientId could not be resolved for appointment.", nameof(request.AppointmentId));
        }

        var report = new MedicalReport(
            appointmentId: request.AppointmentId,
            patientId: patientId.Value,
            doctorId: doctorId,
            doctorName: doctorName,
            specialtyName: request.SpecialtyName,
            scheduleDate: scheduleDate,
            symptoms: request.Symptoms,
            diagnosis: request.Diagnosis,
            prescription: request.Prescription,
            notes: request.Notes
        );

        if (request.IsFinalize)
        {
            report.FinalizeReport();
        }

        _db.MedicalReports.Add(report);
        AddAudit(currentUserId, currentRole, currentDoctorId, null, report.AppointmentId, report.PatientId,
            report.Id, request.IsFinalize ? "FinalizeReport" : "SaveDraft", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);

        // Reload with patient info
        await _db.Entry(report).Reference(r => r.Patient).LoadAsync(ct);

        _logger.LogInformation("Created new medical report Id={ReportId} for AppointmentId={AppointmentId}, Status={Status}",
            report.Id, report.AppointmentId, report.Status);

        return MapReportToDto(report);
    }

    public async Task<MedicalReportDto> UpdateReportDraftAsync(
        Guid reportId,
        UpdateMedicalReportRequest request,
        Guid currentUserId,
        string currentRole,
        Guid? currentDoctorId = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var report = await _db.MedicalReports
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report == null)
        {
            throw new KeyNotFoundException($"Medical report with Id={reportId} was not found.");
        }

        EnsureDoctorOwnsReport(report, currentRole, currentDoctorId);

        report.UpdateDraft(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
        if (request.IsFinalize)
        {
            report.FinalizeReport();
        }

        AddAudit(currentUserId, currentRole, currentDoctorId, null, report.AppointmentId, report.PatientId,
            report.Id, request.IsFinalize ? "FinalizeReport" : "UpdateDraft", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated draft report Id={ReportId}, Finalized={IsFinalize}", reportId, request.IsFinalize);
        return MapReportToDto(report);
    }

    public async Task<MedicalReportDto> FinalizeReportAsync(
        Guid reportId,
        Guid currentUserId,
        string currentRole,
        Guid? currentDoctorId = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var report = await _db.MedicalReports
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report == null)
        {
            throw new KeyNotFoundException($"Medical report with Id={reportId} was not found.");
        }

        EnsureDoctorOwnsReport(report, currentRole, currentDoctorId);

        report.FinalizeReport();
        AddAudit(currentUserId, currentRole, currentDoctorId, null, report.AppointmentId, report.PatientId,
            report.Id, "FinalizeReport", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Medical report Id={ReportId} finalized successfully without calling queue service.", reportId);
        return MapReportToDto(report);
    }

    public async Task<MedicalReportAmendmentDto> CreateAmendmentAsync(
        Guid appointmentId,
        CreateAmendmentRequest request,
        Guid currentUserId,
        string currentRole,
        Guid currentDoctorId,
        string currentDoctorName,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var report = await _db.MedicalReports
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

        if (report == null)
        {
            throw new KeyNotFoundException($"Medical report for AppointmentId={appointmentId} was not found.");
        }

        EnsureDoctorOwnsReport(report, currentRole, currentDoctorId);

        if (!report.Status.Equals("Finalized", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Amendments can only be added to finalized medical reports.");
        }

        var nextNumber = (report.Amendments.Count > 0 ? report.Amendments.Max(a => a.AmendmentNumber) : 0) + 1;

        var amendment = new MedicalReportAmendment(
            medicalReportId: report.Id,
            appointmentId: appointmentId,
            amendmentNumber: nextNumber,
            reason: request.Reason,
            symptoms: request.Symptoms,
            diagnosis: request.Diagnosis,
            prescription: request.Prescription,
            notes: request.Notes,
            authorDoctorId: currentDoctorId,
            authorName: currentDoctorName
        );

        _db.MedicalReportAmendments.Add(amendment);
        AddAudit(currentUserId, currentRole, currentDoctorId, null, appointmentId, report.PatientId,
            report.Id, "CreateAmendment", "Success", ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Amendment #{Number} added to MedicalReportId={ReportId} by Doctor={DoctorName}",
            nextNumber, report.Id, currentDoctorName);

        return new MedicalReportAmendmentDto(
            amendment.Id,
            amendment.MedicalReportId,
            amendment.AppointmentId,
            amendment.AmendmentNumber,
            amendment.Reason,
            amendment.Symptoms,
            amendment.Diagnosis,
            amendment.Prescription,
            amendment.Notes,
            amendment.AuthorDoctorId,
            amendment.AuthorName,
            amendment.CreatedAt
        );
    }

    public async Task<MedicalReportDto?> GetReportByIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var report = await _db.MedicalReports
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        return report == null ? null : MapReportToDto(report);
    }

    public async Task<MedicalReportDto?> GetReportByAppointmentIdAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var report = await _db.MedicalReports
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

        return report == null ? null : MapReportToDto(report);
    }

    public async Task<PagedResult<MedicalReportDto>> GetPagedReportsAsync(
        string? search,
        string? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int pageNumber,
        int pageSize,
        Guid? doctorId = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.MedicalReports
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .AsQueryable();

        if (doctorId.HasValue) query = query.Where(r => r.DoctorId == doctorId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.ScheduleDate >= fromDate.Value || DateOnly.FromDateTime(r.CreatedAt.DateTime) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.ScheduleDate <= toDate.Value || DateOnly.FromDateTime(r.CreatedAt.DateTime) <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(r =>
                (r.Patient != null && r.Patient.FullName.Contains(trimmed)) ||
                (r.DoctorName != null && r.DoctorName.Contains(trimmed)) ||
                (r.Diagnosis != null && r.Diagnosis.Contains(trimmed)) ||
                (r.Symptoms != null && r.Symptoms.Contains(trimmed)));
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(MapReportToDto).ToList();
        return new PagedResult<MedicalReportDto>(dtos, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<List<MedicalReportDto>> GetPatientMedicalHistoryAsync(Guid patientId, Guid? doctorId = null, CancellationToken ct = default)
    {
        var reports = await _db.MedicalReports
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .Where(r => r.PatientId == patientId && (!doctorId.HasValue || r.DoctorId == doctorId.Value))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return reports.Select(MapReportToDto).ToList();
    }

    public async Task<List<MedicalReportDto>> GetMyMedicalHistoryAsync(Guid patientId, CancellationToken ct = default)
    {
        // For patient viewing their own history: show finalized reports
        var reports = await _db.MedicalReports
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Amendments)
            .Where(r => r.PatientId == patientId && r.Status == "Finalized")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return reports.Select(MapReportToDto).ToList();
    }

    public async Task HandleExamStartedAsync(ExamStarted evt, CancellationToken ct = default)
    {
        if (evt.AppointmentId == Guid.Empty || evt.PatientId == Guid.Empty || evt.DoctorId == Guid.Empty)
            throw new ArgumentException("ExamStarted requires AppointmentId, PatientId, and DoctorId.", nameof(evt));
        var existing = await _db.EncounterContexts.FirstOrDefaultAsync(e => e.AppointmentId == evt.AppointmentId, ct);
        if (existing == null)
        {
            var encounter = new EncounterContext(
                evt.AppointmentId,
                evt.QueueTicketId,
                evt.PatientId,
                evt.DoctorId,
                evt.DoctorName,
                evt.QueueDate,
                evt.StartedAt
            );
            _db.EncounterContexts.Add(encounter);
        }
        else
        {
            existing.MarkStarted(evt);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Handled ExamStarted for AppointmentId={AppointmentId}, PatientId={PatientId}",
            evt.AppointmentId, evt.PatientId);
    }

    public async Task HandleExamCompletedAsync(ExamCompleted evt, CancellationToken ct = default)
    {
        if (evt.AppointmentId == Guid.Empty || evt.DoctorId == Guid.Empty)
            throw new ArgumentException("ExamCompleted requires AppointmentId and DoctorId.", nameof(evt));
        var existing = await _db.EncounterContexts.FirstOrDefaultAsync(e => e.AppointmentId == evt.AppointmentId, ct);
        if (existing != null)
        {
            existing.MarkCompleted(evt.CompletedAt);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Handled ExamCompleted for AppointmentId={AppointmentId}", evt.AppointmentId);
        }
        else
        {
            _db.EncounterContexts.Add(new EncounterContext
            {
                AppointmentId = evt.AppointmentId,
                Status = "Completed",
                CompletedAt = evt.CompletedAt,
                StartedAt = evt.CompletedAt,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
    }

    private static void EnsureDoctorOwnsReport(MedicalReport report, string currentRole, Guid? currentDoctorId)
    {
        if (!currentRole.Equals("Doctor", StringComparison.OrdinalIgnoreCase)) return;
        if (!currentDoctorId.HasValue || currentDoctorId.Value == Guid.Empty || report.DoctorId != currentDoctorId.Value)
            throw new UnauthorizedAccessException("A doctor can only access or modify their own medical reports.");
    }

    private void AddAudit(Guid actorUserId, string actorRole, Guid? actorDoctorId, Guid? actorPatientId,
        Guid? appointmentId, Guid? patientId, Guid? reportId, string action, string outcome, string? ipAddress)
    {
        _db.MedicalAccessAudits.Add(new MedicalAccessAudit(actorUserId, actorRole, actorDoctorId,
            actorPatientId, appointmentId, patientId, reportId, action, outcome, ipAddress, null));
    }

    private void QueuePatientProjection(Patient patient) =>
        _db.PatientProjectionOutbox.Add(new PatientProjectionOutboxMessage { PatientId = patient.Id });

    private static MedicalReportDto MapReportToDto(MedicalReport report)
    {
        var amendments = report.Amendments?
            .OrderBy(a => a.AmendmentNumber)
            .Select(a => new MedicalReportAmendmentDto(
                a.Id,
                a.MedicalReportId,
                a.AppointmentId,
                a.AmendmentNumber,
                a.Reason,
                a.Symptoms,
                a.Diagnosis,
                a.Prescription,
                a.Notes,
                a.AuthorDoctorId,
                a.AuthorName,
                a.CreatedAt
            )).ToList() ?? new List<MedicalReportAmendmentDto>();

        return new MedicalReportDto(
            report.Id,
            report.AppointmentId,
            report.PatientId,
            report.Patient?.FullName ?? string.Empty,
            report.DoctorId,
            report.DoctorName ?? string.Empty,
            report.SpecialtyName ?? string.Empty,
            report.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt.DateTime),
            report.Symptoms,
            report.Diagnosis,
            report.Prescription,
            report.Notes,
            report.ExamStartTime,
            report.ExamEndTime,
            report.Status,
            report.CreatedAt,
            amendments
        );
    }

    public async Task RecordAccessAuditAsync(
        Guid actorUserId,
        string actorRole,
        Guid? actorDoctorId,
        Guid? actorPatientId,
        Guid? appointmentId,
        Guid? patientId,
        Guid? medicalReportId,
        string action,
        string outcome = "Success",
        string? ipAddress = null,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        var audit = new MedicalAccessAudit(
            actorUserId,
            actorRole,
            actorDoctorId,
            actorPatientId,
            appointmentId,
            patientId,
            medicalReportId,
            action,
            outcome,
            ipAddress,
            correlationId
        );

        _db.MedicalAccessAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<MedicalAccessAuditDto>> GetAuditsByAppointmentAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var audits = await _db.MedicalAccessAudits
            .AsNoTracking()
            .Where(a => a.AppointmentId == appointmentId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new MedicalAccessAuditDto(
                a.Id,
                a.ActorUserId,
                a.ActorRole,
                a.ActorDoctorId,
                a.ActorPatientId,
                a.AppointmentId,
                a.PatientId,
                a.MedicalReportId,
                a.Action,
                a.Outcome,
                a.IpAddress,
                a.Timestamp,
                a.CorrelationId
            ))
            .ToListAsync(ct);

        return audits;
    }

    #endregion
}

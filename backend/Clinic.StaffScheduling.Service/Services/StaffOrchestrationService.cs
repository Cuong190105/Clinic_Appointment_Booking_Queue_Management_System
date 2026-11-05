namespace Clinic.StaffScheduling.Service.Services;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Contracts.Events;
using Clinic.StaffScheduling.Service.DTOs;
using Clinic.StaffScheduling.Service.Entities;
using Clinic.StaffScheduling.Service.Enums;
using Clinic.StaffScheduling.Service.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public interface IStaffOrchestrationService
{
    Task<IResult> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct);
    Task<IResult> CreateEmployeeFromUserAsync(CreateEmployeeFromUserRequest request, CancellationToken ct);
    Task<IResult> UpdateEmployeeAsync(Guid id, AdminUpdateEmployeeRequest request, CancellationToken ct);
    Task<IResult> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct);
    Task<IResult> CreateDoctorFromUserAsync(CreateDoctorFromUserRequest request, CancellationToken ct);
    Task<IResult> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken ct);
}

public class StaffOrchestrationService : IStaffOrchestrationService
{
    private static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();
    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("ClinicTimeZone", TimeSpan.FromHours(7), "Clinic", "Clinic"); }
    }
    private static DateOnly ClinicToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ClinicTimeZone).DateTime);

    private readonly SchedulingDbContext _db;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ISchedulingOutboxWriter _outbox;
    private readonly string? _serviceToken;
    private readonly ILogger<StaffOrchestrationService> _logger;

    public StaffOrchestrationService(
        SchedulingDbContext db,
        IHttpClientFactory clientFactory,
        ISchedulingOutboxWriter outbox,
        IConfiguration configuration,
        ILogger<StaffOrchestrationService> logger)
    {
        _db = db;
        _clientFactory = clientFactory;
        _outbox = outbox;
        _serviceToken = configuration["Identity:ServiceToken"] ?? configuration["Services:InternalToken"];
        _logger = logger;
    }

    private HttpClient IdentityClient => _clientFactory.CreateClient("Identity");
    private HttpClient MedicalRecordsClient => _clientFactory.CreateClient("MedicalRecords");

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_serviceToken))
        {
            req.Headers.TryAddWithoutValidation("X-Identity-Service-Token", _serviceToken);
            req.Headers.TryAddWithoutValidation("X-Clinic-Service-Token", _serviceToken);
        }
        return req;
    }

    public async Task<IResult> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Results.BadRequest(new { message = "Full name is required." });

        var hireDate = request.HireDate ?? ClinicToday;
        if (hireDate > ClinicToday.AddDays(30))
            return Results.BadRequest(new { message = "Hire date cannot be more than 30 days in the future." });

        if (request.ManagerId.HasValue && !await _db.Employees.AnyAsync(e => e.Id == request.ManagerId && !e.IsDeleted, ct))
            return Results.BadRequest(new { message = "Manager not found." });

        // If username is provided, this is a full staff account provisioning workflow
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
                return Results.BadRequest(new { message = "Password must be at least 8 characters." });

            var phone = request.PhoneNumber?.Trim();
            if (string.IsNullOrWhiteSpace(phone))
                return Results.BadRequest(new { message = "Phone number is required for account creation." });

            // Check phone uniqueness among employees
            if (await _db.Employees.AnyAsync(e => e.PhoneNumber == phone && !e.IsDeleted, ct))
                return Results.Conflict(new { code = "phone_exists", message = "An employee with this phone number already exists." });

            var personId = request.PersonId ?? Guid.NewGuid();
            var identityUserId = Guid.NewGuid();
            var roles = (request.Roles ?? ["Receptionist"]).Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            // 1. Resolve patient profile from Medical Records
            IdentityRegistrationPatientIds? patientIds = null;
            try
            {
                using var resolveReq = CreateServiceRequest(HttpMethod.Get, $"internal/patients/resolve-registration?phoneNumber={Uri.EscapeDataString(phone)}&personId={personId}");
                using var resolveRes = await MedicalRecordsClient.SendAsync(resolveReq, ct);
                if (resolveRes.IsSuccessStatusCode)
                {
                    patientIds = await resolveRes.Content.ReadFromJsonAsync<IdentityRegistrationPatientIds>(cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Medical records resolve-registration failed for phone {Phone}", phone);
            }

            if (patientIds is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            // 2. Provision account in Identity
            IdentityProvisionResult? provisioned = null;
            try
            {
                using var provReq = CreateServiceRequest(HttpMethod.Post, "v1/internal/users/provision");
                provReq.Content = JsonContent.Create(new
                {
                    userId = identityUserId,
                    personId,
                    username = request.Username.Trim(),
                    password = request.Password,
                    fullName = request.FullName.Trim(),
                    phoneNumber = phone,
                    email = request.Email?.Trim(),
                    dateOfBirth = request.DateOfBirth ?? new DateOnly(1990, 1, 1),
                    gender = request.Gender ?? "Other",
                    address = request.Address,
                    roles,
                    patientId = patientIds.PatientId
                });
                using var provRes = await IdentityClient.SendAsync(provReq, ct);
                if (provRes.StatusCode == HttpStatusCode.Conflict || provRes.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorPayload = await provRes.Content.ReadAsStringAsync(ct);
                    return Results.Content(errorPayload, "application/json", statusCode: (int)provRes.StatusCode);
                }
                if (!provRes.IsSuccessStatusCode)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                provisioned = await provRes.Content.ReadFromJsonAsync<IdentityProvisionResult>(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Identity provision request failed for username {Username}", request.Username);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (provisioned is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            identityUserId = provisioned.UserId;
            var patientId = provisioned.PatientId ?? patientIds.PatientId;

            // 3. Provision patient projection in Medical Records
            bool mrSuccess = false;
            try
            {
                using var mrReq = CreateServiceRequest(HttpMethod.Post, "internal/patients/provision-identity");
                mrReq.Content = JsonContent.Create(new IdentityPatientProvisionRequest(
                    patientId, personId, identityUserId,
                    request.FullName.Trim(), phone, request.Email?.Trim(),
                    request.DateOfBirth ?? new DateOnly(1990, 1, 1),
                    request.Gender ?? "Other", request.Address));
                using var mrRes = await MedicalRecordsClient.SendAsync(mrReq, ct);
                mrSuccess = mrRes.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Medical records provision-identity failed for userId {UserId}", identityUserId);
            }

            if (!mrSuccess)
            {
                // Compensation: leave identity user inactive (not calling activate)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // 4. Save Employee locally in SchedulingDbContext
            var employee = new Employee
            {
                Id = request.Id ?? Guid.NewGuid(),
                PersonId = personId,
                IdentityUserId = identityUserId,
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                Email = request.Email?.Trim(),
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                HireDate = hireDate,
                Status = EmployeeStatus.Active,
                ManagerId = request.ManagerId
            };

            if (employee.ManagerId == employee.Id)
                return Results.BadRequest(new { message = "An employee cannot be their own manager." });

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync(ct);

            // 5. Update user roles and activate in Identity
            try
            {
                using var roleReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{identityUserId}/roles");
                roleReq.Content = JsonContent.Create(new
                {
                    roles,
                    patientId,
                    employeeId = employee.Id,
                    doctorId = (Guid?)null
                });
                using var roleRes = await IdentityClient.SendAsync(roleReq, ct);
                if (!roleRes.IsSuccessStatusCode)
                    _logger.LogWarning("Identity role update returned status {StatusCode} for employee {EmployeeId}", (int)roleRes.StatusCode, employee.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update identity roles for employee {EmployeeId}", employee.Id);
            }

            return Results.Created($"/employees/{employee.Id}", EmployeeDetail(employee, null, roles));
        }

        // Direct / internal creation without username/password
        if (request.PersonId == null || request.PersonId == Guid.Empty || request.IdentityUserId == null || request.IdentityUserId == Guid.Empty)
            return Results.BadRequest(new { message = "PersonId and IdentityUserId are required for direct employee creation." });

        var existing = await _db.Employees.FirstOrDefaultAsync(e => e.PersonId == request.PersonId.Value && !e.IsDeleted, ct);
        if (existing is not null)
        {
            if (existing.IdentityUserId is Guid linkedUser && linkedUser != request.IdentityUserId.Value)
                return Results.Conflict(new { message = "Person is already linked to a different employee account." });
            existing.IdentityUserId = request.IdentityUserId.Value;
            await _db.SaveChangesAsync(ct);
            return Results.Ok(EmployeeDetail(existing, null, null));
        }

        var directEmployee = new Employee
        {
            Id = request.Id ?? Guid.NewGuid(),
            PersonId = request.PersonId.Value,
            IdentityUserId = request.IdentityUserId.Value,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            HireDate = hireDate,
            Status = request.Status,
            ManagerId = request.ManagerId
        };

        if (directEmployee.ManagerId == directEmployee.Id)
            return Results.BadRequest(new { message = "An employee cannot be their own manager." });

        _db.Employees.Add(directEmployee);
        await _db.SaveChangesAsync(ct);
        return Results.Created($"/employees/{directEmployee.Id}", EmployeeDetail(directEmployee, null, request.Roles));
    }

    public async Task<IResult> CreateEmployeeFromUserAsync(CreateEmployeeFromUserRequest request, CancellationToken ct)
    {
        if (await _db.Employees.AnyAsync(e => e.IdentityUserId == request.UserId && !e.IsDeleted, ct))
            return Results.Conflict(new { message = "Employee profile already exists for this user." });

        // Load profile from Identity
        JsonDocument? userDoc = null;
        try
        {
            using var userReq = CreateServiceRequest(HttpMethod.Get, $"v1/internal/users/{request.UserId}");
            using var userRes = await IdentityClient.SendAsync(userReq, ct);
            if (userRes.StatusCode == HttpStatusCode.NotFound) return Results.NotFound();
            if (!userRes.IsSuccessStatusCode) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            userDoc = await userRes.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load identity user {UserId}", request.UserId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (userDoc is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        var root = userDoc.RootElement;
        var personId = root.GetProperty("personId").GetGuid();
        var fullName = root.GetProperty("fullName").GetString() ?? string.Empty;
        var phoneNumber = root.TryGetProperty("phoneNumber", out var pProp) ? pProp.GetString() : null;
        var email = root.TryGetProperty("email", out var eProp) ? eProp.GetString() : null;
        var gender = root.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null;
        var dateOfBirth = root.TryGetProperty("dateOfBirth", out var dProp) && dProp.TryGetDateTime(out var dt) ? DateOnly.FromDateTime(dt) : (DateOnly?)null;
        var address = root.TryGetProperty("address", out var aProp) ? aProp.GetString() : null;
        var patientId = root.TryGetProperty("patientId", out var ptProp) && ptProp.ValueKind == JsonValueKind.String ? ptProp.GetGuid() : (Guid?)null;
        var doctorId = root.TryGetProperty("doctorId", out var docProp) && docProp.ValueKind == JsonValueKind.String ? docProp.GetGuid() : (Guid?)null;

        var existingRoles = root.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0);
        var combinedRoles = existingRoles.Concat(request.Roles ?? []).Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            IdentityUserId = request.UserId,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Email = email,
            Gender = gender,
            DateOfBirth = dateOfBirth,
            Address = address,
            HireDate = request.HireDate,
            Status = EmployeeStatus.Active
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        // Update Identity roles
        try
        {
            using var roleReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{request.UserId}/roles");
            roleReq.Content = JsonContent.Create(new
            {
                roles = combinedRoles,
                patientId,
                employeeId = employee.Id,
                doctorId
            });
            await IdentityClient.SendAsync(roleReq, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update identity roles for employee {EmployeeId}", employee.Id);
        }

        return Results.Created($"/employees/{employee.Id}", EmployeeDetail(employee, null, combinedRoles));
    }

    public async Task<IResult> UpdateEmployeeAsync(Guid id, AdminUpdateEmployeeRequest request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
        if (employee is null) return Results.NotFound();

        if (request.ManagerId == id)
            return Results.BadRequest(new { message = "An employee cannot be their own manager." });
        if (request.ManagerId.HasValue && !await _db.Employees.AnyAsync(e => e.Id == request.ManagerId && !e.IsDeleted, ct))
            return Results.BadRequest(new { message = "Manager not found." });

        employee.FullName = request.FullName.Trim();
        employee.PhoneNumber = request.PhoneNumber;
        employee.Email = request.Email;
        employee.Gender = request.Gender;
        employee.DateOfBirth = request.DateOfBirth;
        employee.Address = request.Address;
        employee.ManagerId = request.ManagerId;
        employee.UpdatedAt = DateTime.UtcNow;

        if (employee.IdentityUserId is Guid userId)
        {
            try
            {
                using var profReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{userId}/profile");
                profReq.Content = JsonContent.Create(new
                {
                    fullName = request.FullName.Trim(),
                    phoneNumber = request.PhoneNumber,
                    email = request.Email,
                    dateOfBirth = request.DateOfBirth ?? new DateOnly(1990, 1, 1),
                    gender = request.Gender ?? "Other",
                    address = request.Address
                });
                await IdentityClient.SendAsync(profReq, ct);

                if (request.Roles is { Count: > 0 })
                {
                    using var userReq = CreateServiceRequest(HttpMethod.Get, $"v1/internal/users/{userId}");
                    using var userRes = await IdentityClient.SendAsync(userReq, ct);
                    if (userRes.IsSuccessStatusCode)
                    {
                        var userDoc = await userRes.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                        if (userDoc is not null)
                        {
                            var r = userDoc.RootElement;
                            var patientId = r.TryGetProperty("patientId", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetGuid() : (Guid?)null;
                            var doctorId = r.TryGetProperty("doctorId", out var doc) && doc.ValueKind == JsonValueKind.String ? doc.GetGuid() : (Guid?)null;
                            var currentRoles = r.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? "");
                            var allRoles = request.Roles.Concat(currentRoles.Where(x => x is "Doctor" or "Patient")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                            using var roleReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{userId}/roles");
                            roleReq.Content = JsonContent.Create(new { roles = allRoles, patientId, employeeId = id, doctorId });
                            await IdentityClient.SendAsync(roleReq, ct);
                        }
                    }
                }

                _outbox.Add(new EventEnvelope<UserProfileUpdated>
                {
                    EventId = Guid.NewGuid(),
                    EventType = "UserProfileUpdated",
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid(),
                    Producer = "Clinic.StaffScheduling.Service",
                    Payload = new UserProfileUpdated(employee.PersonId, employee.FullName, employee.PhoneNumber, employee.Email, employee.Gender, employee.DateOfBirth, employee.Address, null, userId)
                }, "user.profile.updated");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Identity profile update failed for employee {EmployeeId}", id);
            }
        }

        await _db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    public async Task<IResult> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct)
    {
        var hireDate = request.HireDate ?? ClinicToday;
        if (request.ExperienceYears < 0 || string.IsNullOrWhiteSpace(request.LicenseNumber) || string.IsNullOrWhiteSpace(request.Qualification) || hireDate > ClinicToday.AddDays(30))
            return Results.BadRequest(new { message = "Invalid doctor details." });

        if (!request.SpecialtyId.HasValue) return Results.BadRequest(new { message = "Specialty not found." });
        var specialty = await _db.Specialties.FirstOrDefaultAsync(s => s.Id == request.SpecialtyId.Value && !s.IsDeleted, ct);
        if (specialty is null) return Results.BadRequest(new { message = "Specialty not found." });

        if (await _db.Doctors.AnyAsync(d => d.LicenseNumber == request.LicenseNumber && !d.IsDeleted, ct))
            return Results.Conflict(new { message = "License number already exists." });

        if (string.IsNullOrWhiteSpace(request.Status) || !Enum.TryParse<DoctorStatus>(request.Status, true, out var parsedStatus))
            return Results.BadRequest(new { message = "Invalid doctor status." });

        // If username provided, full doctor provisioning
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
                return Results.BadRequest(new { message = "Password must be at least 8 characters." });

            var phone = request.PhoneNumber?.Trim();
            if (string.IsNullOrWhiteSpace(phone))
                return Results.BadRequest(new { message = "Phone number is required." });

            if (await _db.Employees.AnyAsync(e => e.PhoneNumber == phone && !e.IsDeleted, ct))
                return Results.Conflict(new { code = "phone_exists", message = "Phone number is already registered for an existing employee." });

            var personId = request.PersonId ?? Guid.NewGuid();
            var identityUserId = Guid.NewGuid();
            var roles = new[] { "Doctor", "Patient" };

            // 1. Resolve patient profile
            IdentityRegistrationPatientIds? patientIds = null;
            try
            {
                using var resolveReq = CreateServiceRequest(HttpMethod.Get, $"internal/patients/resolve-registration?phoneNumber={Uri.EscapeDataString(phone)}&personId={personId}");
                using var resolveRes = await MedicalRecordsClient.SendAsync(resolveReq, ct);
                if (resolveRes.IsSuccessStatusCode)
                    patientIds = await resolveRes.Content.ReadFromJsonAsync<IdentityRegistrationPatientIds>(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Medical records resolve-registration failed for doctor phone {Phone}", phone);
            }

            if (patientIds is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            // 2. Provision account in Identity
            IdentityProvisionResult? provisioned = null;
            try
            {
                using var provReq = CreateServiceRequest(HttpMethod.Post, "v1/internal/users/provision");
                provReq.Content = JsonContent.Create(new
                {
                    userId = identityUserId,
                    personId,
                    username = request.Username.Trim(),
                    password = request.Password,
                    fullName = request.FullName.Trim(),
                    phoneNumber = phone,
                    email = request.Email?.Trim(),
                    dateOfBirth = request.DateOfBirth ?? new DateOnly(1985, 1, 1),
                    gender = request.Gender ?? "Other",
                    address = request.Address,
                    roles,
                    patientId = patientIds.PatientId
                });
                using var provRes = await IdentityClient.SendAsync(provReq, ct);
                if (provRes.StatusCode == HttpStatusCode.Conflict || provRes.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorPayload = await provRes.Content.ReadAsStringAsync(ct);
                    return Results.Content(errorPayload, "application/json", statusCode: (int)provRes.StatusCode);
                }
                if (!provRes.IsSuccessStatusCode) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                provisioned = await provRes.Content.ReadFromJsonAsync<IdentityProvisionResult>(cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Identity provisioning failed for doctor username {Username}", request.Username);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            if (provisioned is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            identityUserId = provisioned.UserId;
            var patientId = provisioned.PatientId ?? patientIds.PatientId;

            // 3. Provision patient projection in Medical Records
            bool mrSuccess = false;
            try
            {
                using var mrReq = CreateServiceRequest(HttpMethod.Post, "internal/patients/provision-identity");
                mrReq.Content = JsonContent.Create(new IdentityPatientProvisionRequest(
                    patientId, personId, identityUserId,
                    request.FullName.Trim(), phone, request.Email?.Trim(),
                    request.DateOfBirth ?? new DateOnly(1985, 1, 1),
                    request.Gender ?? "Other", request.Address));
                using var mrRes = await MedicalRecordsClient.SendAsync(mrReq, ct);
                mrSuccess = mrRes.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Medical records provision-identity failed for doctor userId {UserId}", identityUserId);
            }

            if (!mrSuccess) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            // 4. Save Employee, Doctor, WorkHistory in DB
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                IdentityUserId = identityUserId,
                FullName = request.FullName.Trim(),
                PhoneNumber = phone,
                Email = request.Email?.Trim(),
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                HireDate = hireDate,
                Status = EmployeeStatus.Active
            };
            _db.Employees.Add(employee);

            var doctor = new Doctor
            {
                Id = request.Id ?? Guid.NewGuid(),
                EmployeeId = employee.Id,
                Employee = employee,
                LicenseNumber = request.LicenseNumber.Trim(),
                Qualification = request.Qualification.Trim(),
                ExperienceYears = request.ExperienceYears,
                Biography = request.Biography,
                Status = parsedStatus
            };
            _db.Doctors.Add(doctor);
            _db.WorkHistories.Add(new WorkHistory
            {
                Id = Guid.NewGuid(),
                DoctorId = doctor.Id,
                Doctor = doctor,
                SpecialtyId = specialty.Id,
                Specialty = specialty,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            // 5. Update user roles and activate in Identity
            try
            {
                using var roleReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{identityUserId}/roles");
                roleReq.Content = JsonContent.Create(new
                {
                    roles,
                    patientId,
                    employeeId = employee.Id,
                    doctorId = doctor.Id
                });
                await IdentityClient.SendAsync(roleReq, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update identity roles for doctor {DoctorId}", doctor.Id);
            }

            return Results.Created($"/doctors/{doctor.Id}", DoctorDetail(doctor, employee, specialty.Name));
        }

        // Direct / internal doctor creation
        Employee emp;
        if (request.EmployeeId is Guid employeeId)
        {
            var existingEmployee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct);
            if (existingEmployee is null)
            {
                emp = new Employee
                {
                    Id = employeeId,
                    PersonId = request.PersonId ?? Guid.NewGuid(),
                    IdentityUserId = request.IdentityUserId,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    Address = request.Address,
                    HireDate = hireDate,
                    Status = EmployeeStatus.Active
                };
                _db.Employees.Add(emp);
            }
            else emp = existingEmployee;

            if (request.PersonId.HasValue && emp.PersonId != request.PersonId.Value)
                return Results.Conflict(new { message = "Identity person does not match the employee profile." });
            if (request.IdentityUserId.HasValue && emp.IdentityUserId.HasValue && emp.IdentityUserId != request.IdentityUserId)
                return Results.Conflict(new { message = "Employee is linked to a different identity account." });
            if (request.IdentityUserId.HasValue) emp.IdentityUserId = request.IdentityUserId.Value;

            var existingDoctor = await _db.Doctors.Include(d => d.WorkHistories).ThenInclude(h => h.Specialty)
                .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && !d.IsDeleted, ct);
            if (existingDoctor is not null)
            {
                await _db.SaveChangesAsync(ct);
                var existingSpecialty = existingDoctor.WorkHistories.FirstOrDefault(h => !h.IsDeleted && h.EndDate == null)?.Specialty?.Name;
                return Results.Ok(DoctorDetail(existingDoctor, emp, existingSpecialty));
            }
        }
        else
        {
            if (request.PersonId == null || request.PersonId == Guid.Empty || request.IdentityUserId == null || request.IdentityUserId == Guid.Empty ||
                await _db.Employees.AnyAsync(e => e.PersonId == request.PersonId.Value && !e.IsDeleted, ct))
                return Results.Conflict(new { message = "Employee profile already exists or identity linkage is missing." });

            emp = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = request.PersonId.Value,
                IdentityUserId = request.IdentityUserId.Value,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                HireDate = hireDate,
                Status = EmployeeStatus.Active
            };
            _db.Employees.Add(emp);
        }

        var doc = new Doctor
        {
            Id = request.Id ?? Guid.NewGuid(),
            EmployeeId = emp.Id,
            Employee = emp,
            LicenseNumber = request.LicenseNumber.Trim(),
            Qualification = request.Qualification.Trim(),
            ExperienceYears = request.ExperienceYears,
            Biography = request.Biography,
            Status = parsedStatus
        };
        _db.Doctors.Add(doc);
        _db.WorkHistories.Add(new WorkHistory
        {
            Id = Guid.NewGuid(),
            DoctorId = doc.Id,
            Doctor = doc,
            SpecialtyId = specialty.Id,
            Specialty = specialty,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Results.Created($"/doctors/{doc.Id}", DoctorDetail(doc, emp, specialty.Name));
    }

    public async Task<IResult> CreateDoctorFromUserAsync(CreateDoctorFromUserRequest request, CancellationToken ct)
    {
        var specialty = await _db.Specialties.FirstOrDefaultAsync(s => s.Id == request.SpecialtyId && !s.IsDeleted, ct);
        if (specialty is null) return Results.BadRequest(new { message = "Specialty not found." });

        if (await _db.Doctors.AnyAsync(d => d.LicenseNumber == request.LicenseNumber && !d.IsDeleted, ct))
            return Results.Conflict(new { message = "License number already exists." });

        if (string.IsNullOrWhiteSpace(request.Status) || !Enum.TryParse<DoctorStatus>(request.Status, true, out var parsedStatus))
            return Results.BadRequest(new { message = "Invalid doctor status." });

        // Load profile from Identity
        JsonDocument? userDoc = null;
        try
        {
            using var userReq = CreateServiceRequest(HttpMethod.Get, $"v1/internal/users/{request.UserId}");
            using var userRes = await IdentityClient.SendAsync(userReq, ct);
            if (userRes.StatusCode == HttpStatusCode.NotFound) return Results.NotFound();
            if (!userRes.IsSuccessStatusCode) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            userDoc = await userRes.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load identity user {UserId}", request.UserId);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (userDoc is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        var root = userDoc.RootElement;
        var personId = root.GetProperty("personId").GetGuid();
        var fullName = root.GetProperty("fullName").GetString() ?? string.Empty;
        var phoneNumber = root.TryGetProperty("phoneNumber", out var pProp) ? pProp.GetString() : null;
        var email = root.TryGetProperty("email", out var eProp) ? eProp.GetString() : null;
        var gender = root.TryGetProperty("gender", out var gProp) ? gProp.GetString() : null;
        var dateOfBirth = root.TryGetProperty("dateOfBirth", out var dProp) && dProp.TryGetDateTime(out var dt) ? DateOnly.FromDateTime(dt) : (DateOnly?)null;
        var address = root.TryGetProperty("address", out var aProp) ? aProp.GetString() : null;
        var patientId = root.TryGetProperty("patientId", out var ptProp) && ptProp.ValueKind == JsonValueKind.String ? ptProp.GetGuid() : (Guid?)null;

        var existingRoles = root.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0);
        var combinedRoles = existingRoles.Append("Doctor").Append("Patient").Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == request.UserId && !e.IsDeleted, ct);
        if (employee is null)
        {
            employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                IdentityUserId = request.UserId,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                Email = email,
                Gender = gender,
                DateOfBirth = dateOfBirth,
                Address = address,
                HireDate = request.HireDate,
                Status = EmployeeStatus.Active
            };
            _db.Employees.Add(employee);
        }

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Employee = employee,
            LicenseNumber = request.LicenseNumber.Trim(),
            Qualification = request.Qualification.Trim(),
            ExperienceYears = request.ExperienceYears,
            Biography = request.Biography,
            Status = parsedStatus
        };
        _db.Doctors.Add(doctor);
        _db.WorkHistories.Add(new WorkHistory
        {
            Id = Guid.NewGuid(),
            DoctorId = doctor.Id,
            Doctor = doctor,
            SpecialtyId = specialty.Id,
            Specialty = specialty,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        // Update Identity roles
        try
        {
            using var roleReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{request.UserId}/roles");
            roleReq.Content = JsonContent.Create(new
            {
                roles = combinedRoles,
                patientId,
                employeeId = employee.Id,
                doctorId = doctor.Id
            });
            await IdentityClient.SendAsync(roleReq, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update identity roles for doctor {DoctorId}", doctor.Id);
        }

        return Results.Created($"/doctors/{doctor.Id}", DoctorDetail(doctor, employee, specialty.Name));
    }

    public async Task<IResult> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken ct)
    {
        var doctor = await _db.Doctors.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
        if (doctor is null) return Results.NotFound();

        if (request.ExperienceYears < 0) return Results.BadRequest();
        if (await _db.Doctors.AnyAsync(d => d.Id != id && d.LicenseNumber == request.LicenseNumber && !d.IsDeleted, ct))
            return Results.Conflict();

        doctor.LicenseNumber = request.LicenseNumber;
        doctor.Qualification = request.Qualification;
        doctor.ExperienceYears = request.ExperienceYears;
        doctor.Biography = request.Biography;
        doctor.UpdatedAt = DateTime.UtcNow;

        doctor.Employee.FullName = request.FullName;
        doctor.Employee.PhoneNumber = request.PhoneNumber;
        doctor.Employee.Email = request.Email;
        doctor.Employee.Gender = request.Gender;
        doctor.Employee.DateOfBirth = request.DateOfBirth;
        doctor.Employee.Address = request.Address;
        doctor.Employee.UpdatedAt = DateTime.UtcNow;

        if (request.SpecialtyId.HasValue && request.SpecialtyId.Value != Guid.Empty)
        {
            var specialty = await _db.Specialties.FirstOrDefaultAsync(s => s.Id == request.SpecialtyId.Value && !s.IsDeleted, ct);
            if (specialty is not null)
            {
                var today = ClinicToday;
                var active = await _db.WorkHistories.FirstOrDefaultAsync(h => h.DoctorId == id && h.EndDate == null && !h.IsDeleted, ct);
                if (active?.SpecialtyId != request.SpecialtyId.Value)
                {
                    if (active is not null) { active.EndDate = today; active.UpdatedAt = DateTime.UtcNow; }
                    _db.WorkHistories.Add(new WorkHistory { Id = Guid.NewGuid(), DoctorId = id, SpecialtyId = request.SpecialtyId.Value, StartDate = today, CreatedAt = DateTime.UtcNow });
                }
            }
        }

        if (doctor.Employee.IdentityUserId is Guid userId)
        {
            try
            {
                using var profReq = CreateServiceRequest(HttpMethod.Put, $"v1/internal/users/{userId}/profile");
                profReq.Content = JsonContent.Create(new
                {
                    fullName = request.FullName.Trim(),
                    phoneNumber = request.PhoneNumber,
                    email = request.Email,
                    dateOfBirth = request.DateOfBirth ?? new DateOnly(1985, 1, 1),
                    gender = request.Gender ?? "Other",
                    address = request.Address
                });
                await IdentityClient.SendAsync(profReq, ct);

                _outbox.Add(new EventEnvelope<UserProfileUpdated>
                {
                    EventId = Guid.NewGuid(),
                    EventType = "UserProfileUpdated",
                    SchemaVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid(),
                    Producer = "Clinic.StaffScheduling.Service",
                    Payload = new UserProfileUpdated(doctor.Employee.PersonId, doctor.Employee.FullName, doctor.Employee.PhoneNumber, doctor.Employee.Email, doctor.Employee.Gender, doctor.Employee.DateOfBirth, doctor.Employee.Address, null, userId)
                }, "user.profile.updated");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update identity profile for doctor {DoctorId}", id);
            }
        }

        await _db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static object EmployeeDetail(Employee employee, string? managerName, IReadOnlyList<string>? roles) => new
    {
        id = employee.Id,
        userId = employee.IdentityUserId,
        fullName = employee.FullName,
        phoneNumber = employee.PhoneNumber,
        email = employee.Email,
        gender = employee.Gender,
        dateOfBirth = employee.DateOfBirth,
        address = employee.Address,
        hireDate = employee.HireDate,
        status = employee.Status.ToString(),
        managerName,
        roles = roles ?? Array.Empty<string>()
    };

    private static object DoctorDetail(Doctor doctor, Employee employee, string? specialtyName) => new
    {
        id = doctor.Id,
        employeeId = employee.Id,
        fullName = employee.FullName,
        phoneNumber = employee.PhoneNumber,
        email = employee.Email,
        gender = employee.Gender,
        licenseNumber = doctor.LicenseNumber,
        qualification = doctor.Qualification,
        currentSpecialty = specialtyName,
        experienceYears = doctor.ExperienceYears,
        status = doctor.Status.ToString(),
        biography = doctor.Biography
    };
}


using System.Security.Claims;
using Clinic.Contracts.Events;
using Clinic.Contracts.Serialization;
using Clinic.Scheduling.Service.DTOs;
using Clinic.Scheduling.Service.Entities;
using Clinic.Scheduling.Service.Enums;
using Clinic.Scheduling.Service.Persistence;
using Clinic.Scheduling.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Scheduling.Service.Endpoints;

public static class StaffSchedulingEndpoints
{
    private static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("ClinicTimeZone", TimeSpan.FromHours(7), "Clinic", "Clinic"); }
    }

    private static DateOnly ClinicToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ClinicTimeZone).DateTime);
    private static DateTime ToUtc(DateOnly date, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), ClinicTimeZone);

    public static IEndpointRouteBuilder MapStaffSchedulingApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/employees", async ([FromQuery] string? search, [FromQuery] string? status,
            [FromQuery] string? sortBy, [FromQuery] string? orderBy,
            [FromQuery] int pageNumber, [FromQuery] int pageSize, SchedulingDbContext db, CancellationToken ct) =>
        {
            pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize == 0 ? 10 : pageSize, 1, 100);
            var query = db.Employees.AsNoTracking().Where(e => !e.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(e => e.FullName.Contains(search) || (e.Email != null && e.Email.Contains(search)) || (e.PhoneNumber != null && e.PhoneNumber.Contains(search)));
            if (Enum.TryParse<EmployeeStatus>(status, true, out var employeeStatus)) query = query.Where(e => e.Status == employeeStatus);
            var count = await query.CountAsync(ct);
            IOrderedQueryable<Employee> orderedEmployees = string.Equals(sortBy, "hireDate", StringComparison.OrdinalIgnoreCase)
                ? (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(e => e.HireDate) : query.OrderBy(e => e.HireDate))
                : string.Equals(sortBy, "status", StringComparison.OrdinalIgnoreCase)
                    ? (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status))
                    : (string.Equals(orderBy, "desc", StringComparison.OrdinalIgnoreCase) ? query.OrderByDescending(e => e.FullName) : query.OrderBy(e => e.FullName));
            var rows = await orderedEmployees.Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(e => new { id = e.Id, userId = e.IdentityUserId, fullName = e.FullName,
                    phoneNumber = e.PhoneNumber, email = e.Email, gender = e.Gender,
                    dateOfBirth = e.DateOfBirth, status = e.Status.ToString(),
                    roles = Array.Empty<string>() }).ToListAsync(ct);
            return Results.Ok(new { items = rows, pageNumber, pageSize, totalCount = count,
                totalPages = (int)Math.Ceiling(count / (double)pageSize), hasPrevious = pageNumber > 1,
                hasNext = pageNumber < (int)Math.Ceiling(count / (double)pageSize) });
        }).RequireAuthorization("StaffOnly");

        app.MapGet("/employees/me", async (ClaimsPrincipal principal, SchedulingDbContext db, CancellationToken ct) =>
        {
            var userId = ClaimGuid(principal, "sub", ClaimTypes.NameIdentifier);
            var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.IdentityUserId == userId && !e.IsDeleted, ct);
            return employee is null ? Results.NotFound() : Results.Ok(EmployeeDetail(employee, null));
        }).RequireAuthorization("StaffOnly");

        app.MapGet("/employees/{id:guid}", async (Guid id, SchedulingDbContext db, CancellationToken ct) =>
        {
            var employee = await db.Employees.AsNoTracking().Include(e => e.Manager).FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
            return employee is null ? Results.NotFound() : Results.Ok(EmployeeDetail(employee, employee.Manager?.FullName));
        }).RequireAuthorization();

        app.MapPost("/employees", async ([FromBody] CreateEmployeeRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            if (request.PersonId == Guid.Empty || request.IdentityUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.FullName)) return Results.BadRequest();
            var existingEmployee = await db.Employees.FirstOrDefaultAsync(e => e.PersonId == request.PersonId && !e.IsDeleted, ct);
            if (existingEmployee is not null)
            {
                if (existingEmployee.IdentityUserId is Guid linkedUser && linkedUser != request.IdentityUserId) return Results.Conflict(new { message = "Person is already linked to a different employee account." });
                existingEmployee.IdentityUserId = request.IdentityUserId;
                await db.SaveChangesAsync(ct);
                return Results.Ok(EmployeeDetail(existingEmployee, null));
            }
            var employee = new Employee { Id = request.Id ?? Guid.NewGuid(), PersonId = request.PersonId, IdentityUserId = request.IdentityUserId,
                FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber, Email = request.Email, Gender = request.Gender,
                DateOfBirth = request.DateOfBirth, Address = request.Address, HireDate = request.HireDate, Status = request.Status, ManagerId = request.ManagerId };
            if (employee.ManagerId == employee.Id) return Results.BadRequest(new { message = "An employee cannot be their own manager." });
            if (employee.HireDate > ClinicToday.AddDays(30)) return Results.BadRequest(new { message = "Hire date cannot be more than 30 days in the future." });
            if (employee.ManagerId.HasValue && !await db.Employees.AnyAsync(e => e.Id == employee.ManagerId && !e.IsDeleted, ct)) return Results.BadRequest(new { message = "Manager not found." });
            db.Employees.Add(employee); await db.SaveChangesAsync(ct);
            return Results.Created($"/employees/{employee.Id}", EmployeeDetail(employee, null));
        }).RequireAuthorization("AdminOnly");

        app.MapPut("/employees/{id:guid}", async (Guid id, [FromBody] UpdateEmployeeRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
            if (employee is null) return Results.NotFound();
            if (request.ManagerId == id) return Results.BadRequest(new { message = "An employee cannot be their own manager." });
            if (request.ManagerId.HasValue && !await db.Employees.AnyAsync(e => e.Id == request.ManagerId && !e.IsDeleted, ct)) return Results.BadRequest(new { message = "Manager not found." });
            employee.FullName = request.FullName.Trim(); employee.PhoneNumber = request.PhoneNumber; employee.Email = request.Email;
            employee.Gender = request.Gender; employee.DateOfBirth = request.DateOfBirth; employee.Address = request.Address;
            employee.ManagerId = request.ManagerId; employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization();

        app.MapPut("/employees/me", async (ClaimsPrincipal principal, [FromBody] UpdateEmployeeRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var userId = ClaimGuid(principal, "sub", ClaimTypes.NameIdentifier);
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.IdentityUserId == userId && !e.IsDeleted, ct);
            if (employee is null) return Results.NotFound();
            employee.FullName = request.FullName.Trim(); employee.PhoneNumber = request.PhoneNumber; employee.Email = request.Email;
            employee.Gender = request.Gender; employee.DateOfBirth = request.DateOfBirth; employee.Address = request.Address; employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/employees/{id:guid}/status", async (Guid id, [FromBody] EmployeeStatus status, SchedulingDbContext db, CancellationToken ct) =>
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);
            if (employee is null) return Results.NotFound();
            employee.Status = status; employee.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/specialties", async ([FromQuery] string? search, [FromQuery] string? sortBy,
            [FromQuery] bool? descending, [FromQuery] int? page, [FromQuery] int? pageSize,
            SchedulingDbContext db, CancellationToken ct) =>
        {
            var pageNumber = Math.Max(page ?? 1, 1); var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var q = db.Specialties.AsNoTracking().Where(s => !s.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(s => s.Name.Contains(search));
            var total = await q.CountAsync(ct);
            q = string.Equals(sortBy, "establishedDate", StringComparison.OrdinalIgnoreCase)
                ? (descending == true ? q.OrderByDescending(s => s.EstablishedDate) : q.OrderBy(s => s.EstablishedDate))
                : (descending == true ? q.OrderByDescending(s => s.Name) : q.OrderBy(s => s.Name));
            var rows = await q.Skip((pageNumber - 1) * size).Take(size)
                .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description, s.EstablishedDate)).ToListAsync(ct);
            var result = new { items = rows, pageNumber, pageSize = size, totalCount = total,
                totalPages = (int)Math.Ceiling(total / (double)size), hasPrevious = pageNumber > 1,
                hasNext = pageNumber < (int)Math.Ceiling(total / (double)size) };
            return Results.Ok(new { isSuccess = true, result });
        }).AllowAnonymous();

        app.MapGet("/specialties/{id:guid}", async (Guid id, SchedulingDbContext db, CancellationToken ct) =>
        {
            var s = await db.Specialties.AsNoTracking().Where(s => s.Id == id && !s.IsDeleted)
                .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description, s.EstablishedDate)).FirstOrDefaultAsync(ct);
            return s is null ? Results.NotFound() : Results.Ok(s);
        }).AllowAnonymous();

        app.MapPost("/specialties", async ([FromBody] CreateSpecialtyRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest();
            if (await db.Specialties.AnyAsync(s => s.Name == request.Name && !s.IsDeleted, ct)) return Results.Conflict(new { message = "Specialty name already exists." });
            var specialty = new Specialty { Id = Guid.NewGuid(), Name = request.Name.Trim(), Description = request.Description, EstablishedDate = request.EstablishedDate };
            db.Specialties.Add(specialty); await db.SaveChangesAsync(ct);
            return Results.Created($"/specialties/{specialty.Id}", new SpecialtyDto(specialty.Id, specialty.Name, specialty.Description, specialty.EstablishedDate));
        }).RequireAuthorization("AdminOnly");

        app.MapPut("/specialties/{id:guid}", async (Guid id, [FromBody] UpdateSpecialtyRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var s = await db.Specialties.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct); if (s is null) return Results.NotFound();
            if (await db.Specialties.AnyAsync(x => x.Id != id && x.Name == request.Name && !x.IsDeleted, ct)) return Results.Conflict();
            s.Name = request.Name.Trim(); s.Description = request.Description; s.EstablishedDate = request.EstablishedDate; s.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        app.MapPatch("/specialties/{id:guid}/status", async (Guid id, SchedulingDbContext db, CancellationToken ct) =>
        {
            var s = await db.Specialties.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct); if (s is null) return Results.NotFound();
            s.IsDeleted = true; s.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/doctors", async ([FromBody] CreateDoctorRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            if (request.ExperienceYears < 0 || string.IsNullOrWhiteSpace(request.LicenseNumber) || string.IsNullOrWhiteSpace(request.Qualification) || request.HireDate > ClinicToday.AddDays(30)) return Results.BadRequest();
            var specialty = await db.Specialties.FirstOrDefaultAsync(s => s.Id == request.SpecialtyId && !s.IsDeleted, ct); if (specialty is null) return Results.BadRequest(new { message = "Specialty not found." });
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            Employee employee;
            if (request.EmployeeId is Guid employeeId)
            {
                var existingEmployee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted, ct);
                if (existingEmployee is null)
                {
                    employee = new Employee { Id = employeeId, PersonId = request.PersonId, IdentityUserId = request.IdentityUserId,
                        FullName = request.FullName, PhoneNumber = request.PhoneNumber, Email = request.Email, Gender = request.Gender,
                        DateOfBirth = request.DateOfBirth, Address = request.Address, HireDate = request.HireDate, Status = EmployeeStatus.Active };
                    db.Employees.Add(employee);
                }
                else employee = existingEmployee;
                if (employee.PersonId != request.PersonId) return Results.Conflict(new { message = "Identity person does not match the employee profile." });
                if (employee.IdentityUserId is Guid linkedUser && linkedUser != request.IdentityUserId) return Results.Conflict(new { message = "Employee is linked to a different identity account." });
                employee.IdentityUserId = request.IdentityUserId;
                var existingDoctor = await db.Doctors.Include(d => d.WorkHistories).ThenInclude(h => h.Specialty)
                    .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && !d.IsDeleted, ct);
                if (existingDoctor is not null)
                {
                    if (employee.IdentityUserId != request.IdentityUserId) return Results.Conflict(new { message = "Employee already has a doctor profile linked to a different account." });
                    await db.SaveChangesAsync(ct);
                    if (tx is not null) await tx.CommitAsync(ct);
                    var existingSpecialty = existingDoctor.WorkHistories.FirstOrDefault(h => !h.IsDeleted && h.EndDate == null)?.Specialty?.Name;
                    return Results.Ok(new { id = existingDoctor.Id, employeeId = employee.Id, fullName = employee.FullName,
                        phoneNumber = employee.PhoneNumber, email = employee.Email, gender = employee.Gender,
                        licenseNumber = existingDoctor.LicenseNumber, qualification = existingDoctor.Qualification,
                        currentSpecialty = existingSpecialty, experienceYears = existingDoctor.ExperienceYears,
                        status = existingDoctor.Status.ToString(), biography = existingDoctor.Biography });
                }
            }
            else
            {
                if (request.PersonId == Guid.Empty || request.IdentityUserId == Guid.Empty || await db.Employees.AnyAsync(e => e.PersonId == request.PersonId && !e.IsDeleted, ct)) return Results.Conflict(new { message = "Employee profile already exists or identity linkage is missing." });
                employee = new Employee { Id = Guid.NewGuid(), PersonId = request.PersonId, IdentityUserId = request.IdentityUserId,
                    FullName = request.FullName, PhoneNumber = request.PhoneNumber, Email = request.Email, Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth, Address = request.Address, HireDate = request.HireDate, Status = EmployeeStatus.Active };
                db.Employees.Add(employee);
            }
            if (await db.Doctors.AnyAsync(d => d.LicenseNumber == request.LicenseNumber && !d.IsDeleted, ct)) return Results.Conflict(new { message = "License number already exists." });
            if (string.IsNullOrWhiteSpace(request.Status) || !Enum.TryParse<DoctorStatus>(request.Status, true, out var parsedStatus)) return Results.BadRequest(new { message = "Invalid doctor status." });
            var doctor = new Doctor { Id = request.Id ?? Guid.NewGuid(), EmployeeId = employee.Id, Employee = employee, LicenseNumber = request.LicenseNumber.Trim(),
                Qualification = request.Qualification.Trim(), ExperienceYears = request.ExperienceYears, Biography = request.Biography,
                Status = parsedStatus };
            db.Doctors.Add(doctor);
            db.WorkHistories.Add(new WorkHistory { Id = Guid.NewGuid(), DoctorId = doctor.Id, Doctor = doctor, SpecialtyId = specialty.Id, Specialty = specialty, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
            return Results.Created($"/doctors/{doctor.Id}", new { id = doctor.Id, employeeId = employee.Id, fullName = employee.FullName,
                phoneNumber = employee.PhoneNumber, email = employee.Email, gender = employee.Gender, licenseNumber = doctor.LicenseNumber,
                qualification = doctor.Qualification, currentSpecialty = specialty.Name, experienceYears = doctor.ExperienceYears,
                status = doctor.Status.ToString(), biography = doctor.Biography });
        }).RequireAuthorization("AdminOnly");

        app.MapPut("/doctors/{id:guid}", async (Guid id, [FromBody] UpdateDoctorRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctor = await db.Doctors.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct); if (doctor is null) return Results.NotFound();
            if (request.ExperienceYears < 0) return Results.BadRequest();
            if (await db.Doctors.AnyAsync(d => d.Id != id && d.LicenseNumber == request.LicenseNumber && !d.IsDeleted, ct)) return Results.Conflict();
            doctor.LicenseNumber = request.LicenseNumber; doctor.Qualification = request.Qualification; doctor.ExperienceYears = request.ExperienceYears; doctor.Biography = request.Biography; doctor.UpdatedAt = DateTime.UtcNow;
            // Personal data is projected from Identity. This endpoint accepts the legacy shape but only updates the local projection for compatibility.
            doctor.Employee.FullName = request.FullName; doctor.Employee.PhoneNumber = request.PhoneNumber; doctor.Employee.Email = request.Email; doctor.Employee.Gender = request.Gender; doctor.Employee.DateOfBirth = request.DateOfBirth; doctor.Employee.Address = request.Address; doctor.Employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization();

        app.MapPut("/doctors/me", async (ClaimsPrincipal principal, [FromBody] UpdateDoctorRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId"); var doctor = await db.Doctors.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct); if (doctor is null) return Results.NotFound();
            doctor.LicenseNumber = request.LicenseNumber; doctor.Qualification = request.Qualification; doctor.ExperienceYears = request.ExperienceYears; doctor.Biography = request.Biography;
            doctor.Employee.FullName = request.FullName; doctor.Employee.PhoneNumber = request.PhoneNumber; doctor.Employee.Email = request.Email; doctor.Employee.Gender = request.Gender; doctor.Employee.DateOfBirth = request.DateOfBirth; doctor.Employee.Address = request.Address;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("DoctorOnly");

        app.MapPatch("/doctors/{id:guid}/status", async (Guid id, [FromBody] string status, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct); if (doctor is null) return Results.NotFound();
            if (!Enum.TryParse<DoctorStatus>(status.Trim('"'), true, out var parsed)) return Results.BadRequest();
            doctor.Status = parsed; doctor.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        app.MapPatch("/doctors/{id:guid}/specialty", async (Guid id, [FromBody] Guid specialtyId, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct); var specialty = await db.Specialties.FirstOrDefaultAsync(s => s.Id == specialtyId && !s.IsDeleted, ct);
            if (doctor is null || specialty is null) return Results.NotFound();
            var today = ClinicToday;
            var active = await db.WorkHistories.FirstOrDefaultAsync(h => h.DoctorId == id && h.EndDate == null && !h.IsDeleted, ct);
            if (active?.SpecialtyId == specialtyId) return Results.NoContent();
            if (active is not null) { active.EndDate = today; active.UpdatedAt = DateTime.UtcNow; }
            db.WorkHistories.Add(new WorkHistory { Id = Guid.NewGuid(), DoctorId = id, SpecialtyId = specialtyId, StartDate = today });
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/doctors/{id:guid}/available", async (Guid id, [FromQuery] DateOnly? date, ISchedulingManager manager, SchedulingDbContext db, CancellationToken ct) =>
        {
            var targetDate = date ?? ClinicToday;
            var slots = await manager.GetAvailabilityAsync(new AvailabilityQuery(id, null, targetDate, targetDate), ct);
            var shifts = await db.WorkSchedules.AsNoTracking().Where(w => w.DoctorId == id && w.Date == targetDate && w.Status == WorkScheduleStatus.Active && !w.IsDeleted)
                .Select(w => new { w.Id, w.ShiftStart, w.ShiftEnd }).ToListAsync(ct);
            var schedules = shifts.Select(w => new { id = w.Id, startTime = w.ShiftStart, endTime = w.ShiftEnd,
                timeSlot = slots.Where(s => s.ScheduleId == w.Id).Select(s => s.TimeSlot).ToList() }).ToList();
            return Results.Ok(schedules);
        }).AllowAnonymous();

        app.MapGet("/doctors/available", async ([FromQuery] Guid? specialtyId, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctors = await db.Doctors.AsNoTracking().Include(d => d.Employee).Include(d => d.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null)).ThenInclude(h => h.Specialty)
                .Where(d => !d.IsDeleted && d.Status == DoctorStatus.Active && (specialtyId == null || d.WorkHistories.Any(h => h.SpecialtyId == specialtyId)))
                .Select(d => new { id = d.Id, fullName = d.Employee.FullName, qualification = d.Qualification,
                    experienceYears = d.ExperienceYears, specialtyId = d.WorkHistories.Select(h => h.SpecialtyId).FirstOrDefault() }).ToListAsync(ct);
            return Results.Ok(doctors);
        }).AllowAnonymous();

        app.MapGet("/doctors/me", async (ClaimsPrincipal principal, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId");
            var doctor = await db.Doctors.AsNoTracking().Include(d => d.Employee).Include(d => d.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null)).ThenInclude(h => h.Specialty)
                .FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct);
            if (doctor is null) return Results.NotFound();
            var specialty = doctor.WorkHistories.FirstOrDefault()?.Specialty?.Name;
            return Results.Ok(new { id = doctor.Id, fullName = doctor.Employee.FullName,
                phoneNumber = doctor.Employee.PhoneNumber, email = doctor.Employee.Email, gender = doctor.Employee.Gender,
                dateOfBirth = doctor.Employee.DateOfBirth, address = doctor.Employee.Address, hireDate = doctor.Employee.HireDate,
                licenseNumber = doctor.LicenseNumber, qualification = doctor.Qualification, currentSpecialty = specialty,
                experienceYears = doctor.ExperienceYears, status = doctor.Status.ToString(), biography = doctor.Biography });
        }).RequireAuthorization("DoctorOnly");

        app.MapGet("/doctors/{id:guid}/staff-link", async (Guid id, SchedulingDbContext db, CancellationToken ct) =>
        {
            var row = await db.Doctors.AsNoTracking().Where(d => d.Id == id && !d.IsDeleted)
                .Select(d => new { employeeId = d.EmployeeId, identityUserId = d.Employee.IdentityUserId }).FirstOrDefaultAsync(ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/doctors/shifts", async ([FromQuery] Guid? specialtyId, [FromQuery] string? searchTerm,
            [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] int pageNumber,
            [FromQuery] int pageSize, SchedulingDbContext db, CancellationToken ct) =>
        {
            pageNumber = Math.Max(1, pageNumber); pageSize = Math.Clamp(pageSize == 0 ? 10 : pageSize, 1, 100);
            var specialtyFilter = specialtyId.GetValueOrDefault();
            var q = db.WorkSchedules.AsNoTracking().Include(w => w.Doctor).ThenInclude(d => d.Employee)
                .Include(w => w.Doctor).ThenInclude(d => d.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null)).ThenInclude(h => h.Specialty)
                .Where(w => !w.IsDeleted && (specialtyFilter == Guid.Empty || w.Doctor.WorkHistories.Any(h => h.SpecialtyId == specialtyFilter)));
            if (startDate.HasValue) q = q.Where(w => w.Date >= startDate.Value); if (endDate.HasValue) q = q.Where(w => w.Date <= endDate.Value);
            if (!string.IsNullOrWhiteSpace(searchTerm)) q = q.Where(w => w.Doctor.Employee.FullName.Contains(searchTerm));
            var flat = await q.Select(w => new
            {
                w.DoctorId,
                DoctorName = w.Doctor.Employee.FullName,
                SpecialtyId = w.Doctor.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null).Select(h => (Guid?)h.SpecialtyId).FirstOrDefault() ?? Guid.Empty,
                SpecialtyName = w.Doctor.WorkHistories.Where(h => !h.IsDeleted && h.EndDate == null).Select(h => h.Specialty.Name).FirstOrDefault() ?? string.Empty,
                Schedule = new WorkScheduleDto(w.Id, w.DoctorId, w.Doctor.Employee.FullName, w.Date, w.ShiftStart, w.ShiftEnd, w.RecommendedCapacity, w.Status.ToString(), w.CancellationReason)
            }).ToListAsync(ct);
            var groups = flat.GroupBy(w => new { w.DoctorId, w.DoctorName, w.SpecialtyId, w.SpecialtyName })
                .Select(g => new { g.Key.DoctorId, g.Key.DoctorName, g.Key.SpecialtyId, g.Key.SpecialtyName, Schedules = g.Select(x => x.Schedule).OrderBy(x => x.Date).ThenBy(x => x.ShiftStart).ToList() })
                .OrderBy(g => g.DoctorName).ToList();
            var page = groups.Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(g => new { doctorId = g.DoctorId, doctorName = g.DoctorName, specialtyId = g.SpecialtyId, specialtyName = g.SpecialtyName, schedules = g.Schedules, startDate = startDate ?? DateOnly.MinValue, endDate = endDate ?? DateOnly.MaxValue }).ToList();
            return Results.Ok(new { items = page, pageNumber, pageSize, totalCount = groups.Count, totalPages = (int)Math.Ceiling(groups.Count / (double)pageSize), hasPrevious = pageNumber > 1, hasNext = pageNumber < (int)Math.Ceiling(groups.Count / (double)pageSize) });
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/doctors/{doctorId:guid}/suggestions", async (Guid doctorId, [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate, SchedulingDbContext db, CancellationToken ct) =>
        {
            var q = db.ShiftRequests.AsNoTracking().Where(r => r.DoctorId == doctorId && !r.IsDeleted);
            if (startDate.HasValue) q = q.Where(r => r.Date >= startDate.Value); if (endDate.HasValue) q = q.Where(r => r.Date <= endDate.Value);
            var rows = await q.OrderByDescending(r => r.Date).Select(r => new { id = r.Id, doctorId = r.DoctorId, date = r.Date, startTime = r.ShiftStart, endTime = r.ShiftEnd, recommendedCapacity = r.RecommendedCapacity, reason = r.Reason, status = r.Status.ToString() }).ToListAsync(ct);
            return Results.Ok(new { doctorId, schedules = rows, startDate = startDate ?? DateOnly.MinValue, endDate = endDate ?? DateOnly.MaxValue });
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/doctors/{doctorId:guid}/shifts", async (Guid doctorId, [FromBody] CreateShiftRequest request,
            SchedulingDbContext db, ISchedulingOutboxWriter outbox, CancellationToken ct) =>
        {
            var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted && d.Status == DoctorStatus.Active, ct); if (doctor is null) return Results.NotFound();
            if (request.EndTime <= request.StartTime || request.RecommendedCapacity <= 0 || ToUtc(request.Date, request.StartTime) <= DateTime.UtcNow) return Results.BadRequest();
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            if (await db.WorkSchedules.AnyAsync(w => w.DoctorId == doctorId && w.Date == request.Date && w.Status != WorkScheduleStatus.Cancelled && !w.IsDeleted && w.ShiftStart < request.EndTime && request.StartTime < w.ShiftEnd, ct)) return Results.Conflict(new { message = "Schedule overlaps an existing shift." });
            var schedule = new WorkSchedule { Id = Guid.NewGuid(), DoctorId = doctorId, Date = request.Date, ShiftStart = request.StartTime, ShiftEnd = request.EndTime, RecommendedCapacity = request.RecommendedCapacity, Status = WorkScheduleStatus.Active };
            db.WorkSchedules.Add(schedule);
            outbox.Add(new EventEnvelope<WorkScheduleCreated> { EventId = Guid.NewGuid(), EventType = EventTypes.WorkScheduleCreated, SchemaVersion = 1, OccurredAt = DateTimeOffset.UtcNow, CorrelationId = Guid.NewGuid(), Producer = "Clinic.Scheduling.Service", Payload = new WorkScheduleCreated(schedule.Id, doctorId, schedule.Date, schedule.ShiftStart, schedule.ShiftEnd, schedule.RecommendedCapacity) }, "schedule.created");
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
            return Results.Created($"/schedules/{schedule.Id}", new WorkScheduleDto(schedule.Id, doctorId, string.Empty, schedule.Date, schedule.ShiftStart, schedule.ShiftEnd, schedule.RecommendedCapacity, schedule.Status.ToString(), null));
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/doctors/{doctorId:guid}/shifts", async (Guid doctorId, [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctor = await db.Doctors.AsNoTracking().Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct);
            if (doctor is null) return Results.NotFound();
            var q = db.WorkSchedules.AsNoTracking().Where(w => w.DoctorId == doctorId && !w.IsDeleted);
            if (startDate.HasValue) q = q.Where(w => w.Date >= startDate.Value); if (endDate.HasValue) q = q.Where(w => w.Date <= endDate.Value);
            var rows = await q.OrderBy(w => w.Date).ThenBy(w => w.ShiftStart).Select(w => new WorkScheduleDto(w.Id, w.DoctorId, doctor.Employee.FullName, w.Date, w.ShiftStart, w.ShiftEnd, w.RecommendedCapacity, w.Status.ToString(), w.CancellationReason)).ToListAsync(ct);
            return Results.Ok(new { doctorId, doctorName = doctor.Employee.FullName, schedules = rows, startDate = startDate ?? DateOnly.MinValue, endDate = endDate ?? DateOnly.MaxValue });
        }).AllowAnonymous();

        app.MapGet("/shifts", async (ClaimsPrincipal principal, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId"); if (doctorId == Guid.Empty) return Results.Forbid();
            var doctor = await db.Doctors.AsNoTracking().Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct);
            if (doctor is null) return Results.NotFound();
            var q = db.WorkSchedules.AsNoTracking().Where(w => w.DoctorId == doctorId && !w.IsDeleted);
            if (startDate.HasValue) q = q.Where(w => w.Date >= startDate.Value); if (endDate.HasValue) q = q.Where(w => w.Date <= endDate.Value);
            var rows = await q.OrderBy(w => w.Date).ThenBy(w => w.ShiftStart).Select(w => new WorkScheduleDto(w.Id, w.DoctorId, doctor.Employee.FullName, w.Date, w.ShiftStart, w.ShiftEnd, w.RecommendedCapacity, w.Status.ToString(), w.CancellationReason)).ToListAsync(ct);
            return Results.Ok(new { doctorId, doctorName = doctor.Employee.FullName, schedules = rows, startDate = startDate ?? DateOnly.MinValue, endDate = endDate ?? DateOnly.MaxValue });
        }).RequireAuthorization("DoctorOnly");

        app.MapPost("/shifts/suggestions", async (ClaimsPrincipal principal, [FromBody] CreateShiftSuggestionRequest request, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId"); if (doctorId == Guid.Empty) return Results.Forbid();
            if (request.EndTime <= request.StartTime || request.RecommendedCapacity <= 0 || ToUtc(request.Date, request.StartTime) <= DateTime.UtcNow) return Results.BadRequest();
            if (!await db.Doctors.AnyAsync(d => d.Id == doctorId && !d.IsDeleted, ct)) return Results.NotFound();
            if (await db.ShiftRequests.AnyAsync(r => r.DoctorId == doctorId && r.Date == request.Date && r.ShiftStart == request.StartTime && r.ShiftEnd == request.EndTime && !r.IsDeleted && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct)) return Results.Conflict();
            var row = new ShiftRequest { Id = Guid.NewGuid(), DoctorId = doctorId, Date = request.Date, ShiftStart = request.StartTime, ShiftEnd = request.EndTime, RecommendedCapacity = request.RecommendedCapacity, Reason = request.Reason, Status = ShiftRequestStatus.Pending };
            db.ShiftRequests.Add(row); await db.SaveChangesAsync(ct); return Results.Created($"/shifts/suggestions/{row.Id}", ShiftSuggestionDto(row));
        }).RequireAuthorization("DoctorOnly");

        app.MapGet("/shifts/suggestions", async (ClaimsPrincipal principal, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId"); if (doctorId == Guid.Empty) return Results.Forbid();
            var doctor = await db.Doctors.AsNoTracking().Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted, ct);
            if (doctor is null) return Results.NotFound();
            var q = db.ShiftRequests.AsNoTracking().Where(r => r.DoctorId == doctorId && !r.IsDeleted);
            if (startDate.HasValue) q = q.Where(r => r.Date >= startDate.Value); if (endDate.HasValue) q = q.Where(r => r.Date <= endDate.Value);
            var rows = await q.OrderByDescending(r => r.Date).Select(r => new { id = r.Id, doctorId = r.DoctorId, date = r.Date, startTime = r.ShiftStart, endTime = r.ShiftEnd, recommendedCapacity = r.RecommendedCapacity, reason = r.Reason, status = r.Status.ToString() }).ToListAsync(ct);
            return Results.Ok(new { doctorId, doctorName = doctor.Employee.FullName, schedules = rows, startDate = startDate ?? DateOnly.MinValue, endDate = endDate ?? DateOnly.MaxValue });
        }).RequireAuthorization("DoctorOnly");

        app.MapPut("/shifts/{id:guid}", async (Guid id, [FromBody] UpdateShiftLegacyRequest request, ISchedulingManager manager, CancellationToken ct) =>
        {
            if (request.EndTime <= request.StartTime || request.RecommendedCapacityPerSlot <= 0 || ToUtc(request.Date, request.StartTime) <= DateTime.UtcNow) return Results.BadRequest();
            try { await manager.UpdateScheduleAsync(id, request.Date, request.StartTime, request.EndTime, request.RecommendedCapacityPerSlot, ct); return Results.NoContent(); }
            catch (KeyNotFoundException) { return Results.NotFound(); } catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/shifts/{id:guid}/cancel", async (Guid id, [FromBody] CancelScheduleRequest request, ISchedulingManager manager, CancellationToken ct) =>
        {
            var ok = await manager.CancelScheduleAsync(id, request.Reason, ct); return ok ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("AdminOnly");

        app.MapPatch("/shifts/suggestions/{id:guid}", async (Guid id, [FromBody] UpdateShiftSuggestionRequest request, ClaimsPrincipal principal, SchedulingDbContext db, CancellationToken ct) =>
        {
            var doctorId = ClaimGuid(principal, "doctorId");
            var row = await db.ShiftRequests.FirstOrDefaultAsync(r => r.Id == id && r.DoctorId == doctorId && !r.IsDeleted, ct); if (row is null) return Results.NotFound();
            if (row.Status != ShiftRequestStatus.Pending) return Results.Conflict(); if (request.EndTime <= request.StartTime || request.RecommendedCapacityPerSlot <= 0 || ToUtc(request.Date, request.StartTime) <= DateTime.UtcNow) return Results.BadRequest();
            row.Date = request.Date; row.ShiftStart = request.StartTime; row.ShiftEnd = request.EndTime; row.RecommendedCapacity = request.RecommendedCapacityPerSlot; row.Reason = request.Reason; row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("DoctorOnly");

        
        app.MapGet("/doctors/dashboard-stats", async (SchedulingDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var currentPeriodStart = now.AddDays(-30);
            var stats = await db.Doctors
                .Where(d => !d.IsDeleted)
                .GroupBy(d => 1)
                .Select(g => new
                {
                    CurrentTotal = g.Count(d => d.CreatedAt < now),
                    PreviousTotal = g.Count(d => d.CreatedAt < currentPeriodStart),
                    CurrentActive = g.Count(d => d.Status == DoctorStatus.Active && d.CreatedAt < now),
                    PreviousActive = g.Count(d => d.Status == DoctorStatus.Active && d.CreatedAt < currentPeriodStart)
                })
                .FirstOrDefaultAsync(ct);

            var currentTotal = stats?.CurrentTotal ?? 0;
            var previousTotal = stats?.PreviousTotal ?? 0;
            var currentActive = stats?.CurrentActive ?? 0;
            var previousActive = stats?.PreviousActive ?? 0;

            static double CalculatePercentChange(double currentValue, double previousValue)
            {
                if (previousValue == 0) return currentValue == 0 ? 0 : 100;
                return (currentValue - previousValue) / previousValue * 100;
            }

            return Results.Ok(new
            {
                totalDoctors = currentTotal,
                totalDoctorsChange = CalculatePercentChange(currentTotal, previousTotal),
                activeDoctors = currentActive,
                activeDoctorsChange = CalculatePercentChange(currentActive, previousActive)
            });
        }).AllowAnonymous();

        return app;
    }

    private static Guid ClaimGuid(ClaimsPrincipal user, params string[] types)
    {
        foreach (var type in types) if (Guid.TryParse(user.FindFirstValue(type), out var value)) return value;
        return Guid.Empty;
    }

    private static object EmployeeDetail(Employee employee, string? managerName) => new
    {
        id = employee.Id, userId = employee.IdentityUserId, fullName = employee.FullName, phoneNumber = employee.PhoneNumber,
        email = employee.Email, gender = employee.Gender, dateOfBirth = employee.DateOfBirth, address = employee.Address,
        hireDate = employee.HireDate, status = employee.Status.ToString(), managerName, roles = Array.Empty<string>()
    };

    private static object ShiftSuggestionDto(ShiftRequest row) => new
    {
        id = row.Id, doctorId = row.DoctorId, date = row.Date, startTime = row.ShiftStart, endTime = row.ShiftEnd,
        recommendedCapacity = row.RecommendedCapacity, reason = row.Reason, status = row.Status.ToString()
    };
}

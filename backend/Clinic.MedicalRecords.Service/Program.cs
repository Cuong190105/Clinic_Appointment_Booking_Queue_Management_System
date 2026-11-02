using System.Security.Claims;
using Clinic.MedicalRecords.Service.Entities;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MedicalRecordsConnection");
builder.Services.AddDbContext<MedicalRecordsDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<MedicalRecordWorkflow>();
builder.Services.AddScoped<QueueCompletionClient>();
builder.Services.AddHttpClient("Appointment", c =>
{
    c.BaseAddress = new Uri((builder.Configuration["Appointment:Authority"] ?? "http://localhost:5003").TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("Queue", c =>
{
    c.BaseAddress = new Uri((builder.Configuration["Queue:Authority"] ?? "http://localhost:5004").TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<MedicalRecordsEventConsumer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Authority = (builder.Configuration["Identity:Authority"] ?? "http://localhost:5001").TrimEnd('/');
    o.RequireHttpsMetadata = builder.Configuration.GetValue("Identity:RequireHttpsMetadata", false);
    o.Audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuers = [builder.Configuration["Jwt:Issuer"] ?? "http://localhost:5000", "Clinic.Identity"],
        ValidateAudience = true, ValidAudiences = [builder.Configuration["Jwt:Audience"] ?? "Clinic.Services", "Clinic.Client"],
        ValidateLifetime = true, ValidateIssuerSigningKey = true, RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("clinic-medical-records-service"))
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddMeter(MedicalRecordsTelemetry.MeterName).AddPrometheusExporter())
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddOtlpExporter());

var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapPrometheusScrapingEndpoint();
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGet("/readyz", async (MedicalRecordsDbContext db, CancellationToken ct) =>
{
    if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503);
    return Results.Ok(new { status = "Ready" });
}).AllowAnonymous();

app.MapGet("/medical-reports", async (string? search, MedicalRecordStatus? status, DateOnly? fromDate, DateOnly? toDate,
    int? pageNumber, int? pageSize, MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var query = db.MedicalRecords.AsNoTracking();
    if (status.HasValue) query = query.Where(x => x.Status == status);
    if (fromDate.HasValue) query = query.Where(x => x.ExamDate >= fromDate);
    if (toDate.HasValue) query = query.Where(x => x.ExamDate <= toDate);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.PatientName.Contains(search) || x.DoctorName.Contains(search));
    var page = Math.Max(1, pageNumber ?? 1); var size = Math.Clamp(pageSize ?? 10, 1, 100);
    var total = await query.CountAsync(ct);
    var items = await query.OrderByDescending(x => x.ExamDate).ThenByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
    await AuditAsync(db, http, null, "medical-record.list", ct);
    return Results.Ok(new { items, pageNumber = page, pageSize = size, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)size) });
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));

app.MapGet("/medical-reports/{id:guid}", async (Guid id, MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var record = await db.MedicalRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    if (record is null) return Results.NotFound();
    await AuditAsync(db, http, id, "medical-record.read", ct); return Results.Ok(record);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));
app.MapGet("/medical-reports/appointment/{appointmentId:guid}", async (Guid appointmentId, MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var record = await db.MedicalRecords.AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
    if (record is null) return Results.NotFound();
    await AuditAsync(db, http, record.Id, "medical-record.read", ct); return Results.Ok(record);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));
app.MapGet("/patients/{patientId:guid}/medical-history", async (Guid patientId, MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var records = await db.MedicalHistory.AsNoTracking().Where(x => x.PatientId == patientId)
        .OrderByDescending(x => x.ExamDate).ToListAsync(ct);
    await AuditAsync(db, http, null, "medical-history.read", ct); return Results.Ok(records);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));
app.MapGet("/me/medical-history", async (MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var raw = http.User.FindFirstValue("patientId") ?? http.User.FindFirstValue("personId") ??
              http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
    if (!Guid.TryParse(raw, out var patientId)) return Results.Forbid();
    var records = await db.MedicalHistory.AsNoTracking().Where(x => x.PatientId == patientId)
        .OrderByDescending(x => x.ExamDate).ToListAsync(ct);
    await AuditAsync(db, http, null, "medical-history.read-own", ct); return Results.Ok(records);
}).RequireAuthorization();
app.MapPost("/admin/medical-history/rebuild", async (MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
    var rebuilt = await MedicalHistoryProjector.RebuildAsync(db, ct);
    await AuditAsync(db, http, null, "medical-history.rebuild", ct, false);
    await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
    return Results.Ok(new { rebuilt, source = "MedicalRecords", completedAt = DateTimeOffset.UtcNow });
}).RequireAuthorization(p => p.RequireRole("Admin"));

app.MapPost("/medical-reports", async (SaveRequest request, MedicalRecordsDbContext db, HttpContext http, IHttpClientFactory clients, CancellationToken ct) =>
{
    var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.AppointmentId == request.AppointmentId, ct);
    if (record is null)
    {
        AppointmentSnapshot? appointment = null;
        if (request.PatientId == Guid.Empty || request.DoctorId == Guid.Empty)
        {
            using var lookup = new HttpRequestMessage(HttpMethod.Get, $"appointments/{request.AppointmentId}");
            if (http.Request.Headers.TryGetValue("Authorization", out var auth)) lookup.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
            using var response = await clients.CreateClient("Appointment").SendAsync(lookup, ct);
            if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);
            appointment = await response.Content.ReadFromJsonAsync<AppointmentSnapshot>(cancellationToken: ct);
            if (appointment is null) return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
        record = new MedicalRecord { AppointmentId = request.AppointmentId, PatientId = request.PatientId == Guid.Empty ? appointment!.PatientId : request.PatientId, PatientName = request.PatientName ?? appointment?.PatientName ?? "",
            DoctorId = request.DoctorId == Guid.Empty ? appointment!.DoctorId : request.DoctorId, DoctorName = request.DoctorName ?? appointment?.DoctorName ?? "", SpecialtyName = request.SpecialtyName ?? appointment?.SpecialtyName ?? "", ExamDate = request.ExamDate ?? appointment?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ExamStartTime = DateTimeOffset.UtcNow, Status = MedicalRecordStatus.Draft, WorkflowVersion = 1 };
        db.MedicalRecords.Add(record);
    }
    if (record.Status == MedicalRecordStatus.Finalized) return Results.Conflict(new { error = "Finalized records are immutable." });
    Apply(record, request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
    if (request.IsFinalize) { Finalize(record); await MedicalHistoryProjector.UpsertAsync(db, record, ct); }
    await AuditAsync(db, http, record.Id, request.IsFinalize ? "medical-record.finalize" : "medical-record.save", ct, false);
    await db.SaveChangesAsync(ct);
    if (request.IsFinalize)
    {
        var queue = http.RequestServices.GetRequiredService<QueueCompletionClient>();
        var failure = await queue.CompleteIfInProgressAsync(record.AppointmentId, http.Request.Headers.Authorization, ct);
        if (failure.HasValue) return Results.StatusCode((int)failure.Value);
    }
    return Results.Ok(record);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));
app.MapPut("/medical-reports/{id:guid}", async (Guid id, UpdateRequest request, MedicalRecordsDbContext db, HttpContext http, CancellationToken ct) =>
{
    var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.Id == id, ct); if (record is null) return Results.NotFound();
    if (record.Status == MedicalRecordStatus.Finalized) return Results.Conflict(new { error = "Finalized records are immutable." });
    Apply(record, request.Symptoms, request.Diagnosis, request.Prescription, request.Notes); if (request.IsFinalize) { Finalize(record); await MedicalHistoryProjector.UpsertAsync(db, record, ct); }
    await AuditAsync(db, http, id, request.IsFinalize ? "medical-record.finalize" : "medical-record.update", ct, false);
    await db.SaveChangesAsync(ct);
    if (request.IsFinalize)
    {
        var queue = http.RequestServices.GetRequiredService<QueueCompletionClient>();
        var failure = await queue.CompleteIfInProgressAsync(record.AppointmentId, http.Request.Headers.Authorization, ct);
        if (failure.HasValue) return Results.StatusCode((int)failure.Value);
    }
    return Results.Ok(record);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));
app.MapPost("/medical-reports/{id:guid}/finalize", async (Guid id, MedicalRecordsDbContext db, HttpContext http, QueueCompletionClient queue, CancellationToken ct) =>
{
    var record = await db.MedicalRecords.SingleOrDefaultAsync(x => x.Id == id, ct); if (record is null) return Results.NotFound();
    Finalize(record); await MedicalHistoryProjector.UpsertAsync(db, record, ct); await AuditAsync(db, http, id, "medical-record.finalize", ct, false); await db.SaveChangesAsync(ct);
    var failure = await queue.CompleteIfInProgressAsync(record.AppointmentId, http.Request.Headers.Authorization, ct);
    return failure.HasValue ? Results.StatusCode((int)failure.Value) : Results.Ok(record);
}).RequireAuthorization(p => p.RequireRole("Doctor", "Admin"));

if (!app.Configuration.GetValue<bool>("SkipDatabaseInitialization")) { await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>().Database.EnsureCreatedAsync(); }
app.Run();

static void Apply(MedicalRecord r, string? symptoms, string? diagnosis, string? prescription, string? notes) { r.Symptoms = symptoms; r.Diagnosis = diagnosis; r.Prescription = prescription; r.Notes = notes; r.UpdatedAt = DateTimeOffset.UtcNow; }
static void Finalize(MedicalRecord r) { if (r.Status == MedicalRecordStatus.Finalized) return; r.Status = MedicalRecordStatus.Finalized; r.ExamEndTime = DateTimeOffset.UtcNow; r.WorkflowVersion = Math.Max(2, r.WorkflowVersion); r.UpdatedAt = DateTimeOffset.UtcNow; }
static async Task AuditAsync(MedicalRecordsDbContext db, HttpContext http, Guid? recordId, string action, CancellationToken ct, bool save = true)
{
    var cid = Guid.TryParse(http.Request.Headers["X-Correlation-ID"], out var parsed) ? parsed : Guid.NewGuid();
    db.AuditEntries.Add(new MedicalAuditEntry { RecordId = recordId, Action = action, ActorId = http.User.FindFirstValue("sub") ?? "unknown", CorrelationId = cid });
    if (save) await db.SaveChangesAsync(ct);
}
record SaveRequest(Guid AppointmentId, Guid PatientId = default, string? PatientName = null, Guid DoctorId = default, string? DoctorName = null, string? SpecialtyName = null, DateOnly? ExamDate = null, string? Symptoms = null, string? Diagnosis = null, string? Prescription = null, string? Notes = null, bool IsFinalize = false);
record UpdateRequest(string? Symptoms, string? Diagnosis, string? Prescription, string? Notes, bool IsFinalize = false);
record AppointmentSnapshot(Guid Id, Guid PatientId, Guid DoctorId, string PatientName, string DoctorName, string? SpecialtyName, DateOnly Date);
public partial class Program { }

using System.Net;
using System.Net.Http.Json;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Endpoints;

public class MedicalRecordsAccessControlAndAuditTests : IClassFixture<MedicalRecordsServiceFactory>
{
    private readonly HttpClient _client;
    private readonly MedicalRecordsServiceFactory _factory;

    public MedicalRecordsAccessControlAndAuditTests(MedicalRecordsServiceFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Receptionist_CannotViewClinicalMedicalReport_ReturnsForbidden()
    {
        // 1. Setup Patient and MedicalReport
        var patientReq = new CreatePatientRequest("Audit Patient 1", "0921000111", null, new DateOnly(1980, 1, 1), "Male", "HCM");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        var apptId = Guid.NewGuid();
        var reportReq = new SaveMedicalReportRequest(
            AppointmentId: apptId,
            PatientId: patient.Id,
            DoctorId: Guid.NewGuid(),
            DoctorName: "Dr. Audit",
            SpecialtyName: "Cardiology",
            ScheduleDate: new DateOnly(2026, 10, 26),
            Symptoms: "Sensitive symptom",
            Diagnosis: "Sensitive diagnosis",
            Prescription: "Sensitive rx",
            Notes: "Highly confidential clinical note",
            IsFinalize: true
        );
        var reportResp = await _client.PostAsJsonAsync("/medical-records", reportReq);
        var report = await reportResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(report);

        // 2. Receptionist attempts to view clinical report -> MUST be Forbidden (403)
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/medical-records/{report.Id}");
        request.Headers.Add("X-Test-Role", "Receptionist");
        request.Headers.Add("X-Test-Permissions", "patient.view.any");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receptionist_CannotViewPatientMedicalHistory_ReturnsForbidden()
    {
        var patientReq = new CreatePatientRequest("Audit Patient 2", "0921000222", null, new DateOnly(1985, 2, 2), "Female", "Hanoi");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/patients/{patient.Id}/medical-history");
        request.Headers.Add("X-Test-Role", "Receptionist");
        request.Headers.Add("X-Test-Permissions", "patient.view.any");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receptionist_CanAccessPatientAdministrativeData_ReturnsOk()
    {
        var patientReq = new CreatePatientRequest("Admin Data Patient", "0921000333", null, new DateOnly(1990, 3, 3), "Male", "Danang");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/patients/{patient.Id}");
        request.Headers.Add("X-Test-Role", "Receptionist");
        request.Headers.Add("X-Test-Permissions", "patient.view.any");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(data);
        Assert.Equal("Admin Data Patient", data.FullName);
    }

    [Fact]
    public async Task Patient_CannotViewOtherPatientReport_ReturnsForbidden_AndLogsDeniedAudit()
    {
        var victimReq = new CreatePatientRequest("Victim Patient", "0921000444", null, new DateOnly(1992, 4, 4), "Female", "Hue");
        var victimResp = await _client.PostAsJsonAsync("/patients", victimReq);
        var victim = await victimResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(victim);

        var apptId = Guid.NewGuid();
        var reportReq = new SaveMedicalReportRequest(apptId, victim.Id, Guid.NewGuid(), "Dr. Evil", "Oncology", new DateOnly(2026, 10, 26), "s", "d", "rx", "confidential", true);
        var reportResp = await _client.PostAsJsonAsync("/medical-records", reportReq);
        var report = await reportResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(report);

        var attackerPatientId = Guid.NewGuid();

        // Attacker attempts IDOR on victim's report
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/medical-records/{report.Id}");
        request.Headers.Add("X-Test-Role", "Patient");
        request.Headers.Add("X-Test-PatientId", attackerPatientId.ToString());
        request.Headers.Add("X-Test-Permissions", "medical-report.view.own");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Verify audit log has Denied entry
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        var audit = await db.MedicalAccessAudits
            .Where(a => a.MedicalReportId == report.Id && a.Outcome == "Denied")
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal("Patient", audit.ActorRole);
    }

    [Fact]
    public async Task Patient_CanViewOwnReport_ReturnsOk_AndLogsSuccessAudit()
    {
        var ownPatientReq = new CreatePatientRequest("Own Patient Audit", "0921000555", null, new DateOnly(1995, 5, 5), "Male", "Can Tho");
        var ownPatientResp = await _client.PostAsJsonAsync("/patients", ownPatientReq);
        var ownPatient = await ownPatientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(ownPatient);

        var apptId = Guid.NewGuid();
        var reportReq = new SaveMedicalReportRequest(apptId, ownPatient.Id, Guid.NewGuid(), "Dr. Good", "General", new DateOnly(2026, 10, 26), "Cold", "Flu", "Flu meds", "Rest", true);
        var reportResp = await _client.PostAsJsonAsync("/medical-records", reportReq);
        var report = await reportResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(report);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/medical-records/{report.Id}");
        request.Headers.Add("X-Test-Role", "Patient");
        request.Headers.Add("X-Test-PatientId", ownPatient.Id.ToString());
        request.Headers.Add("X-Test-Permissions", "medical-report.view.own");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify audit log has Success entry
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        var audit = await db.MedicalAccessAudits
            .Where(a => a.MedicalReportId == report.Id && a.Outcome == "Success")
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Outcome);
    }
}

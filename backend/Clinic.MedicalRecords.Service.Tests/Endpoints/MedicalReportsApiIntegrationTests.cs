using System.Net;
using System.Net.Http.Json;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Tests.Fixtures;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Endpoints;

public class MedicalReportsApiIntegrationTests : IClassFixture<MedicalRecordsServiceFactory>
{
    private readonly HttpClient _client;

    public MedicalReportsApiIntegrationTests(MedicalRecordsServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SaveReport_AsDraft_ReturnsOkOrCreated()
    {
        var patientReq = new CreatePatientRequest("Report Patient 1", "0911000111", null, new DateOnly(1988, 8, 8), "Male", "HCM");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        var appointmentId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        var reportReq = new SaveMedicalReportRequest(
            AppointmentId: appointmentId,
            PatientId: patient.Id,
            DoctorId: doctorId,
            DoctorName: "Dr. Gregory House",
            SpecialtyName: "Infectious Disease",
            ScheduleDate: new DateOnly(2026, 10, 26),
            Symptoms: "Fever and rash",
            Diagnosis: "Differential diagnosis pending",
            Prescription: null,
            Notes: "Rule out vasculitis",
            IsFinalize: false
        );

        var response = await _client.PostAsJsonAsync("/medical-records", reportReq);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        var report = await response.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(report);
        Assert.Equal("Draft", report.Status);
        Assert.Equal(appointmentId, report.AppointmentId);
        Assert.Equal(patient.Id, report.PatientId);
    }

    [Fact]
    public async Task FinalizeReport_Succeeds_WithoutCallingQueueCompleteExam()
    {
        var patientReq = new CreatePatientRequest("Report Patient 2", "0911000222", null, new DateOnly(1985, 5, 5), "Female", "Hanoi");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        var appointmentId = Guid.NewGuid();
        var reportReq = new SaveMedicalReportRequest(
            AppointmentId: appointmentId,
            PatientId: patient.Id,
            DoctorId: Guid.NewGuid(),
            DoctorName: "Dr. James Wilson",
            SpecialtyName: "Oncology",
            ScheduleDate: new DateOnly(2026, 10, 26),
            Symptoms: "Fatigue",
            Diagnosis: "Benign cyst",
            Prescription: "Vitamins",
            Notes: "Discharged",
            IsFinalize: false
        );
        var saveResp = await _client.PostAsJsonAsync("/medical-records", reportReq);
        var savedReport = await saveResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(savedReport);

        var finalizeResp = await _client.PostAsync($"/medical-records/{savedReport.Id}/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalizeResp.StatusCode);

        var finalized = await finalizeResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(finalized);
        Assert.Equal("Finalized", finalized.Status);
        Assert.NotNull(finalized.ExamEndTime);

        // Verification: Updating draft after finalized must fail
        var updateResp = await _client.PutAsJsonAsync($"/medical-records/{savedReport.Id}", new UpdateMedicalReportRequest("New", "New", "New", "New", false));
        Assert.Equal(HttpStatusCode.BadRequest, updateResp.StatusCode);
    }

    [Fact]
    public async Task CreateAmendment_AppendsToFinalizedReport()
    {
        var patientReq = new CreatePatientRequest("Report Patient 3", "0911000333", null, new DateOnly(1992, 3, 14), "Male", "Can Tho");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        var appointmentId = Guid.NewGuid();
        var reportReq = new SaveMedicalReportRequest(
            AppointmentId: appointmentId,
            PatientId: patient.Id,
            DoctorId: Guid.NewGuid(),
            DoctorName: "Dr. Allison Cameron",
            SpecialtyName: "Immunology",
            ScheduleDate: new DateOnly(2026, 10, 26),
            Symptoms: "Allergic reaction",
            Diagnosis: "Food allergy",
            Prescription: "Antihistamine",
            Notes: "Rest",
            IsFinalize: true
        );
        var saveResp = await _client.PostAsJsonAsync("/medical-records", reportReq);
        var savedReport = await saveResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(savedReport);
        Assert.Equal("Finalized", savedReport.Status);

        var amendmentReq = new CreateAmendmentRequest(
            Reason: "Patient reported delayed reaction to peanuts, added Epipen prescription",
            Symptoms: null,
            Diagnosis: "Severe peanut allergy",
            Prescription: "Epipen 0.3mg Auto-injector + Antihistamine",
            Notes: "Instructed on Epipen usage"
        );

        var amendResp = await _client.PostAsJsonAsync($"/medical-records/appointments/{appointmentId}/amendments", amendmentReq);
        Assert.Equal(HttpStatusCode.Created, amendResp.StatusCode);

        var amendDto = await amendResp.Content.ReadFromJsonAsync<MedicalReportAmendmentDto>();
        Assert.NotNull(amendDto);
        Assert.Equal(1, amendDto.AmendmentNumber);
        Assert.Equal(amendmentReq.Reason, amendDto.Reason);

        // Fetch report by appointment to verify amendment is embedded
        var byApptResp = await _client.GetAsync($"/medical-records/appointment/{appointmentId}");
        Assert.Equal(HttpStatusCode.OK, byApptResp.StatusCode);

        var fetched = await byApptResp.Content.ReadFromJsonAsync<MedicalReportDto>();
        Assert.NotNull(fetched);
        Assert.Single(fetched.Amendments);
        Assert.Equal(1, fetched.Amendments[0].AmendmentNumber);
    }

    [Fact]
    public async Task GetPatientMedicalHistory_ReturnsListOfReports()
    {
        var patientReq = new CreatePatientRequest("History Patient", "0911000444", null, new DateOnly(1989, 9, 9), "Female", "Da Lat");
        var patientResp = await _client.PostAsJsonAsync("/patients", patientReq);
        var patient = await patientResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);

        var rep1 = new SaveMedicalReportRequest(Guid.NewGuid(), patient.Id, Guid.NewGuid(), "Dr. A", "General", new DateOnly(2026, 8, 1), "s1", "d1", "rx1", "n1", true);
        var rep2 = new SaveMedicalReportRequest(Guid.NewGuid(), patient.Id, Guid.NewGuid(), "Dr. B", "Dental", new DateOnly(2026, 9, 1), "s2", "d2", "rx2", "n2", true);
        await _client.PostAsJsonAsync("/medical-records", rep1);
        await _client.PostAsJsonAsync("/medical-records", rep2);

        var historyResp = await _client.GetAsync($"/patients/{patient.Id}/medical-history");
        Assert.Equal(HttpStatusCode.OK, historyResp.StatusCode);

        var history = await historyResp.Content.ReadFromJsonAsync<List<MedicalReportDto>>();
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task ReportEndpoints_WithoutToken_ReturnUnauthorized()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/medical-records/{Guid.NewGuid()}");
        req.Headers.Add("X-Test-NoAuth", "true");

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

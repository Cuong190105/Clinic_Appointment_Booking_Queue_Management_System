using System.Net;
using System.Net.Http.Json;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Tests.Fixtures;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Endpoints;

public class PatientApiIntegrationTests : IClassFixture<MedicalRecordsServiceFactory>
{
    private readonly HttpClient _client;

    public PatientApiIntegrationTests(MedicalRecordsServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePatient_ViaHttpApi_ReturnsCreated()
    {
        var req = new CreatePatientRequest(
            FullName: "Do Thi Mai",
            PhoneNumber: "0961234567",
            Email: "mai.do@example.com",
            DateOfBirth: new DateOnly(1996, 8, 20),
            Gender: "Female",
            Address: "12 Hang Bac, Hoan Kiem, Ha Noi",
            InsuranceNumber: "DN4799998888777",
            EmergencyContact: "0969999999"
        );

        var response = await _client.PostAsJsonAsync("/patients", req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("Do Thi Mai", body.FullName);
        Assert.Equal("/patients/" + body.Id, response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreatePatient_ViaLegacyAddRoute_ReturnsCreated()
    {
        var req = new CreatePatientRequest(
            FullName: "Vu Van Tung",
            PhoneNumber: "0971234567",
            Email: "tung.vu@example.com",
            DateOfBirth: new DateOnly(1988, 12, 1),
            Gender: "Male",
            Address: "56 Nguyen Du, Q1, HCMC",
            InsuranceNumber: null,
            EmergencyContact: null
        );

        var response = await _client.PostAsJsonAsync("/patients/add", req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(body);
        Assert.Equal("Vu Van Tung", body.FullName);
    }

    [Fact]
    public async Task GetPatientById_WhenFound_ReturnsOk()
    {
        var createReq = new CreatePatientRequest(
            FullName: "Bui Thi Lan",
            PhoneNumber: "0981112233",
            Email: "lan.bui@example.com",
            DateOfBirth: new DateOnly(1991, 3, 25),
            Gender: "Female",
            Address: "Hai Phong",
            InsuranceNumber: null,
            EmergencyContact: null
        );
        var createResp = await _client.PostAsJsonAsync("/patients", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        var getResp = await _client.GetAsync($"/patients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var fetched = await getResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Bui Thi Lan", fetched.FullName);
    }

    [Fact]
    public async Task GetPatientById_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/patients/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPagedPatients_ReturnsOk()
    {
        var response = await _client.GetAsync("/patients?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<PatientDto>>();
        Assert.NotNull(paged);
        Assert.True(paged.TotalCount >= 0);
    }

    [Fact]
    public async Task InternalUserProfileUpdated_ViaHttp_SynchronizesPatient()
    {
        var createReq = new CreatePatientRequest(
            FullName: "Initial Name Before Sync",
            PhoneNumber: "0901112222",
            Email: "init@example.com",
            DateOfBirth: new DateOnly(1994, 9, 9),
            Gender: "Male",
            Address: "Vung Tau",
            InsuranceNumber: null,
            EmergencyContact: null
        );
        var createResp = await _client.PostAsJsonAsync("/patients", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        var syncEvt = new UserProfileUpdated(
            PersonId: created.Id,
            FullName: "Updated Name After Sync",
            PhoneNumber: "0909999888",
            Email: "synced@example.com",
            Gender: "Male",
            DateOfBirth: new DateOnly(1994, 9, 9),
            Address: "TP Ho Chi Minh"
        );

        var syncResp = await _client.PostAsJsonAsync("/internal/events/user-profile-updated", syncEvt);
        Assert.Equal(HttpStatusCode.OK, syncResp.StatusCode);

        var getResp = await _client.GetAsync($"/patients/{created.Id}");
        var updated = await getResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Name After Sync", updated.FullName);
        Assert.Equal("0909999888", updated.PhoneNumber);
        Assert.Equal("synced@example.com", updated.Email);
        Assert.Equal("TP Ho Chi Minh", updated.Address);
    }

    [Fact]
    public async Task CreatePatient_WithoutAuth_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/patients")
        {
            Content = JsonContent.Create(new CreatePatientRequest("Anon User", null, null, new DateOnly(2000, 1, 1), "Other", "Addr"))
        };
        request.Headers.Add("X-Test-NoAuth", "true");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePatient_AsPatientRole_ReturnsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/patients")
        {
            Content = JsonContent.Create(new CreatePatientRequest("Denied User", null, null, new DateOnly(2000, 1, 1), "Other", "Addr"))
        };
        request.Headers.Add("X-Test-Role", "Patient");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientById_WhenOtherPatientAccesses_ReturnsForbidden()
    {
        var createReq = new CreatePatientRequest("Secret Patient", null, null, new DateOnly(1990, 1, 1), "Male", "Secret City");
        var createResp = await _client.PostAsJsonAsync("/patients", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/patients/{created.Id}");
        request.Headers.Add("X-Test-Role", "Patient");
        request.Headers.Add("X-Test-PatientId", Guid.NewGuid().ToString()); // different patient

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientById_WhenOwnPatientAccesses_ReturnsOk()
    {
        var createReq = new CreatePatientRequest("Own Patient", null, null, new DateOnly(1990, 1, 1), "Male", "Own City");
        var createResp = await _client.PostAsJsonAsync("/patients", createReq);
        var created = await createResp.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/patients/{created.Id}");
        request.Headers.Add("X-Test-Role", "Patient");
        request.Headers.Add("X-Test-PatientId", created.Id.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

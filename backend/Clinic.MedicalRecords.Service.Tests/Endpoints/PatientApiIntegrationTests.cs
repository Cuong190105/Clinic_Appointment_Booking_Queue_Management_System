using System.Net;
using System.Net.Http.Json;
using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Endpoints;

public class PatientApiIntegrationTests : IClassFixture<MedicalRecordsServiceFactory>
{
    private readonly HttpClient _client;
    private readonly MedicalRecordsServiceFactory _factory;

    public PatientApiIntegrationTests(MedicalRecordsServiceFactory factory)
    {
        _factory = factory;
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
    public async Task CreatePatient_ViaLegacyAddRoute_AcceptsNumericGenderAndOnlyRequiredFields()
    {
        var response = await _client.PostAsJsonAsync("/patients/add", new
        {
            fullName = "Le Thi Hoa",
            dateOfBirth = "1994-06-15",
            gender = 1,
            address = "Da Nang"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var patient = await response.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);
        Assert.Equal("Female", patient.Gender);
        Assert.Null(patient.PhoneNumber);
        Assert.Null(patient.Email);
        Assert.Null(patient.InsuranceNumber);
        Assert.Null(patient.EmergencyContact);
    }

    [Fact]
    public async Task CreatePatient_ViaLegacyAddRoute_RejectsMissingMinimumFields()
    {
        var response = await _client.PostAsJsonAsync("/patients/add", new
        {
            fullName = "Le Thi Hoa",
            gender = 1,
            address = "Da Nang"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOwnPatient_AcceptsNumericGenderAndQueuesProjection()
    {
        var createResponse = await _client.PostAsJsonAsync("/patients", new CreatePatientRequest(
            "Projection Update Patient", null, null, new DateOnly(1990, 1, 1), "Male", "Initial address"));
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(created);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/patients/me")
        {
            Content = JsonContent.Create(new
            {
                email = "updated@example.test",
                gender = 1,
                address = "Updated address"
            })
        };
        request.Headers.Add("X-Test-Role", "Patient");
        request.Headers.Add("X-Test-PatientId", created.Id.ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        var updatedPatient = await db.Patients.FindAsync(created.Id);
        Assert.NotNull(updatedPatient);
        Assert.Equal("Female", updatedPatient.Gender);
        Assert.Contains(await db.PatientProjectionOutbox.ToListAsync(), message => message.PatientId == created.Id && message.ProcessedAt == null);
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
        var identityUserId = Guid.NewGuid();
        var createReq = new CreatePatientRequest(
            FullName: "Initial Name Before Sync",
            PhoneNumber: "0901112222",
            Email: "init@example.com",
            DateOfBirth: new DateOnly(1994, 9, 9),
            Gender: "Male",
            Address: "Vung Tau",
            InsuranceNumber: null,
            EmergencyContact: null,
            IdentityUserId: identityUserId
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
            Address: "TP Ho Chi Minh",
            IdentityUserId: identityUserId
        );

        using var syncRequest = new HttpRequestMessage(HttpMethod.Post, "/internal/events/user-profile-updated")
        {
            Content = JsonContent.Create(syncEvt)
        };
        syncRequest.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var syncResp = await _client.SendAsync(syncRequest);
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
    public async Task IdentityPatientProvisioning_UsesExplicitIdsAndRequiresServiceToken()
    {
        var phone = $"09{Random.Shared.Next(100000000, 999999999)}";
        var unauthorized = await _client.GetAsync($"/internal/patients/resolve-registration?phoneNumber={phone}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var resolve = new HttpRequestMessage(HttpMethod.Get,
            $"/internal/patients/resolve-registration?phoneNumber={phone}");
        resolve.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var resolvedResponse = await _client.SendAsync(resolve);
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<RegistrationIds>();
        Assert.NotNull(resolved);

        var identityUserId = Guid.NewGuid();
        using var provision = new HttpRequestMessage(HttpMethod.Post, "/internal/patients/provision-identity")
        {
            Content = JsonContent.Create(new
            {
                patientId = resolved.PatientId,
                personId = resolved.PersonId,
                identityUserId,
                fullName = "Provisioned Patient",
                phoneNumber = phone,
                email = "provisioned@example.test",
                dateOfBirth = new DateOnly(1990, 1, 1),
                gender = "Other",
                address = "Test address"
            })
        };
        provision.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var provisionResponse = await _client.SendAsync(provision);
        Assert.Equal(HttpStatusCode.OK, provisionResponse.StatusCode);

        var patient = await provisionResponse.Content.ReadFromJsonAsync<PatientDto>();
        Assert.NotNull(patient);
        Assert.Equal(resolved.PatientId, patient.Id);
        Assert.Equal(identityUserId, patient.IdentityUserId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        Assert.True(await db.PatientProjectionOutbox.AnyAsync(m => m.PatientId == resolved.PatientId && m.ProcessedAt == null));
    }

    [Fact]
    public async Task ResolveRegistration_WithClinicPersonId_PreservesIdAndExistingPatientLink()
    {
        var phone = $"09{Random.Shared.Next(100000000, 999999999)}";
        var personId = Guid.NewGuid();
        using var resolve = new HttpRequestMessage(HttpMethod.Get,
            $"/internal/patients/resolve-registration?phoneNumber={phone}&personId={personId}");
        resolve.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");

        var resolvedResponse = await _client.SendAsync(resolve);
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<RegistrationIds>();
        Assert.NotNull(resolved);
        Assert.Equal(personId, resolved.PersonId);

        using var provision = new HttpRequestMessage(HttpMethod.Post, "/internal/patients/provision-identity")
        {
            Content = JsonContent.Create(new
            {
                patientId = resolved.PatientId,
                personId,
                identityUserId = Guid.NewGuid(),
                fullName = "Linked Staff Patient",
                phoneNumber = phone,
                email = "linked-staff@example.test",
                dateOfBirth = new DateOnly(1990, 1, 1),
                gender = "Other",
                address = "Test address"
            })
        };
        provision.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(provision)).StatusCode);

        using var retry = new HttpRequestMessage(HttpMethod.Get,
            $"/internal/patients/resolve-registration?phoneNumber={phone}&personId={personId}");
        retry.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var retryResponse = await _client.SendAsync(retry);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retried = await retryResponse.Content.ReadFromJsonAsync<RegistrationIds>();
        Assert.NotNull(retried);
        Assert.Equal(resolved.PatientId, retried.PatientId);
        Assert.Equal(personId, retried.PersonId);
    }

    private sealed record RegistrationIds(Guid PersonId, Guid PatientId);

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

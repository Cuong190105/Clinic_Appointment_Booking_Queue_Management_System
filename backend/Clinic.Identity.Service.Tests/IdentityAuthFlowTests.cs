using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Identity.Service.Models;

namespace Clinic.Identity.Service.Tests;

public sealed class IdentityAuthFlowTests : IClassFixture<IdentityServiceFactory>
{
    private readonly HttpClient _client;

    public IdentityAuthFlowTests(IdentityServiceFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Registration_login_and_single_use_password_reset_are_owned_by_identity()
    {
        var phone = $"09{Random.Shared.Next(100000000, 999999999)}";
        var username = $"identity-test-{Guid.NewGuid():N}";
        var register = new
        {
            username,
            password = "InitialPassword123!",
            fullName = "Identity Integration Test",
            phoneNumber = phone,
            email = (string?)null,
            dateOfBirth = new DateOnly(1990, 1, 1),
            gender = "Other",
            address = (string?)null,
            personId = Guid.NewGuid(),
            patientId = Guid.NewGuid()
        };

        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/register")
        {
            Content = JsonContent.Create(register)
        };
        registerRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var registration = await _client.SendAsync(registerRequest);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var ids = await registration.Content.ReadFromJsonAsync<JsonElement>();

        using var login = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password = register.password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        var oldRefreshToken = session.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(oldRefreshToken));
        Assert.Contains("Patient", session.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));

        using var forgotRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/forgot-password")
        {
            Content = JsonContent.Create(new { phoneNumberOrEmail = phone })
        };
        forgotRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var forgot = await _client.SendAsync(forgotRequest);
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var reset = await forgot.Content.ReadFromJsonAsync<JsonElement>();
        var resetToken = reset.GetProperty("resetToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(resetToken));

        using var resetResponse = await _client.PostAsJsonAsync("/v1/auth/reset-password", new
        {
            token = resetToken,
            newPassword = "UpdatedPassword456!"
        });
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        using var replay = await _client.PostAsJsonAsync("/v1/auth/reset-password", new
        {
            token = resetToken,
            newPassword = "AttackerPassword789!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        using var oldPasswordLogin = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password = register.password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        using var replayPasswordLogin = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password = "AttackerPassword789!" });
        Assert.Equal(HttpStatusCode.Unauthorized, replayPasswordLogin.StatusCode);
        using var newPasswordLogin = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password = "UpdatedPassword456!" });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

        using var oldRefresh = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = oldRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        Assert.Equal(register.personId.ToString(), ids.GetProperty("personId").GetString());
    }

    [Fact]
    public async Task Registration_requires_the_internal_service_token()
    {
        using var response = await _client.PostAsJsonAsync("/v1/auth/register", new
        {
            username = $"unauthorized-{Guid.NewGuid():N}", password = "LongPassword123!", fullName = "No Token",
            phoneNumber = $"09{Random.Shared.Next(100000000, 999999999)}", dateOfBirth = new DateOnly(1990, 1, 1),
            gender = "Other", personId = Guid.NewGuid(), patientId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_account_stays_inactive_until_clinic_profile_is_linked_and_admin_reads_use_identity()
    {
        var userId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var username = $"staff-test-{Guid.NewGuid():N}";
        var password = "StaffPassword123!";
        using var provisionRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/internal/users/provision")
        {
            Content = JsonContent.Create(new
            {
                userId, personId, username, password, fullName = "Staff Identity Test",
                phoneNumber = $"08{Random.Shared.Next(100000000, 999999999)}", email = "staff@example.test",
                dateOfBirth = new DateOnly(1990, 1, 1), gender = "Other", address = "Clinic address",
                roles = new[] { "Receptionist", "Patient" }
            })
        };
        provisionRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var provision = await _client.SendAsync(provisionRequest);
        Assert.Equal(HttpStatusCode.OK, provision.StatusCode);
        var provisionResult = await provision.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(provisionResult.GetProperty("isActive").GetBoolean());

        using var inactiveLogin = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);

        using var linkRequest = new HttpRequestMessage(HttpMethod.Put, $"/v1/internal/users/{userId}/roles")
        {
            Content = JsonContent.Create(new { roles = new[] { "Receptionist", "Patient" }, employeeId, doctorId = (Guid?)null })
        };
        linkRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var linked = await _client.SendAsync(linkRequest);
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);

        using var login = await _client.PostAsJsonAsync("/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Receptionist", session.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Patient", session.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));

        using var adminListRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/internal/users?search=staff-test&role=Receptionist&pageNumber=1&pageSize=100");
        adminListRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var adminList = await _client.SendAsync(adminListRequest);
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);
        var page = await adminList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("userId").GetGuid() == userId);

        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/internal/users/{userId}");
        detailsRequest.Headers.Add("X-Identity-Service-Token", IdentityServiceFactory.ServiceToken);
        using var details = await _client.SendAsync(detailsRequest);
        Assert.Equal(employeeId, (await details.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task Internal_account_endpoints_require_the_service_token()
    {
        using var list = await _client.GetAsync("/v1/internal/users");
        using var detail = await _client.GetAsync($"/v1/internal/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }
}

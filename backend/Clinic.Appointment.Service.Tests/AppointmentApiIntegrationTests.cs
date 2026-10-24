using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Contracts.Events;
using Xunit;

namespace Clinic.Appointment.Service.Tests;

public class AppointmentApiIntegrationTests : IClassFixture<AppointmentServiceFactory>
{
    private readonly HttpClient _client;
    private readonly AppointmentServiceFactory _factory;

    public AppointmentApiIntegrationTests(AppointmentServiceFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("Clinic.Appointment.Service", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Readyz_ReturnsReady()
    {
        var response = await _client.GetAsync("/readyz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Ready", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Root_ReturnsServiceInfo()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal("Clinic.Appointment.Service", json.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized()
    {
        var appointmentsResponse = await _client.GetAsync("/appointments");
        Assert.Equal(HttpStatusCode.Unauthorized, appointmentsResponse.StatusCode);

        var myAppointmentsResponse = await _client.GetAsync("/me/appointments");
        Assert.Equal(HttpStatusCode.Unauthorized, myAppointmentsResponse.StatusCode);

        var meResponse = await _client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task InternalEvents_UserProfileUpdated_ViaHttpApi_Succeeds()
    {
        var evt = new UserProfileUpdated(
            Guid.NewGuid(),
            "Nguyen Van Test",
            "0901112233",
            "test@integration.local",
            "Male");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/events/user-profile-updated")
        {
            Content = JsonContent.Create(evt)
        };
        request.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.GetProperty("handled").GetBoolean());
    }

    [Fact]
    public async Task InternalEvents_WithoutServiceToken_ReturnUnauthorized()
    {
        var evt = new UserProfileUpdated(Guid.NewGuid(), "Test", null, null, "Other");
        var response = await _client.PostAsJsonAsync("/internal/events/user-profile-updated", evt);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}


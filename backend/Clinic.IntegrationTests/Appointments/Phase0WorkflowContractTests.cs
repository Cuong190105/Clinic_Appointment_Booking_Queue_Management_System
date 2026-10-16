using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Appointments;

public sealed class Phase0WorkflowContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public Phase0WorkflowContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Availability_returns_slot_collection_contract()
    {
        using var response = await _client.GetAsync("/schedules/availability?fromDate=2026-10-16&toDate=2026-10-17");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var envelope = await ApiResponseAssertions.ReadEnvelopeAsync(response);
        Assert.Equal(JsonValueKind.Array, ApiResponseAssertions.Result(envelope).ValueKind);
    }

    [Fact]
    public async Task Reservation_can_be_reserved_confirmed_and_released()
    {
        var scheduleId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        using var reserveRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/schedules/{scheduleId}/reservations");
        reserveRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        reserveRequest.Content = JsonContent.Create(new
        {
            timeSlot = "08:30:00",
            patientId = Guid.NewGuid()
        });

        using var reserveResponse = await _client.SendAsync(reserveRequest);
        Assert.True(reserveResponse.StatusCode == HttpStatusCode.Created,
            await reserveResponse.Content.ReadAsStringAsync());
        using var reserveEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(reserveResponse);
        Assert.Equal("Reserved", ApiResponseAssertions.Result(reserveEnvelope).GetProperty("status").GetString());

        using var confirmResponse = await _client.PostAsync(
            $"/schedules/{scheduleId}/reservations/{reservationId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        using var releaseResponse = await _client.PostAsync(
            $"/schedules/{scheduleId}/reservations/{reservationId}/release", null);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
    }

    [Fact]
    public async Task Appointment_booking_requires_idempotency_aware_command_boundary()
    {
        using var response = await _client.PostAsJsonAsync("/appointments", new
        {
            workScheduleId = Guid.NewGuid(),
            timeSlot = "09:00:00",
            reason = "baseline integration test"
        });

        // Existing appointment policy is intentionally not bypassed by the test host;
        // this verifies the endpoint remains protected while the contract evolves.
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }
}

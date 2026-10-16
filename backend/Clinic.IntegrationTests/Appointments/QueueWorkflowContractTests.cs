using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Appointments;

public sealed class QueueWorkflowContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public QueueWorkflowContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Check_in_and_exam_completion_return_queue_contracts()
    {
        var appointmentId = Guid.NewGuid();
        using var checkInRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/queue/tickets/check-in?appointmentId={appointmentId}");
        checkInRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var checkInResponse = await _client.SendAsync(checkInRequest);

        Assert.Equal(HttpStatusCode.Created, checkInResponse.StatusCode);
        using var checkInEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(checkInResponse);
        Assert.Equal("Waiting", ApiResponseAssertions.Result(checkInEnvelope).GetProperty("status").GetString());

        var ticketId = Guid.NewGuid();
        using var completeResponse = await _client.PostAsync(
            $"/queue/tickets/{ticketId}/complete-exam", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        using var completeEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(completeResponse);
        Assert.Equal("Completed", ApiResponseAssertions.Result(completeEnvelope).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Legacy_call_next_and_complete_exam_endpoints_remain_protected()
    {
        using var callNext = await _client.PostAsync($"/queue/call-next/{Guid.NewGuid()}", null);
        using var complete = await _client.PostAsync($"/queue/{Guid.NewGuid()}/complete-exam", null);

        Assert.Equal(HttpStatusCode.Forbidden, callNext.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, complete.StatusCode);
    }
}

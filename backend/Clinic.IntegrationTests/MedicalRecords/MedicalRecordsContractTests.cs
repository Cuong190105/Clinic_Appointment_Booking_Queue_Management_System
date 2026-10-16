using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.MedicalRecords;

public sealed class MedicalRecordsContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public MedicalRecordsContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Medical_record_lifecycle_supports_draft_finalize_and_amendment()
    {
        var visitId = Guid.NewGuid();
        using var draftResponse = await _client.PostAsJsonAsync(
            $"/medical-records/encounters/{visitId}/draft", new
            {
                encounterId = visitId,
                symptoms = "headache",
                diagnosis = "draft diagnosis",
                prescription = "rest",
                notes = "baseline"
            });

        Assert.True(draftResponse.StatusCode == HttpStatusCode.Created,
            await draftResponse.Content.ReadAsStringAsync());
        using var draftEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(draftResponse);
        Assert.Equal("Draft", ApiResponseAssertions.Result(draftEnvelope).GetProperty("status").GetString());

        using var getResponse = await _client.GetAsync($"/medical-records/encounters/{visitId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var reportId = Guid.NewGuid();
        using var finalizeRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/medical-records/reports/{reportId}/finalize");
        finalizeRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var finalizeResponse = await _client.SendAsync(finalizeRequest);
        Assert.Equal(HttpStatusCode.OK, finalizeResponse.StatusCode);
        using var finalizeEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(finalizeResponse);
        Assert.Equal("Finalized", ApiResponseAssertions.Result(finalizeEnvelope).GetProperty("status").GetString());

        using var amendmentResponse = await _client.PostAsJsonAsync(
            $"/medical-records/reports/{reportId}/amendments", new
            {
                reason = "corrected diagnosis",
                diagnosis = "amended diagnosis",
                prescription = (string?)null,
                notes = "audit trail required"
            });
        Assert.Equal(HttpStatusCode.Created, amendmentResponse.StatusCode);
    }
}

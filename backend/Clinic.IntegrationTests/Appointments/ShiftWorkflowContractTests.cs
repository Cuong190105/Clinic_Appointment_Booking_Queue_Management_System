using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Appointments;

public sealed class ShiftWorkflowContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public ShiftWorkflowContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Shift_approval_endpoint_requires_shift_management_permission()
    {
        using var response = await _client.PostAsync(
            $"/shifts/suggestions/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

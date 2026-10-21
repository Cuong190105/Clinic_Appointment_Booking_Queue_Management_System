using System.Net;
using System.Text.Json;
using Clinic.IntegrationTests.Fixtures;

namespace Clinic.IntegrationTests.Auth;

public sealed class AdminIdentityContractTests(ApiContractFactory factory) : IClassFixture<ApiContractFactory>
{
    [Fact]
    public async Task Admin_account_list_and_detail_are_served_from_identity_contract()
    {
        using var client = factory.CreateAuthenticatedClient();
        using var listResponse = await client.GetAsync("/admin/users?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(listResponse);
        var page = ApiResponseAssertions.Result(listEnvelope);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal("identity-admin-user", page.GetProperty("items")[0].GetProperty("username").GetString());

        using var detailResponse = await client.GetAsync("/admin/users/b0000001-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(detailResponse);
        JsonElement detail = ApiResponseAssertions.Result(detailEnvelope);
        Assert.Equal("Identity Admin User", detail.GetProperty("fullName").GetString());
        Assert.Equal("Patient", detail.GetProperty("roles")[0].GetString());

        using var rolesResponse = await client.GetAsync("/admin/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        using var rolesEnvelope = await ApiResponseAssertions.ReadEnvelopeAsync(rolesResponse);
        Assert.Equal("Patient", ApiResponseAssertions.Result(rolesEnvelope)[0].GetProperty("name").GetString());
    }
}

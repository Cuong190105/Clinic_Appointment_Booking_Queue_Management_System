using System.Net;
using System.Net.Http.Json;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Clinic.IntegrationTests.Auth;

public sealed class StaffProvisioningContractTests(ApiContractFactory factory) : IClassFixture<ApiContractFactory>
{
    [Fact]
    public async Task New_employee_creation_uses_identity_user_id_and_creates_only_a_clinic_projection()
    {
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        var phone = $"07{Random.Shared.Next(100000000, 999999999)}";
        var username = $"employee-test-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync("/employees", new
        {
            username,
            password = "EmployeePassword123!",
            fullName = "Identity Employee Test",
            phoneNumber = phone,
            email = $"{username}@example.test",
            dateOfBirth = new DateOnly(1990, 1, 1),
            gender = "Other",
            address = "Test Clinic",
            hireDate = new DateOnly(2026, 1, 1),
            roles = new[] { "Receptionist" }
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var envelope = await ApiResponseAssertions.ReadEnvelopeAsync(response);
        var employeeId = ApiResponseAssertions.Result(envelope).GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, employeeId);

        await using var db = factory.CreateDbContext();
        var person = await db.Persons.SingleAsync(x => x.PhoneNumber == phone);
        Assert.NotEqual(Guid.Empty, person.IdentityUserId);
    }
}

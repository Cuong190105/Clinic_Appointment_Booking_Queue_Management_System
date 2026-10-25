using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinic.Domain.Enums;

namespace Clinic.API.Services;

public sealed class IdentityAccountClient(IHttpClientFactory factory, IConfiguration configuration)
{
    private readonly HttpClient _client = factory.CreateClient("Identity");
    private readonly string? _serviceToken = configuration["Identity:ServiceToken"];

    public async Task<IdentityProvisionResult?> ProvisionAsync(
        Guid userId, Guid personId, string username, string? password, string fullName,
        string? phoneNumber, string? email, DateOnly dateOfBirth, Gender gender, string? address,
        IReadOnlyList<string> roles, Guid? patientId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "v1/internal/users/provision");
        request.Content = JsonContent.Create(new
        {
            userId, personId, username, password, fullName, phoneNumber, email,
            dateOfBirth, gender = gender.ToString(), address, roles, patientId
        });
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<IdentityProvisionResult>(cancellationToken);
    }

    public async Task<bool> UpdateRolesAsync(
        Guid userId, IReadOnlyList<string> roles, Guid? patientId, Guid? employeeId, Guid? doctorId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, $"v1/internal/users/{userId}/roles");
        request.Content = JsonContent.Create(new { roles, patientId, employeeId, doctorId });
        using var response = await _client.SendAsync(request, cancellationToken);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<(IReadOnlyList<string> Roles, Guid? PatientId, Guid? EmployeeId, Guid? DoctorId)?> GetRolesAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"v1/internal/users/{userId}");
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var roles = root.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        var patientId = root.TryGetProperty("patientId", out var patient) && patient.ValueKind == JsonValueKind.String
            ? patient.GetGuid() : (Guid?)null;
        var employeeId = root.TryGetProperty("employeeId", out var employee) && employee.ValueKind == JsonValueKind.String
            ? employee.GetGuid() : (Guid?)null;
        var doctorId = root.TryGetProperty("doctorId", out var doctor) && doctor.ValueKind == JsonValueKind.String
            ? doctor.GetGuid() : (Guid?)null;
        return (roles, patientId, employeeId, doctorId);
    }

    public async Task<IdentityUserProfile?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"v1/internal/users/{userId}");
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<IdentityUserProfile>(cancellationToken);
    }

    public async Task<bool> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber, string? email,
        DateOnly dateOfBirth, Gender gender, string? address, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, $"v1/internal/users/{userId}/profile");
        request.Content = JsonContent.Create(new { fullName, phoneNumber, email, dateOfBirth, gender = gender.ToString(), address });
        using var response = await _client.SendAsync(request, cancellationToken);
        return response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<IReadOnlyList<Clinic.API.Models.IdentityRoleDetails>?> GetRoleCatalogAsync(
        IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        var query = names is { Count: > 0 }
            ? "?name=" + Uri.EscapeDataString(string.Join(',', names))
            : string.Empty;
        using var request = CreateRequest(HttpMethod.Get, "v1/internal/roles" + query);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<Clinic.API.Models.IdentityRoleDetails>>(cancellationToken);
    }

    public async Task<bool> RolesExistAsync(IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        var roles = await GetRoleCatalogAsync(names, cancellationToken);
        return roles is not null && names.All(name => roles.Any(role =>
            string.Equals(role.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_serviceToken))
            throw new InvalidOperationException("Identity service token is not configured.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Identity-Service-Token", _serviceToken);
        return request;
    }
}

public sealed record IdentityProvisionResult(Guid UserId, Guid PersonId, bool IsActive, bool Created);
public sealed record IdentityUserProfile(Guid UserId, Guid PersonId, string Username, string FullName,
    string? PhoneNumber, string? Email, string Gender, string? Address, DateOnly DateOfBirth,
    IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt, Guid? PatientId, Guid? EmployeeId, Guid? DoctorId);

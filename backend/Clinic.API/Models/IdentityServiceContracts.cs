namespace Clinic.API.Models;

public sealed record AuthLoginResponse(string AccessToken, IReadOnlyList<string> Roles);

public sealed record AuthRefreshResponse(string AccessToken);

internal sealed record IdentityRegisterRequest(
    string Username,
    string Password,
    string FullName,
    string? PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Gender,
    string? Address,
    Guid PersonId,
    Guid PatientId);

internal sealed record IdentityRegisterResponse(Guid UserId, Guid PersonId, Guid? PatientId);

internal sealed record IdentityForgotPasswordRequest(string PhoneNumberOrEmail);

internal sealed record IdentityPasswordResetDelivery(bool Found, Guid? PersonId, string? ResetToken);

internal sealed record IdentityResetPasswordRequest(string Token, string NewPassword);

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

internal sealed record IdentityUserProfileResponse(
    Guid UserId,
    Guid PersonId,
    string Username,
    string FullName,
    string? PhoneNumber,
    string? Email,
    string Gender,
    string? Address,
    DateOnly DateOfBirth,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime CreatedAt);

internal sealed record IdentityAdminUserSummary(
    Guid UserId, Guid PersonId, string Username, string FullName, string? PhoneNumber,
    string? Email, string Gender, IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt,
    Guid? EmployeeId, Guid? DoctorId);

internal sealed record IdentityAdminUserDetail(
    Guid UserId, Guid PersonId, string Username, string FullName, string? PhoneNumber,
    string? Email, string Gender, string? Address, DateOnly DateOfBirth,
    IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt,
    Guid? PatientId, Guid? EmployeeId, Guid? DoctorId);

internal sealed record IdentityPagedAdminUsers(
    IReadOnlyList<IdentityAdminUserSummary> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record IdentityRoleDetails(Guid Id, string Name, string? Description, IReadOnlyList<string> Permissions);

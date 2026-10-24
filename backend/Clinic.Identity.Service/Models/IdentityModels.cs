namespace Clinic.Identity.Service.Models;

public sealed class PersonProfile
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Gender { get; set; } = "Unspecified";
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class IdentityUser
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    // Stable cross-service resource IDs carried as claims; these are identifiers only, not foreign keys.
    public Guid? PatientId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? DoctorId { get; set; }
    public PersonProfile Person { get; set; } = null!;
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<IdentityUserRole> UserRoles { get; set; } = [];
    public List<IdentityRefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class IdentityRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<IdentityUserRole> UserRoles { get; set; } = [];
    public List<IdentityRolePermission> RolePermissions { get; set; } = [];
}

public sealed class IdentityPermission
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<IdentityRolePermission> RolePermissions { get; set; } = [];
}

public sealed class IdentityUserRole
{
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public IdentityRole Role { get; set; } = null!;
}

public sealed class IdentityRolePermission
{
    public Guid RoleId { get; set; }
    public IdentityRole Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public IdentityPermission Permission { get; set; } = null!;
}

public sealed class IdentityRefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class IdentityPasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public sealed record RegisterRequest(
    string Username, string Password, string FullName, string? PhoneNumber,
    string? Email, DateOnly DateOfBirth, string Gender, string? Address,
    Guid PersonId, Guid PatientId);
public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ForgotPasswordRequest(string PhoneNumberOrEmail);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record PasswordResetDelivery(bool Found, Guid? PersonId = null, string? ResetToken = null);
public sealed record UserProfileResponse(
    Guid UserId, Guid PersonId, string Username, string FullName, string? PhoneNumber, string? Email,
    string Gender, string? Address, DateOnly DateOfBirth, IReadOnlyList<string> Roles,
    bool IsActive, DateTime CreatedAt);
public sealed record TokenResponse(string AccessToken, string RefreshToken, IReadOnlyList<string> Roles);
public sealed record JwksResponse(IReadOnlyList<JwkResponse> Keys);
public sealed record JwkResponse(string Kid, string Kty, string Use, string Alg, string N, string E);

public sealed record ProvisionAccountRequest(
    Guid UserId, Guid PersonId, string Username, string? Password, string FullName,
    string? PhoneNumber, string? Email, DateOnly DateOfBirth, string Gender, string? Address,
    IReadOnlyList<string> Roles, Guid? PatientId = null, Guid? EmployeeId = null, Guid? DoctorId = null);

public sealed record UpdateAccountRolesRequest(
    IReadOnlyList<string> Roles, Guid? PatientId = null, Guid? EmployeeId = null, Guid? DoctorId = null);

public sealed record UpdateIdentityProfileRequest(
    string FullName, string? PhoneNumber, string? Email, DateOnly DateOfBirth, string Gender, string? Address);

public sealed record AdminUserSummary(
    Guid UserId, Guid PersonId, string Username, string FullName, string? PhoneNumber,
    string? Email, string Gender, IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt,
    Guid? EmployeeId, Guid? DoctorId);

public sealed record AdminUserDetail(
    Guid UserId, Guid PersonId, string Username, string FullName, string? PhoneNumber,
    string? Email, string Gender, string? Address, DateOnly DateOfBirth,
    IReadOnlyList<string> Roles, bool IsActive, DateTime CreatedAt,
    Guid? PatientId, Guid? EmployeeId, Guid? DoctorId);

public sealed record PagedAdminUsers(
    IReadOnlyList<AdminUserSummary> Items, int TotalCount, int PageNumber, int PageSize);

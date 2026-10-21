using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Clinic.Identity.Service.Auth;
using Clinic.Identity.Service.Models;
using Clinic.Identity.Service.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var issuer = builder.Configuration["Jwt:Issuer"] ?? "Clinic.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "Clinic.Services";
var publicBaseUrl = (builder.Configuration["Jwt:PublicBaseUrl"] ?? "http://localhost:5001").TrimEnd('/');
var accessMinutes = builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 15);
var refreshDays = builder.Configuration.GetValue("Jwt:RefreshTokenDays", 7);
var serviceToken = builder.Configuration["Identity:ServiceToken"];
if (accessMinutes is < 1 or > 60 || refreshDays is < 1 or > 90)
    throw new InvalidOperationException("JWT access/refresh lifetimes are outside supported bounds.");

var connectionString = builder.Configuration.GetConnectionString("IdentityConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityConnection is required.");
builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
var signingKeyRing = new SigningKeyRing(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(signingKeyRing);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        ValidateIssuerSigningKey = true,
        IssuerSigningKeyResolver = (_, _, _, _) => signingKeyRing.ValidationKeys,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = JwtRegisteredClaimNames.Sub
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("identity-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var app = builder.Build();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/readyz", async (IdentityDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "Ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapGet("/.well-known/jwks.json", (SigningKeyRing ring) => Results.Ok(new JwksResponse(ring.PublicKeys())))
    .AllowAnonymous();
app.MapGet("/.well-known/openid-configuration", () => Results.Ok(new
{
    issuer,
    jwks_uri = $"{publicBaseUrl}/.well-known/jwks.json",
    id_token_signing_alg_values_supported = new[] { "RS256" },
    subject_types_supported = new[] { "public" },
    token_endpoint = $"{publicBaseUrl}/v1/auth/login"
})).AllowAnonymous();

var auth = app.MapGroup("/v1/auth");
auth.MapPost("/register", async (RegisterRequest request, HttpRequest httpRequest, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(httpRequest, serviceToken)) return Results.Unauthorized();
    var username = request.Username?.Trim();
    var phoneNumber = request.PhoneNumber?.Trim();
    if (string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
        string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 ||
        string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200 ||
        string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > 20 ||
        (request.Email?.Length ?? 0) > 200 || (request.Address?.Length ?? 0) > 500 ||
        !System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^\+?[0-9]+$") ||
        request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) ||
        !new[] { "Male", "Female", "Other" }.Contains(request.Gender, StringComparer.OrdinalIgnoreCase) ||
        request.PersonId == Guid.Empty || request.PatientId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Provide a valid username, password (8+ characters), name, phone number, date of birth, and gender."] });

    var existingUsername = await db.Users.Include(x => x.Person)
        .SingleOrDefaultAsync(x => x.Username == username, ct);
    if (existingUsername is not null)
    {
        // A repeated gateway request can finish local Patient provisioning after a transient failure.
        if (existingUsername.Person.PhoneNumber == phoneNumber && existingUsername.IsActive &&
            BCrypt.Net.BCrypt.Verify(request.Password, existingUsername.PasswordHash))
            return Results.Created($"/auth/users/{existingUsername.Id}", new
            {
                userId = existingUsername.Id, personId = existingUsername.PersonId,
                patientId = existingUsername.PatientId
            });
        return Results.Conflict(new { code = "username_exists", message = "Username is already registered." });
    }

    var personByPhone = await db.Persons.SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber, ct);
    if (personByPhone is not null && personByPhone.Id != request.PersonId)
        return Results.Conflict(new { code = "phone_exists", message = "Phone number is already registered." });
    if (personByPhone is not null && await db.Users.AnyAsync(x => x.PersonId == personByPhone.Id, ct))
        return Results.Conflict(new { code = "phone_exists", message = "Phone number is already registered." });

    var person = await db.Persons.SingleOrDefaultAsync(x => x.Id == request.PersonId, ct);
    if (person is not null && person.PhoneNumber != phoneNumber)
        return Results.Conflict(new { code = "person_mismatch", message = "The patient profile does not match the registration phone number." });

    var patientRole = await db.Roles.SingleOrDefaultAsync(x => x.Name == "Patient", ct);
    if (patientRole is null) return Results.Problem("Identity roles have not been initialized.", statusCode: 503);
    if (person is null)
    {
        person = new PersonProfile { Id = request.PersonId };
        db.Persons.Add(person);
    }
    person.FullName = request.FullName.Trim();
    person.PhoneNumber = phoneNumber;
    person.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
    person.DateOfBirth = request.DateOfBirth;
    person.Gender = new[] { "Male", "Female", "Other" }.First(value => string.Equals(value, request.Gender, StringComparison.OrdinalIgnoreCase));
    person.Address = request.Address;

    var user = new IdentityUser
    {
        Id = Guid.NewGuid(), PersonId = person.Id, Person = person, PatientId = request.PatientId,
        Username = username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
    };
    db.Users.Add(user);
    db.UserRoles.Add(new IdentityUserRole { UserId = user.Id, User = user, RoleId = patientRole.Id, Role = patientRole });
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException) { return Results.Conflict(new { code = "identity_conflict", message = "Username or phone number is already registered." }); }
    return Results.Created($"/auth/users/{user.Id}", new { userId = user.Id, personId = person.Id, patientId = user.PatientId });
}).AllowAnonymous().RequireRateLimiting("identity-auth");

auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, HttpRequest httpRequest, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(httpRequest, serviceToken)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.PhoneNumberOrEmail))
        return Results.Ok(new PasswordResetDelivery(false));

    var identifier = request.PhoneNumberOrEmail.Trim();
    var user = await db.Users.Include(x => x.Person)
        .SingleOrDefaultAsync(x => x.Person.PhoneNumber == identifier || x.Person.Email == identifier, ct);
    if (user is null || !user.IsActive) return Results.Ok(new PasswordResetDelivery(false));

    var now = DateTime.UtcNow;
    await db.PasswordResetTokens.Where(x => x.UserId == user.Id && x.UsedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.UsedAt, now), ct);
    var resetToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    db.PasswordResetTokens.Add(new IdentityPasswordResetToken
    {
        Id = Guid.NewGuid(), UserId = user.Id, User = user,
        TokenHash = Hash(resetToken), CreatedAt = now, ExpiresAt = now.AddMinutes(30)
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new PasswordResetDelivery(true, user.PersonId, resetToken));
}).AllowAnonymous().RequireRateLimiting("identity-auth");

auth.MapPost("/reset-password", async (ResetPasswordRequest request, IdentityDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["New password must contain at least 8 characters."] });

    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    var now = DateTime.UtcNow;
    var tokenHash = Hash(request.Token);
    var token = await db.PasswordResetTokens.Include(x => x.User)
        .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, ct);
    if (token is null || !token.User.IsActive)
    {
        await transaction.RollbackAsync(ct);
        return Results.Problem("The password reset code is invalid or expired.", statusCode: StatusCodes.Status400BadRequest);
    }

    token.UsedAt = now;
    token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    await db.RefreshTokens.Where(x => x.UserId == token.UserId && x.RevokedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.RevokedAt, now), ct);
    await db.PasswordResetTokens.Where(x => x.UserId == token.UserId && x.Id != token.Id && x.UsedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.UsedAt, now), ct);
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return Results.NoContent();
}).AllowAnonymous().RequireRateLimiting("identity-auth");

auth.MapPost("/login", async (LoginRequest request, IdentityDbContext db, SigningKeyRing ring, CancellationToken ct) =>
{
    var user = await db.Users.Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.Username == request.Username, ct);
    if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();
    var tokens = await IssueTokens(user, db, ring, issuer, audience, accessMinutes, refreshDays, ct);
    return Results.Ok(tokens);
}).AllowAnonymous().RequireRateLimiting("identity-auth");

auth.MapPost("/refresh", async (RefreshRequest request, IdentityDbContext db, SigningKeyRing ring, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Results.Unauthorized();
    var hash = Hash(request.RefreshToken);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    var now = DateTime.UtcNow;
    var updated = await db.RefreshTokens
        .Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now && x.User.IsActive)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.RevokedAt, now), ct);
    if (updated != 1) return Results.Unauthorized();

    var token = await db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == hash)
        .Select(x => x.UserId).SingleAsync(ct);
    var user = await db.Users.Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
        .SingleAsync(x => x.Id == token, ct);
    var response = await IssueTokens(user, db, ring, issuer, audience, accessMinutes, refreshDays, ct);
    await transaction.CommitAsync(ct);
    return Results.Ok(response);
}).AllowAnonymous().RequireRateLimiting("identity-auth");

auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, IdentityDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["New password must contain at least 8 characters."] });
    var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(subject, out var userId)) return Results.Unauthorized();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
    if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        return Results.Unauthorized();
    if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["New password must differ from the current password."] });

    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    var now = DateTime.UtcNow;
    await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.RevokedAt, now), ct);
    await db.PasswordResetTokens.Where(x => x.UserId == user.Id && x.UsedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.UsedAt, now), ct);
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("identity-auth");

auth.MapPost("/revoke", async (RefreshRequest request, IdentityDbContext db, CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        var hash = Hash(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    return Results.NoContent();
}).AllowAnonymous();

auth.MapGet("/permissions", (ClaimsPrincipal principal) => Results.Ok(principal.FindAll("permission").Select(x => x.Value).Distinct().Order().ToArray()))
    .RequireAuthorization();
auth.MapGet("/me", async (ClaimsPrincipal principal, IdentityDbContext db, CancellationToken ct) =>
{
    var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(subject, out var id)) return Results.Unauthorized();
    var user = await db.Users.AsNoTracking().Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .SingleOrDefaultAsync(x => x.Id == id, ct);
    return user is null ? Results.NotFound() : Results.Ok(new UserProfileResponse(
        user.Id, user.PersonId, user.Username, user.Person.FullName, user.Person.PhoneNumber, user.Person.Email,
        user.Person.Gender, user.Person.Address, user.Person.DateOfBirth,
        user.UserRoles.Select(x => x.Role.Name).Order().ToArray(), user.IsActive, user.CreatedAt));
}).RequireAuthorization();
auth.MapGet("/users/{userId:guid}", async (Guid userId, ClaimsPrincipal principal, IdentityDbContext db, CancellationToken ct) =>
{
    var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if ((!Guid.TryParse(subject, out var currentUserId) || currentUserId != userId) && !principal.IsInRole("Admin"))
        return Results.Forbid();
    var user = await db.Users.AsNoTracking().Include(x => x.Person).SingleOrDefaultAsync(x => x.Id == userId, ct);
    return user is null ? Results.NotFound() : Results.Ok(new
    {
        userId = user.Id, personId = user.PersonId, username = user.Username, isActive = user.IsActive,
        fullName = user.Person.FullName, phoneNumber = user.Person.PhoneNumber, email = user.Person.Email
    });
}).RequireAuthorization();

var internalUsers = app.MapGroup("/v1/internal/users");
internalUsers.MapPost("/provision", async (ProvisionAccountRequest request, HttpRequest httpRequest, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(httpRequest, serviceToken)) return Results.Unauthorized();
    if (request.UserId == Guid.Empty || request.PersonId == Guid.Empty ||
        string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100 ||
        string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200 ||
        (request.Password is not null && request.Password.Length < 8) ||
        (request.PhoneNumber?.Length ?? 0) > 20 || (request.Email?.Length ?? 0) > 200 ||
        (request.Address?.Length ?? 0) > 500 || request.Roles is null || request.Roles.Count == 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Account fields and at least one role are required."] });

    var username = request.Username.Trim();
    var user = await db.Users.Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .SingleOrDefaultAsync(x => x.Id == request.UserId, ct);
    if (user is null)
    {
        var retryUser = await db.Users.Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Username == username, ct);
        if (retryUser is not null)
        {
            if (retryUser.PersonId != request.PersonId || retryUser.Person.PhoneNumber != request.PhoneNumber ||
                string.IsNullOrWhiteSpace(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, retryUser.PasswordHash))
                return Results.Conflict(new { code = "username_exists", message = "Username is already registered." });
            user = retryUser;
        }
    }
    var created = user is null;
    if (created && string.IsNullOrWhiteSpace(request.Password))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["A password is required for a new account."] });
    if (user is not null && (user.PersonId != request.PersonId || user.Username != username))
        return Results.Conflict(new { code = "account_link_conflict", message = "The Identity account is already linked to a different person or username." });
    var excludedUserId = user?.Id ?? request.UserId;
    if (await db.Users.AnyAsync(x => x.Username == username && x.Id != excludedUserId, ct))
        return Results.Conflict(new { code = "username_exists", message = "Username is already registered." });

    var person = await db.Persons.SingleOrDefaultAsync(x => x.Id == request.PersonId, ct);
    if (person is null)
    {
        if (!created) return Results.Conflict(new { code = "person_missing", message = "The account profile was not found." });
        person = new PersonProfile { Id = request.PersonId };
        db.Persons.Add(person);
    }
    else if (created && !string.IsNullOrWhiteSpace(request.PhoneNumber) &&
             await db.Persons.AnyAsync(x => x.PhoneNumber == request.PhoneNumber && x.Id != request.PersonId, ct))
        return Results.Conflict(new { code = "phone_exists", message = "Phone number is already registered." });

    person.FullName = request.FullName.Trim();
    person.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
    person.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
    person.DateOfBirth = request.DateOfBirth;
    person.Gender = request.Gender;
    person.Address = request.Address;

    var normalizedRoles = request.Roles.Select(x => x.Trim()).Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var roles = await db.Roles.Where(x => normalizedRoles.Contains(x.Name)).ToListAsync(ct);
    if (roles.Count != normalizedRoles.Length)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["One or more requested roles do not exist."] });

    if (created)
    {
        user = new IdentityUser
        {
            Id = request.UserId, PersonId = request.PersonId, Person = person, Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password!), IsActive = false,
            PatientId = request.PatientId, EmployeeId = request.EmployeeId, DoctorId = request.DoctorId
        };
        db.Users.Add(user);
        foreach (var role in roles)
            db.UserRoles.Add(new IdentityUserRole { User = user, UserId = user.Id, Role = role, RoleId = role.Id });
    }
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException) { return Results.Conflict(new { code = "identity_conflict", message = "Username, person, or contact information is already in use." }); }
    return Results.Ok(new { userId = user.Id, personId = user.PersonId, isActive = user.IsActive, created });
}).AllowAnonymous();

internalUsers.MapPut("/{userId:guid}/roles", async (Guid userId, UpdateAccountRolesRequest request, HttpRequest httpRequest, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(httpRequest, serviceToken)) return Results.Unauthorized();
    var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .SingleOrDefaultAsync(x => x.Id == userId, ct);
    if (user is null) return Results.NotFound();
    if (request.Roles is null || request.Roles.Count == 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["At least one role is required."] });
    var names = request.Roles.Select(x => x.Trim()).Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var roles = await db.Roles.Where(x => names.Contains(x.Name)).ToListAsync(ct);
    if (roles.Count != names.Length)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["One or more requested roles do not exist."] });

    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    db.UserRoles.RemoveRange(user.UserRoles);
    foreach (var role in roles)
        db.UserRoles.Add(new IdentityUserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role });
    user.EmployeeId = request.EmployeeId;
    user.DoctorId = request.DoctorId;
    user.PatientId = request.PatientId;
    user.IsActive = true;
    var now = DateTime.UtcNow;
    await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
        .ExecuteUpdateAsync(changes => changes.SetProperty(x => x.RevokedAt, now), ct);
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return Results.NoContent();
}).AllowAnonymous();

internalUsers.MapGet("", async (HttpRequest request, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(request, serviceToken)) return Results.Unauthorized();
    var page = Math.Max(1, int.TryParse(request.Query["pageNumber"], out var pn) ? pn : 1);
    var size = Math.Clamp(int.TryParse(request.Query["pageSize"], out var ps) ? ps : 10, 1, 100);
    var search = request.Query["search"].ToString().Trim();
    var role = request.Query["role"].ToString().Trim();
    var gender = request.Query["gender"].ToString().Trim();
    var query = db.Users.AsNoTracking().Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role).AsQueryable();
    if (search.Length > 0) query = query.Where(x => x.Username.Contains(search) || x.Person.FullName.Contains(search) || (x.Person.PhoneNumber != null && x.Person.PhoneNumber.Contains(search)) || (x.Person.Email != null && x.Person.Email.Contains(search)));
    if (role.Length > 0) query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name == role));
    if (gender.Length > 0) query = query.Where(x => x.Person.Gender == gender);
    var descending = string.Equals(request.Query["descending"], "true", StringComparison.OrdinalIgnoreCase) || string.Equals(request.Query["orderBy"], "desc", StringComparison.OrdinalIgnoreCase);
    query = string.Equals(request.Query["sortBy"], "username", StringComparison.OrdinalIgnoreCase)
        ? descending ? query.OrderByDescending(x => x.Username) : query.OrderBy(x => x.Username)
        : descending ? query.OrderByDescending(x => x.Person.FullName) : query.OrderBy(x => x.Person.FullName);
    var total = await query.CountAsync(ct);
    var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
    return Results.Ok(new PagedAdminUsers(rows.Select(x => new AdminUserSummary(
        x.Id, x.PersonId, x.Username, x.Person.FullName, x.Person.PhoneNumber, x.Person.Email,
        x.Person.Gender, x.UserRoles.Select(ur => ur.Role.Name).Order().ToArray(), x.IsActive, x.CreatedAt)).ToArray(), total, page, size));
}).AllowAnonymous();

internalUsers.MapGet("/{userId:guid}", async (Guid userId, HttpRequest request, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(request, serviceToken)) return Results.Unauthorized();
    var user = await db.Users.AsNoTracking().Include(x => x.Person).Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .SingleOrDefaultAsync(x => x.Id == userId, ct);
    return user is null ? Results.NotFound() : Results.Ok(new AdminUserDetail(
        user.Id, user.PersonId, user.Username, user.Person.FullName, user.Person.PhoneNumber, user.Person.Email,
        user.Person.Gender, user.Person.Address, user.Person.DateOfBirth,
        user.UserRoles.Select(x => x.Role.Name).Order().ToArray(), user.IsActive, user.CreatedAt,
        user.PatientId, user.EmployeeId, user.DoctorId));
}).AllowAnonymous();

app.MapGet("/v1/internal/roles", async (HttpRequest request, IdentityDbContext db, CancellationToken ct) =>
{
    if (!HasServiceToken(request, serviceToken)) return Results.Unauthorized();
    var names = request.Query["name"].SelectMany(x => (x ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
    var roles = await db.Roles.AsNoTracking().Include(x => x.RolePermissions).ThenInclude(x => x.Permission)
        .OrderBy(x => x.Name).ToListAsync(ct);
    if (names.Length > 0) roles = roles.Where(x => names.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList();
    return Results.Ok(roles.Select(x => new
    {
        x.Id, x.Name, x.Description,
        permissions = x.RolePermissions.Select(rp => rp.Permission.Name).Order().ToArray()
    }));
}).AllowAnonymous();

var rotationSecret = builder.Configuration["Jwt:RotationSecret"];
app.MapPost("/internal/keys/rotate", (HttpRequest request, SigningKeyRing ring) =>
{
    if (string.IsNullOrEmpty(rotationSecret) || request.Headers["X-Key-Rotation-Secret"] != rotationSecret)
        return Results.NotFound();
    return Results.Ok(new { activeKid = ring.Rotate() });
}).AllowAnonymous();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    await SeedRolesAsync(db);
}
app.Run();

static bool HasServiceToken(HttpRequest request, string? configuredToken)
{
    if (string.IsNullOrWhiteSpace(configuredToken) ||
        !request.Headers.TryGetValue("X-Identity-Service-Token", out var suppliedToken) ||
        string.IsNullOrWhiteSpace(suppliedToken)) return false;
    var expected = Encoding.UTF8.GetBytes(configuredToken);
    var supplied = Encoding.UTF8.GetBytes(suppliedToken.ToString());
    return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
}

static async Task<TokenResponse> IssueTokens(IdentityUser user, IdentityDbContext db, SigningKeyRing ring, string issuer,
    string audience, int accessMinutes, int refreshDays, CancellationToken ct)
{
    var roles = user.UserRoles.Select(x => x.Role.Name).Distinct().ToArray();
    var permissions = user.UserRoles.SelectMany(x => x.Role.RolePermissions).Select(x => x.Permission.Name).Distinct().ToArray();
    var now = DateTime.UtcNow;
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new("personId", user.PersonId.ToString())
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
    claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
    if (user.PatientId is Guid patientId) claims.Add(new Claim("patientId", patientId.ToString()));
    if (user.EmployeeId is Guid employeeId) claims.Add(new Claim("employeeId", employeeId.ToString()));
    if (user.DoctorId is Guid doctorId) claims.Add(new Claim("doctorId", doctorId.ToString()));
    var jwt = new JwtSecurityToken(issuer, audience, claims, now, now.AddMinutes(accessMinutes), ring.Credentials);
    var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
    var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    db.RefreshTokens.Add(new IdentityRefreshToken
    {
        Id = Guid.NewGuid(), UserId = user.Id, User = user, TokenHash = Hash(refreshToken),
        IssuedAt = now, ExpiresAt = now.AddDays(refreshDays)
    });
    await db.SaveChangesAsync(ct);
    return new TokenResponse(accessToken, refreshToken, roles);
}

static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

static async Task SeedRolesAsync(IdentityDbContext db)
{
    var roles = new Dictionary<string, Guid>
    {
        ["Admin"] = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        ["Patient"] = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        ["Receptionist"] = Guid.Parse("00000000-0000-0000-0000-000000000003"),
        ["Doctor"] = Guid.Parse("00000000-0000-0000-0000-000000000004")
    };
    foreach (var (roleName, roleId) in roles)
        if (!await db.Roles.AnyAsync(x => x.Name == roleName))
            db.Roles.Add(new IdentityRole { Id = roleId, Name = roleName, Description = roleName });
    await db.SaveChangesAsync();
}

public partial class Program { }

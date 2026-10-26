using Clinic.API.Models;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net;
using Clinic.API.Services;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly HttpClient _identityClient;
        private readonly string? _identityServiceToken;
        private readonly int _refreshTokenDays;
        private readonly bool _useIdentityService;
        private readonly ILogger<AuthController> _logger;
        private readonly MedicalRecordsServiceClient _medicalRecordsClient;
        private readonly NotificationServiceClient _notificationClient;

        public AuthController(
            ISender sender,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            MedicalRecordsServiceClient medicalRecordsClient,
            NotificationServiceClient notificationClient)
        {
            _sender = sender;
            _identityClient = httpClientFactory.CreateClient("Identity");
            _identityServiceToken = configuration["Identity:ServiceToken"];
            _refreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 7);
            _useIdentityService = configuration.GetValue("Features:UseIdentityService", true);
            _logger = logger;
            _medicalRecordsClient = medicalRecordsClient;
            _notificationClient = notificationClient;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthLoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        // The login body exposes only client-safe session metadata; the refresh token stays in the HttpOnly cookie.
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            var clientIp = GetClientIp();
            _logger.LogInformation("AUTH_AUDIT: Login attempt for username '{Username}' from IP '{ClientIp}'. CorrelationId: {CorrelationId}",
                request.Username, clientIp, correlationId);

            try
            {
                using var upstreamRequest = CreatePublicRequest(HttpMethod.Post, "v1/auth/login");
                upstreamRequest.Content = JsonContent.Create(request);
                using var upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Login failed for username '{Username}' with status {StatusCode}. CorrelationId: {CorrelationId}",
                        request.Username, (int)upstream.StatusCode, correlationId);
                    return StatusCode((int)upstream.StatusCode, new { message = "Username or password is incorrect." });
                }

                var response = await upstream.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: HttpContext.RequestAborted);
                if (response is null)
                {
                    _logger.LogError("AUTH_AUDIT: Identity service returned null payload on login for username '{Username}'. CorrelationId: {CorrelationId}",
                        request.Username, correlationId);
                    return StatusCode(StatusCodes.Status502BadGateway);
                }

                _logger.LogInformation("AUTH_AUDIT: Login succeeded for username '{Username}', roles: [{Roles}]. CorrelationId: {CorrelationId}",
                    request.Username, string.Join(", ", response.Roles), correlationId);
                return CreateLoginResponse(response.AccessToken, response.RefreshToken, response.Roles);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during login for username '{Username}'. CorrelationId: {CorrelationId}",
                    request.Username, correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            var clientIp = GetClientIp();
            _logger.LogInformation("AUTH_AUDIT: Registration attempt for username '{Username}', phone '{PhoneNumber}' from IP '{ClientIp}'. CorrelationId: {CorrelationId}",
                request.Username.Trim(), request.PhoneNumber.Trim(), clientIp, correlationId);

            var profileIds = await _medicalRecordsClient.GetInternalAsync<IdentityRegistrationPatientIds>(
                $"internal/patients/resolve-registration?phoneNumber={Uri.EscapeDataString(request.PhoneNumber.Trim())}",
                HttpContext.RequestAborted);
            if (profileIds is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Medical Records Service is unavailable for patient registration." });
            using var upstreamRequest = CreateServiceRequest(HttpMethod.Post, "v1/auth/register");
            upstreamRequest.Content = JsonContent.Create(new IdentityRegisterRequest(
                request.Username.Trim(), request.Password, request.FullName.Trim(), request.PhoneNumber.Trim(),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(), request.DateOfBirth,
                request.Gender.ToString(), request.Address, profileIds.PersonId, profileIds.PatientId));

            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during register for username '{Username}'. CorrelationId: {CorrelationId}",
                    request.Username.Trim(), correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }

            using (upstream)
            {
                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Registration rejected for username '{Username}' with status {StatusCode}. CorrelationId: {CorrelationId}",
                        request.Username.Trim(), (int)upstream.StatusCode, correlationId);
                    return StatusCode((int)upstream.StatusCode, new { message = "Identity Service rejected the registration." });
                }

                var identity = await upstream.Content.ReadFromJsonAsync<IdentityRegisterResponse>(cancellationToken: HttpContext.RequestAborted);
                if (identity is null || identity.PatientId is null)
                {
                    _logger.LogError("AUTH_AUDIT: Identity service returned incomplete registration response for '{Username}'. CorrelationId: {CorrelationId}",
                        request.Username.Trim(), correlationId);
                    return StatusCode(StatusCodes.Status502BadGateway, new { message = "Identity Service returned an incomplete registration response." });
                }

                var provisioned = await _medicalRecordsClient.PostInternalAsync("internal/patients/provision-identity",
                    new IdentityPatientProvisionRequest(identity.PatientId.Value, identity.PersonId, identity.UserId,
                        request.FullName.Trim(), request.PhoneNumber.Trim(), request.Email, request.DateOfBirth,
                        request.Gender.ToString(), request.Address), HttpContext.RequestAborted);
                if (!provisioned)
                {
                    _logger.LogError("AUTH_AUDIT: Medical Records patient provisioning failed after Identity account creation. UserId={UserId}, CorrelationId={CorrelationId}",
                        identity.UserId, correlationId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new { message = "The account was created, but the Patient profile could not be provisioned. Contact support to retry provisioning." });
                }

                _logger.LogInformation("AUTH_AUDIT: Registration completed successfully for username '{Username}', userId '{UserId}'. CorrelationId: {CorrelationId}",
                    request.Username.Trim(), identity.UserId, correlationId);
                return StatusCode(StatusCodes.Status201Created, new { id = identity.UserId });
            }
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout()
        {
            var correlationId = GetCorrelationId();
            _logger.LogInformation("AUTH_AUDIT: Logout requested. CorrelationId: {CorrelationId}", correlationId);

            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return NoContent();
            }

            if (_useIdentityService)
            {
                try
                {
                    using var upstreamRequest = CreatePublicRequest(HttpMethod.Post, "v1/auth/revoke");
                    upstreamRequest.Content = JsonContent.Create(new RefreshRequest { RefreshToken = refreshToken });
                    using var upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
                    if (!upstream.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("AUTH_AUDIT: Upstream token revocation returned {StatusCode} on logout. CorrelationId: {CorrelationId}",
                            (int)upstream.StatusCode, correlationId);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "AUTH_AUDIT: Upstream identity unreachable on logout revocation. CorrelationId: {CorrelationId}", correlationId);
                }
            }

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth"
            });

            _logger.LogInformation("AUTH_AUDIT: Logout completed. CorrelationId: {CorrelationId}", correlationId);
            return NoContent();
        }

        [HttpPost("revoke")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequest request)
        {
            var correlationId = GetCorrelationId();
            _logger.LogInformation("AUTH_AUDIT: Token revoke requested. CorrelationId: {CorrelationId}", correlationId);

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return NoContent();
            }

            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            try
            {
                using var upstreamRequest = CreatePublicRequest(HttpMethod.Post, "v1/auth/revoke");
                upstreamRequest.Content = JsonContent.Create(request);
                using var upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Upstream revoke returned status {StatusCode}. CorrelationId: {CorrelationId}",
                        (int)upstream.StatusCode, correlationId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during revoke. CorrelationId: {CorrelationId}", correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth"
            });

            _logger.LogInformation("AUTH_AUDIT: Token revoke completed. CorrelationId: {CorrelationId}", correlationId);
            return NoContent();
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthRefreshResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh()
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            _logger.LogInformation("AUTH_AUDIT: Token refresh attempt from IP '{ClientIp}'. CorrelationId: {CorrelationId}",
                GetClientIp(), correlationId);

            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("AUTH_AUDIT: Token refresh rejected: refresh token missing in cookies. CorrelationId: {CorrelationId}", correlationId);
                return Unauthorized(new { message = "Refresh token is missing." });
            }

            try
            {
                using var upstreamRequest = CreatePublicRequest(HttpMethod.Post, "v1/auth/refresh");
                upstreamRequest.Content = JsonContent.Create(new RefreshRequest { RefreshToken = refreshToken });
                using var upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);

                if (upstream.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("AUTH_AUDIT: Token refresh rejected: invalid or expired token. CorrelationId: {CorrelationId}", correlationId);
                    Response.Cookies.Delete("refreshToken", RefreshCookieOptions());
                    return Unauthorized(new { message = "Refresh token is invalid or expired." });
                }

                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Token refresh failed with upstream status {StatusCode}. CorrelationId: {CorrelationId}",
                        (int)upstream.StatusCode, correlationId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                var response = await upstream.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: HttpContext.RequestAborted);
                if (response is null)
                {
                    _logger.LogError("AUTH_AUDIT: Identity service returned null payload on refresh. CorrelationId: {CorrelationId}", correlationId);
                    return StatusCode(StatusCodes.Status502BadGateway);
                }

                Response.Cookies.Append("refreshToken", response.RefreshToken, RefreshCookieOptions());
                _logger.LogInformation("AUTH_AUDIT: Token refresh succeeded. CorrelationId: {CorrelationId}", correlationId);
                return Ok(new { accessToken = response.AccessToken });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during refresh. CorrelationId: {CorrelationId}", correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(IdentityUserProfileResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserInfo()
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            using var request = CreateAuthorizedRequest(HttpMethod.Get, "v1/auth/me");
            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.SendAsync(request, HttpContext.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during /auth/me. CorrelationId: {CorrelationId}", correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            using (upstream)
            {
                if (upstream.IsSuccessStatusCode)
                {
                    var profile = await upstream.Content.ReadFromJsonAsync<IdentityUserProfileResponse>(cancellationToken: HttpContext.RequestAborted);
                    return profile is null ? StatusCode(StatusCodes.Status502BadGateway) : Ok(profile);
                }
                if (upstream.StatusCode == HttpStatusCode.Unauthorized) return Unauthorized();
                if (upstream.StatusCode == HttpStatusCode.NotFound) return NotFound();
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        [HttpGet("permissions")]
        [Authorize]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        public IActionResult GetPermissions()
        {
            var permissions = User.FindAll("permission")
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(permission => permission, StringComparer.Ordinal)
                .ToArray();

            return Ok(permissions);
        }

        [HttpPost("/forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            _logger.LogInformation("AUTH_AUDIT: Forgot password requested for identifier '{Identifier}'. CorrelationId: {CorrelationId}",
                request.PhoneNumberOrEmail, correlationId);

            using var upstreamRequest = CreateServiceRequest(HttpMethod.Post, "v1/auth/forgot-password");
            upstreamRequest.Content = JsonContent.Create(new IdentityForgotPasswordRequest(request.PhoneNumberOrEmail));
            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during forgot-password. CorrelationId: {CorrelationId}", correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Password reset is temporarily unavailable." });
            }

            using (upstream)
            {
                if (!upstream.IsSuccessStatusCode)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new { message = "Password reset is temporarily unavailable." });
                var delivery = await upstream.Content.ReadFromJsonAsync<IdentityPasswordResetDelivery>(cancellationToken: HttpContext.RequestAborted);
                if (delivery is { Found: true, PersonId: not null, ResetToken: not null })
                {
                    var identifierValue = request.PhoneNumberOrEmail.Trim();
                    var isEmail = identifierValue.Contains('@');
                    var email = isEmail ? identifierValue : null;
                    var phone = isEmail ? null : identifierValue;

                    await _notificationClient.SendPasswordResetNotificationAsync(
                        delivery.PersonId.Value,
                        email,
                        phone,
                        delivery.ResetToken,
                        HttpContext.RequestAborted);
                }


                _logger.LogInformation("AUTH_AUDIT: Forgot password request completed for identifier '{Identifier}'. Found: {Found}. CorrelationId: {CorrelationId}",
                    request.PhoneNumberOrEmail, delivery?.Found == true, correlationId);
                return Ok(new { message = "If the account exists, a password reset code has been sent to its registered contact." });
            }
        }

        [HttpPost("/reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            _logger.LogInformation("AUTH_AUDIT: Reset password attempt from IP '{ClientIp}'. CorrelationId: {CorrelationId}",
                GetClientIp(), correlationId);

            HttpResponseMessage upstream;
            try
            {
                using var upstreamRequest = CreatePublicRequest(HttpMethod.Post, "v1/auth/reset-password");
                upstreamRequest.Content = JsonContent.Create(new IdentityResetPasswordRequest(request.Token, request.NewPassword));
                upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during reset-password. CorrelationId: {CorrelationId}", correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            using (upstream)
            {
                if (upstream.StatusCode == HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("AUTH_AUDIT: Password reset code invalid or expired. CorrelationId: {CorrelationId}", correlationId);
                    return BadRequest(new { message = "The reset code or new password is invalid." });
                }
                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Upstream reset-password returned status {StatusCode}. CorrelationId: {CorrelationId}",
                        (int)upstream.StatusCode, correlationId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                _logger.LogInformation("AUTH_AUDIT: Password reset completed successfully. CorrelationId: {CorrelationId}", correlationId);
                return Ok(new { message = "If the reset code is valid, your password has been updated. You can now sign in." });
            }
        }

        [HttpPost("/change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangePassword([FromBody] UpdatePasswordRequest request)
        {
            if (CheckIdentityFeatureFlag() is { } flagResult) return flagResult;

            var correlationId = GetCorrelationId();
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value ?? "unknown";
            _logger.LogInformation("AUTH_AUDIT: Password change requested for user '{UserId}'. CorrelationId: {CorrelationId}",
                userId, correlationId);

            using var upstreamRequest = CreateAuthorizedRequest(HttpMethod.Post, "v1/auth/change-password");
            upstreamRequest.Content = JsonContent.Create(request);
            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AUTH_AUDIT: Identity service unreachable during change-password for user '{UserId}'. CorrelationId: {CorrelationId}",
                    userId, correlationId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            using (upstream)
            {
                if (upstream.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("AUTH_AUDIT: Password change rejected: invalid current password for user '{UserId}'. CorrelationId: {CorrelationId}",
                        userId, correlationId);
                    return Unauthorized(new { message = "Current password is incorrect or the session is invalid." });
                }
                if (upstream.StatusCode == HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("AUTH_AUDIT: Password change rejected: bad request for user '{UserId}'. CorrelationId: {CorrelationId}",
                        userId, correlationId);
                    return BadRequest(new { message = "New password is invalid or does not meet the password policy." });
                }
                if (!upstream.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AUTH_AUDIT: Upstream change-password returned status {StatusCode} for user '{UserId}'. CorrelationId: {CorrelationId}",
                        (int)upstream.StatusCode, userId, correlationId);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                _logger.LogInformation("AUTH_AUDIT: Password changed successfully for user '{UserId}'. CorrelationId: {CorrelationId}",
                    userId, correlationId);
                return Ok(new { message = "Password updated successfully." });
            }
        }

    internal sealed record IdentityTokenResponse(string AccessToken, string RefreshToken, IReadOnlyList<string> Roles);
    private sealed record IdentityPatientProvisionRequest(Guid PatientId, Guid PersonId, Guid IdentityUserId,
        string FullName, string PhoneNumber, string? Email, DateOnly DateOfBirth, string Gender, string Address);
    private sealed record IdentityRegistrationPatientIds(Guid PersonId, Guid PatientId);

    private IActionResult? CheckIdentityFeatureFlag()
    {
        if (!_useIdentityService)
        {
            _logger.LogWarning("AUTH_AUDIT: Request rejected because Identity Service is disabled by configuration. CorrelationId: {CorrelationId}",
                GetCorrelationId());
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Identity Service routing is currently disabled by configuration." });
        }
        return null;
    }

    private string GetCorrelationId()
    {
        if (HttpContext.Response.Headers.TryGetValue("X-Correlation-ID", out var cid) && !string.IsNullOrWhiteSpace(cid))
            return cid.ToString();
        if (HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out cid) && !string.IsNullOrWhiteSpace(cid))
            return cid.ToString();
        return Guid.NewGuid().ToString();
    }

    private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private IActionResult CreateLoginResponse(string accessToken, string refreshToken, IReadOnlyList<string> roles)
    {
        Response.Cookies.Append("refreshToken", refreshToken, RefreshCookieOptions());
        return Ok(new { accessToken, roles });
    }

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenDays),
        Path = "/auth"
    };

    private HttpRequestMessage CreatePublicRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", GetCorrelationId());
        return request;
    }

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_identityServiceToken))
            request.Headers.TryAddWithoutValidation("X-Identity-Service-Token", _identityServiceToken);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", GetCorrelationId());
        return request;
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (Request.Headers.Authorization.Count > 0)
            request.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", GetCorrelationId());
        return request;
    }
    }
}

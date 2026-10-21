using Clinic.API.Models;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net;

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

        public AuthController(ISender sender, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _sender = sender;
            _identityClient = httpClientFactory.CreateClient("Identity");
            _identityServiceToken = configuration["Identity:ServiceToken"];
            _refreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 7);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthLoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        // The login body exposes only client-safe session metadata; the refresh token stays in the HttpOnly cookie.
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                using var upstream = await _identityClient.PostAsJsonAsync("v1/auth/login", request, HttpContext.RequestAborted);
                if (!upstream.IsSuccessStatusCode)
                    return StatusCode((int)upstream.StatusCode, new { message = "Username or password is incorrect." });
                var response = await upstream.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: HttpContext.RequestAborted);
                if (response is null) return StatusCode(StatusCodes.Status502BadGateway);
                return CreateLoginResponse(response.AccessToken, response.RefreshToken, response.Roles);
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var profileIds = await _sender.Send(new ResolveIdentityRegistrationProfileQuery(request.PhoneNumber));
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
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }
            using (upstream)
            {
            if (!upstream.IsSuccessStatusCode)
                return StatusCode((int)upstream.StatusCode, new { message = "Identity Service rejected the registration." });
            var identity = await upstream.Content.ReadFromJsonAsync<IdentityRegisterResponse>(cancellationToken: HttpContext.RequestAborted);
            if (identity is null || identity.PatientId is null)
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Identity Service returned an incomplete registration response." });

            try
            {
                await _sender.Send(new ProvisionIdentityPatientCommand(
                    identity.UserId, identity.PersonId, identity.PatientId.Value,
                    request.FullName.Trim(), request.PhoneNumber.Trim(), request.Email, request.DateOfBirth,
                    request.Gender, request.Address), HttpContext.RequestAborted);
            }
            catch (Exception)
            {
                // Registration is retryable: Identity returns the same account for an authenticated duplicate request.
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "The account was created, but the Patient profile is still being provisioned. Please retry registration shortly." });
            }
            return StatusCode(StatusCodes.Status201Created, new { id = identity.UserId });
            }
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return NoContent();
            }
            try
            {
                using var upstream = await _identityClient.PostAsJsonAsync("v1/auth/revoke", new RefreshRequest { RefreshToken = refreshToken }, HttpContext.RequestAborted);
                if (!upstream.IsSuccessStatusCode) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth"
            });
            return NoContent();
        }

        [HttpPost("revoke")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return NoContent();
            }

            try
            {
                using var upstream = await _identityClient.PostAsJsonAsync("v1/auth/revoke", request, HttpContext.RequestAborted);
                if (!upstream.IsSuccessStatusCode) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth"
            });
            return NoContent();
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthRefreshResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                return Unauthorized(new { message = "Refresh token is missing." });
            }
            try
            {
                using var upstream = await _identityClient.PostAsJsonAsync("v1/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, HttpContext.RequestAborted);
                if (upstream.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Response.Cookies.Delete("refreshToken", RefreshCookieOptions());
                    return Unauthorized(new { message = "Refresh token is invalid or expired." });
                }
                if (!upstream.IsSuccessStatusCode)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                var response = await upstream.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: HttpContext.RequestAborted);
                if (response is null) return StatusCode(StatusCodes.Status502BadGateway);
                Response.Cookies.Append("refreshToken", response.RefreshToken, RefreshCookieOptions());
                return Ok(new { accessToken = response.AccessToken });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Identity Service is temporarily unavailable." });
            }
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(IdentityUserProfileResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserInfo()
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, "v1/auth/me");
            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.SendAsync(request, HttpContext.RequestAborted);
            }
            catch (HttpRequestException)
            {
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
            using var upstreamRequest = CreateServiceRequest(HttpMethod.Post, "v1/auth/forgot-password");
            upstreamRequest.Content = JsonContent.Create(new IdentityForgotPasswordRequest(request.PhoneNumberOrEmail));
            using var upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted);
            if (!upstream.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Password reset is temporarily unavailable." });
            var delivery = await upstream.Content.ReadFromJsonAsync<IdentityPasswordResetDelivery>(cancellationToken: HttpContext.RequestAborted);
            if (delivery is { Found: true, PersonId: not null, ResetToken: not null })
                await _sender.Send(new QueuePasswordResetNotificationCommand(delivery.PersonId.Value, delivery.ResetToken), HttpContext.RequestAborted);
            return Ok(new { message = "If the account exists, a password reset code has been sent to its registered contact." });
        }

        [HttpPost("/reset-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            HttpResponseMessage upstream;
            try
            {
                upstream = await _identityClient.PostAsJsonAsync("v1/auth/reset-password",
                    new IdentityResetPasswordRequest(request.Token, request.NewPassword), HttpContext.RequestAborted);
            }
            catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
            using (upstream)
            {
            if (upstream.StatusCode == HttpStatusCode.BadRequest)
                return BadRequest(new { message = "The reset code or new password is invalid." });
            if (!upstream.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            return Ok(new { message = "If the reset code is valid, your password has been updated. You can now sign in." });
            }
        }

        [HttpPost("/change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangePassword([FromBody] UpdatePasswordRequest request)
        {
            using var upstreamRequest = CreateAuthorizedRequest(HttpMethod.Post, "v1/auth/change-password");
            upstreamRequest.Content = JsonContent.Create(request);
            HttpResponseMessage upstream;
            try { upstream = await _identityClient.SendAsync(upstreamRequest, HttpContext.RequestAborted); }
            catch (HttpRequestException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
            using (upstream)
            {
            if (upstream.StatusCode == HttpStatusCode.Unauthorized) return Unauthorized(new { message = "Current password is incorrect or the session is invalid." });
            if (upstream.StatusCode == HttpStatusCode.BadRequest) return BadRequest(new { message = "New password is invalid or does not meet the password policy." });
            if (!upstream.IsSuccessStatusCode) return StatusCode(StatusCodes.Status503ServiceUnavailable);
            return Ok(new { message = "Password updated successfully." });
            }
        }

    internal sealed record IdentityTokenResponse(string AccessToken, string RefreshToken, IReadOnlyList<string> Roles);

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

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_identityServiceToken))
            request.Headers.TryAddWithoutValidation("X-Identity-Service-Token", _identityServiceToken);
        return request;
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (Request.Headers.Authorization.Count > 0)
            request.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
        return request;
    }
    }
}

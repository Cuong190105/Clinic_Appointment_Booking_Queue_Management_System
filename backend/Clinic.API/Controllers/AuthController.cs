using Clinic.API.Models;
using Clinic.Application.Contracts;
using Clinic.Application.Features.Auth.Commands;
using Clinic.Application.Features.Users.NewFolder;
using Clinic.Application.Features.Users.Queries;
using Clinic.Application.Interfaces;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.API.Controllers
{
    [ApiController]
    [Route("/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public AuthController(ISender sender, IMapper mapper, ICurrentUser currentUser)
        {
            _sender = sender;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        // The login body exposes only client-safe session metadata; the refresh token stays in the HttpOnly cookie.
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var command = _mapper.Map<LoginCommand>(request);
            var response = await _sender.Send(command);
            Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/auth"
            });
            return Ok(new
            {
                accessToken = response.AccessToken,
                roles = response.Roles
            });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                request.Email = null;
            }
            var command = _mapper.Map<RegisterCommand>(request);
            var response = await _sender.Send(command);
            return StatusCode(StatusCodes.Status201Created, new { id = response });
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
            var command = new LogoutCommand(refreshToken);
            await _sender.Send(command);
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
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                return Unauthorized(new { message = "Refresh token is missing." });
            }
            var command = _mapper.Map<RefreshCommand>(new RefreshRequest { RefreshToken = refreshToken });
            var response = await _sender.Send(command);
            Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/auth"
            });
            return Ok(new
            {
                accessToken = response.AccessToken
            });
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserInfo()
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not found.");
            
            var command = new GetUserQuery(userId);
            var response = await _sender.Send(command);
            return Ok(response);
        }

        [HttpPost("/forgot-password")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var command = _mapper.Map<ForgotPasswordCommand>(request);
            await _sender.Send(command);
            return Ok(new { message = "If the email exists, a password reset link has been sent." });
        }

        [HttpPost("/change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChangePassword([FromBody] UpdatePasswordRequest request)
        {
            var command = _mapper.Map<UpdatePasswordCommand>(request);
            await _sender.Send(command);
            return Ok(new { message = "Password updated successfully." });
        }

    }
}

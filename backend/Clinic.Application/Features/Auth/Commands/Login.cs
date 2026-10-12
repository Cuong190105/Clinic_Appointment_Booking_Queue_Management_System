using Clinic.Application.Interfaces;
using Clinic.Application.Services;
using Clinic.Domain.Entities;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands
{
    public record LoginCommand(string Username, string Password) : IRequest<LoginResponse>;

    // Login callers need roles immediately after authentication to choose the correct app surface without decoding JWT claims.
    public record LoginResponse(
        string AccessToken,
        string RefreshToken,
        IReadOnlyList<string> Roles
    );

    public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public LoginCommandHandler(IUserService userService, ITokenService tokenService, IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        // Keep role names in the application response while refresh tokens remain server-managed through the auth cookie.
        public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
        {
            var user = await _userService.VerifyUser(command.Username, command.Password);
            var tokens = await _tokenService.GenerateTokensAsync(user);
            var roles = user.UserRoles
                .Select(ur => ur.Role.Name)
                .Distinct()
                .ToList();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new LoginResponse(
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                Roles: roles
            );
        }
    }
}

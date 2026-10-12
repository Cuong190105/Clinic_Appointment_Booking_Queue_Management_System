using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands
{
    public record LogoutCommand(string RefreshToken) : IRequest;
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ITokenProvider _tokenProvider;
        private readonly IUnitOfWork _unitOfWork;
        public LogoutCommandHandler(IRefreshTokenRepository refreshTokenRepository, ITokenProvider tokenProvider, IUnitOfWork unitOfWork)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _tokenProvider = tokenProvider;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var tokenHash = _tokenProvider.HashToken(request.RefreshToken);
            var token = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);
            if (token !=  null)
            {
                token.Revoke();
                await _refreshTokenRepository.UpdateAsync(token);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

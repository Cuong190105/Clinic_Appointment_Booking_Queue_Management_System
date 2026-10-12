using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Users.NewFolder
{
    public record UpdatePasswordCommand(
        string CurrentPassword,
        string NewPassword
    ) : IRequest<int>;
    public class UpdatePasswordCommandHandler : IRequestHandler<UpdatePasswordCommand, int>
    {
        private readonly IUserService _userService;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        public UpdatePasswordCommandHandler(IUserService userService, ICurrentUser currentUser, IUnitOfWork unitOfWork)
        {
            _userService = userService;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
        {
            await _userService.VerifyAndUpdatePassword(_currentUser.UserId ?? throw new UnauthorizedAccessException(), request.CurrentPassword, request.NewPassword);
            await _unitOfWork.SaveChangesAsync();
            return 0;
        }
    }
}

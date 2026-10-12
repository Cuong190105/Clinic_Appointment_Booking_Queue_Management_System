using Clinic.Application.Common.Exceptions;
using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Clinic.Application.Features.Auth.Commands
{
    public record ForgotPasswordCommand(string PhoneNumberOrEmail) : IRequest<int>;
    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, int>
    {
        private readonly IPersonRepository _personRepository;
        private readonly IUserService _userService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationQueue _notificationQueue;
        public ForgotPasswordCommandHandler(IPersonRepository personRepository, IUserService userService, IUnitOfWork unitOfWork, INotificationQueue notificationQueue)
        {
            _personRepository = personRepository;
            _userService = userService;
            _unitOfWork = unitOfWork;
            _notificationQueue = notificationQueue;
        }

        public async Task<int> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _personRepository.GetByPhoneNumberAsync(request.PhoneNumberOrEmail);
            if (user == null)
            {
                user = await _personRepository.GetByEmailAsync(request.PhoneNumberOrEmail);
                if (user == null)
                {
                    return -1; // No spoil user info
                }
            }

            if (user.User == null)
            {
                return -1; // No spoil user info
            }

            // Temporary way to reset password.
            const string chars =
                "abcdefghijklmnopqrstuvwxyz" +
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                "0123456789" +
                "!@#$%^&*";

            var randomPassword = new char[15];
            for (int i = 0; i < randomPassword.Length; i++)
            {
                randomPassword[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }

            // Send a notification to user via email below
             await _notificationQueue.EnqueueAsync(new NotificationJob<Account>(
                 user,
                 new Account { FullName = user.FullName, Password = new string(randomPassword) },
                 NotificationType.ResetPassword,
                 true, false, false), cancellationToken
             );
            Console.WriteLine($"Temporary password for {user.FullName}: {new string(randomPassword)}");

            await _userService.UpdatePassword(user.User, new string(randomPassword));
            await _unitOfWork.SaveChangesAsync();

            return 0;
        }
    }
}

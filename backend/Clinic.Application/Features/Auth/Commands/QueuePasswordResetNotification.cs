using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands;

public sealed record QueuePasswordResetNotificationCommand(Guid PersonId, string ResetToken)
    : IRequest<bool>;

public sealed class QueuePasswordResetNotificationHandler(
    IPersonRepository people,
    INotificationQueue notificationQueue)
    : IRequestHandler<QueuePasswordResetNotificationCommand, bool>
{
    public async Task<bool> Handle(
        QueuePasswordResetNotificationCommand request, CancellationToken cancellationToken)
    {
        var person = await people.GetByIdAsync(request.PersonId);
        if (person is null || (string.IsNullOrWhiteSpace(person.Email) && string.IsNullOrWhiteSpace(person.PhoneNumber)))
            return false;

        await notificationQueue.EnqueueAsync(new NotificationJob<Account>(
            person,
            new Account { FullName = person.FullName, Email = person.Email, PhoneNumber = person.PhoneNumber, PasswordResetCode = request.ResetToken },
            NotificationType.ResetPassword,
            SendEmail: !string.IsNullOrWhiteSpace(person.Email),
            SendInApp: false,
            SendSms: string.IsNullOrWhiteSpace(person.Email) && !string.IsNullOrWhiteSpace(person.PhoneNumber)), cancellationToken);
        return true;
    }
}

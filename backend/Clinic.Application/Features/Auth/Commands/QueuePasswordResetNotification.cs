using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using Clinic.Contracts.Events;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands;

public sealed record QueuePasswordResetNotificationCommand(Guid PersonId, string ResetToken)
    : IRequest<bool>;

public sealed class QueuePasswordResetNotificationHandler(
    IPersonRepository people,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    ICorrelationContext correlation)
    : IRequestHandler<QueuePasswordResetNotificationCommand, bool>
{
    public async Task<bool> Handle(
        QueuePasswordResetNotificationCommand request, CancellationToken cancellationToken)
    {
        var person = await people.GetByIdAsync(request.PersonId);
        if (person is null || (string.IsNullOrWhiteSpace(person.Email) && string.IsNullOrWhiteSpace(person.PhoneNumber)))
            return false;

        var channels = !string.IsNullOrWhiteSpace(person.Email) ? new[] { "email" } : new[] { "sms" };
        var payload = new NotificationRequested(person.Id, "password-reset", "Đặt lại mật khẩu",
            $"Mã đặt lại mật khẩu của bạn là: {request.ResetToken}", channels, person.Email, person.PhoneNumber);
        outbox.Add(new EventEnvelope<NotificationRequested>
        {
            EventId = Guid.NewGuid(), EventType = EventTypes.NotificationRequested, SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow, CorrelationId = correlation.CorrelationId ?? Guid.NewGuid(),
            Producer = "Clinic.API", Payload = payload
        }, "notification.requested");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

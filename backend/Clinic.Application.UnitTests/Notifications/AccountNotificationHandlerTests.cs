using Clinic.Application.Common.Models;
using Clinic.Application.Interfaces;
using Clinic.Application.Notifications.Dispatchers;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Notifications;

public sealed class AccountNotificationHandlerTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Reset_code_is_sent_but_never_persisted_in_notification_content(bool sendEmail, bool sendSms)
    {
        const string resetCode = "sensitive-one-time-reset-code";
        var repository = new CapturingNotificationRepository();
        var email = new CapturingEmailSender();
        var sms = new CapturingSmsSender();
        var handler = new AccountNotificationHandler(repository, email, sms, new NoopInAppSender());
        var person = new Person("Reset Test", "0912345678", "reset@example.test", new DateOnly(1990, 1, 1), Gender.Other, "Address");
        var job = new NotificationJob<Account>(
            person,
            new Account { FullName = person.FullName, Email = person.Email, PhoneNumber = person.PhoneNumber, PasswordResetCode = resetCode },
            NotificationType.ResetPassword,
            SendEmail: sendEmail,
            SendInApp: false,
            SendSms: sendSms);

        await handler.HandleAsync(job);

        Assert.NotEmpty(repository.Notifications);
        Assert.All(repository.Notifications, notification => Assert.DoesNotContain(resetCode, notification.Message));
        if (sendEmail) Assert.Contains(resetCode, email.Body);
        if (sendSms) Assert.Contains(resetCode, sms.Message);
    }

    private sealed class CapturingNotificationRepository : INotificationRepository
    {
        public List<Notification> Notifications { get; } = [];
        public Task AddAsync(Notification notification, CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
        public Task<IEnumerable<Notification>> GetAllScheduledAndFailedByAsync(DateTimeOffset time, CancellationToken stoppingToken) => throw new NotImplementedException();
        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<Notification>> GetNotificationsForPerson(Guid? personId, DateTime createdBefore, int limit) => throw new NotImplementedException();
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string Body { get; private set; } = string.Empty;
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            Body = body;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingSmsSender : ISmsSender
    {
        public string Message { get; private set; } = string.Empty;
        public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopInAppSender : IInAppSender
    {
        public Task SendAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}


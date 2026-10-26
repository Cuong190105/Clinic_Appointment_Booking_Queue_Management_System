namespace Clinic.Notification.Service.Services.Delivery;

public interface IEmailSender
{
    Task<ProviderDeliveryResult> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task<ProviderDeliveryResult> SendSmsAsync(string toPhone, string message, CancellationToken ct = default);
}

public interface IRealtimeNotifier
{
    Task SendNotificationHintAsync(Guid userId, Guid notificationId, string notificationType, CancellationToken ct = default);
}

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task<ProviderDeliveryResult> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("EMAIL_SENT: to={Email}, subject={Subject}", toEmail, subject);
        return Task.FromResult(new ProviderDeliveryResult(
            ProviderResultCategory.Success,
            "250 OK: Mock message accepted",
            Guid.NewGuid().ToString("N")));
    }
}

public sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task<ProviderDeliveryResult> SendSmsAsync(string toPhone, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("SMS_SENT: to={Phone}", toPhone);
        return Task.FromResult(new ProviderDeliveryResult(
            ProviderResultCategory.Success,
            "200 OK: Mock SMS sent",
            Guid.NewGuid().ToString("N")));
    }
}

public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task SendNotificationHintAsync(Guid userId, Guid notificationId, string notificationType, CancellationToken ct = default) =>
        Task.CompletedTask;
}

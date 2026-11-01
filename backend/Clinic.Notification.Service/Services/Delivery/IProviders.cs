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

/// <summary>
/// MOCK provider (course/demo scope): records the attempt but sends nothing to a real mailbox.
/// Attempt audit is labelled MOCK so it is never mistaken for real delivery.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task<ProviderDeliveryResult> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("MOCK_EMAIL: not sent to a real mailbox. to={Email}, subject={Subject}", Mask(toEmail), subject);
        return Task.FromResult(new ProviderDeliveryResult(
            ProviderResultCategory.Success,
            "MOCK: email accepted by mock provider (not sent to a real recipient)",
            "mock-" + Guid.NewGuid().ToString("N")));
    }

    internal static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value)) return "***";
        var at = value.IndexOf('@');
        return at > 1 ? value[0] + "***" + value[at..] : value.Length > 4 ? "***" + value[^2..] : "***";
    }
}

/// <summary>MOCK provider (course/demo scope): no real SMS is sent.</summary>
public sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task<ProviderDeliveryResult> SendSmsAsync(string toPhone, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("MOCK_SMS: not sent to a real phone. to={Phone}", LoggingEmailSender.Mask(toPhone));
        return Task.FromResult(new ProviderDeliveryResult(
            ProviderResultCategory.Success,
            "MOCK: sms accepted by mock provider (not sent to a real recipient)",
            "mock-" + Guid.NewGuid().ToString("N")));
    }
}


public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task SendNotificationHintAsync(Guid userId, Guid notificationId, string notificationType, CancellationToken ct = default) =>
        Task.CompletedTask;
}


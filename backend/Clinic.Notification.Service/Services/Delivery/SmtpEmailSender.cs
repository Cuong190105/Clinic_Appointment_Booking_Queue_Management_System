using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Clinic.Notification.Service.Services.Delivery;

public sealed class SmtpEmailOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpEmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProviderDeliveryResult> SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogError("SMTP email delivery is not configured.");
            return new ProviderDeliveryResult(
                ProviderResultCategory.PermanentFailure,
                "SMTP email provider is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _options.FromName ?? _options.Username,
            _options.FromAddress ?? _options.Username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                ct);
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "SMTP email delivered to masked recipient {Recipient}.",
                LoggingEmailSender.Mask(toEmail));

            return new ProviderDeliveryResult(
                ProviderResultCategory.Success,
                "SMTP email accepted by configured provider.",
                message.MessageId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SmtpCommandException ex) when ((int)ex.StatusCode >= 500)
        {
            _logger.LogError(
                "SMTP permanently rejected email to masked recipient {Recipient}; status={StatusCode}.",
                LoggingEmailSender.Mask(toEmail),
                ex.StatusCode);
            return new ProviderDeliveryResult(
                ProviderResultCategory.PermanentFailure,
                "SMTP provider permanently rejected the message.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SMTP email delivery failed for masked recipient {Recipient}.",
                LoggingEmailSender.Mask(toEmail));
            return new ProviderDeliveryResult(
                ProviderResultCategory.TransientFailure,
                "SMTP provider delivery failed.");
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }
}

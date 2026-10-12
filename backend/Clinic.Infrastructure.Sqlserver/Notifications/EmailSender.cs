using Clinic.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Clinic.Infrastructure.Sqlserver.Notifications
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        public EmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }
        public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    "Clinic",
                    _options.Username));

            message.To.Add(
                MailboxAddress.Parse(to));

            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = body
            };

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(
                    _options.Host,
                    _options.Port,
                    SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);

                await smtp.SendAsync(message, cancellationToken);
                await smtp.DisconnectAsync(
                    true,
                    cancellationToken);
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    public class EmailOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

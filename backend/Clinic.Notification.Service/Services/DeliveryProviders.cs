using System.Net;
using System.Net.Mail;
using Clinic.Notification.Service.Entities;
using Clinic.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Clinic.Notification.Service.Services;

public sealed record ProviderResult(bool Success, string Code, bool Transient);
public interface IDeliveryProvider { string Channel { get; } Task<ProviderResult> SendAsync(NotificationMessage message, CancellationToken ct); }

public sealed class InAppProvider(IHubContext<NotificationHub> hub) : IDeliveryProvider
{
    public string Channel => "in-app";
    public async Task<ProviderResult> SendAsync(NotificationMessage m, CancellationToken ct)
    { await hub.Clients.User(m.RecipientId.ToString()).SendAsync("NotificationReceived", new { id = m.Id, m.Title, content = m.Body, m.CreatedAt }, ct); return new(true, "signalr.accepted", false); }
}

public sealed class EmailProvider(IConfiguration config) : IDeliveryProvider
{
    public string Channel => "email";
    public async Task<ProviderResult> SendAsync(NotificationMessage m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(m.Email)) return new(false, "recipient.missing", false);
        var host = config["Email:Host"]; if (string.IsNullOrWhiteSpace(host)) return new(false, "provider.unconfigured", false);
        try
        {
            using var client = new SmtpClient(host, config.GetValue("Email:Port", 587)) { EnableSsl = config.GetValue("Email:EnableSsl", true) };
            var user = config["Email:Username"]; if (!string.IsNullOrWhiteSpace(user)) client.Credentials = new NetworkCredential(user, config["Email:Password"]);
            using var mail = new MailMessage(config["Email:From"] ?? user ?? "no-reply@clinic.local", m.Email, m.Title, m.Body);
            await client.SendMailAsync(mail, ct); return new(true, "smtp.accepted", false);
        }
        catch (SmtpException ex) { return new(false, $"smtp.{(int)ex.StatusCode}", true); }
    }
}

public sealed class SmsProvider(IHttpClientFactory clients, IConfiguration config) : IDeliveryProvider
{
    public string Channel => "sms";
    public async Task<ProviderResult> SendAsync(NotificationMessage m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(m.PhoneNumber)) return new(false, "recipient.missing", false);
        var endpoint = config["Sms:Endpoint"]; if (string.IsNullOrWhiteSpace(endpoint)) return new(false, "provider.unconfigured", false);
        try { using var response = await clients.CreateClient("sms").PostAsJsonAsync(endpoint, new { to = m.PhoneNumber, message = m.Body }, ct); return new(response.IsSuccessStatusCode, $"http.{(int)response.StatusCode}", (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(false, "provider.timeout", true); }
        catch (HttpRequestException) { return new(false, "provider.unavailable", true); }
    }
}

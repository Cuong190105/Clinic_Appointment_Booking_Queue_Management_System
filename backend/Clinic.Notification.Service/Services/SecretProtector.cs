using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Notification.Service.Services;

public static class NotificationDataProtectionRegistration
{
    public static IDataProtectionBuilder AddNotificationDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("Clinic.Notification.Service");

        var keyDirectory = configuration["DataProtection:KeyDirectory"];
        if (!string.IsNullOrWhiteSpace(keyDirectory))
        {
            Directory.CreateDirectory(keyDirectory);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        }

        return dataProtection;
    }
}

/// <summary>
/// Protects short-lived secrets (e.g. password-reset codes) at rest in the delivery outbox.
/// Keys live outside the database (ASP.NET Data Protection key ring), so a DB read alone never reveals the secret.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Returns null when the value is not protected by this service or the key is unavailable.</summary>
    string? Unprotect(string stored);

    bool IsProtected(string stored);
}

public sealed class DataProtectionSecretProtector : ISecretProtector
{
    public const string Prefix = "dp1:";
    public const string Redacted = "[REDACTED]";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Clinic.Notification.PasswordReset.v1");
    }

    public string Protect(string plaintext) => Prefix + _protector.Protect(plaintext);

    public bool IsProtected(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

    public string? Unprotect(string stored)
    {
        if (!IsProtected(stored)) return null;
        try
        {
            return _protector.Unprotect(stored[Prefix.Length..]);
        }
        catch
        {
            return null;
        }
    }
}

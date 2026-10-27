using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Notification.Service.Tests;

internal sealed class TestServiceProvider : IServiceProvider, IServiceScope, IServiceScopeFactory
{
    private readonly NotificationDbContext _db;
    public TestServiceProvider(NotificationDbContext db) => _db = db;
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(NotificationDbContext)) return _db;
        if (serviceType == typeof(IServiceScopeFactory)) return this;
        return null;
    }
    public IServiceScope CreateScope() => this;
    public IServiceProvider ServiceProvider => this;
    public void Dispose() { }
}

internal sealed class ConfigurableEmailSender : IEmailSender
{
    public Func<string, string, string, CancellationToken, Task<ProviderDeliveryResult>> OnSend { get; set; } =
        (_, _, _, _) => Task.FromResult(new ProviderDeliveryResult(ProviderResultCategory.Success, "OK"));

    public Task<ProviderDeliveryResult> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default) =>
        OnSend(toEmail, subject, body, ct);
}

internal static class TestDbHelper
{
    public static NotificationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new NotificationDbContext(options);
    }
}


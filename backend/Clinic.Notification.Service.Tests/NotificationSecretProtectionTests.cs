using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Services;
using Clinic.Notification.Service.Services.Delivery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clinic.Notification.Service.Tests;

public sealed class NotificationSecretProtectionTests
{
    [Fact]
    public void DataProtectionSecret_RemainsReadableAfterServiceProviderRecreation()
    {
        var keyDirectory = Path.Combine(Path.GetTempPath(), "clinic-notification-keys", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyDirectory"] = keyDirectory
            })
            .Build();

        try
        {
            string ciphertext;
            using (var firstProvider = CreateProtectionProvider(configuration))
            {
                var protector = new DataProtectionSecretProtector(firstProvider.GetRequiredService<IDataProtectionProvider>());
                ciphertext = protector.Protect("reset-secret-after-restart");
            }

            using var recreatedProvider = CreateProtectionProvider(configuration);
            var recreatedProtector = new DataProtectionSecretProtector(recreatedProvider.GetRequiredService<IDataProtectionProvider>());

            Assert.Equal("reset-secret-after-restart", recreatedProtector.Unprotect(ciphertext));
            Assert.Contains(Directory.EnumerateFiles(keyDirectory), path => path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(keyDirectory))
            {
                Directory.Delete(keyDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PasswordResetProviderException_DoesNotWriteExceptionMessageToLogs()
    {
        await using var factory = new NotificationTestFactory();
        var secret = "reset-secret-in-provider-exception";
        Guid deliveryId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Clinic.Notification.Service.Persistence.NotificationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
            var delivery = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                RecipientId = Guid.NewGuid(),
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.Email,
                NotificationType = NotificationType.PasswordReset,
                IdempotencyKey = $"reset-test:{Guid.NewGuid():N}",
                RecipientAddress = "patient@example.test",
                Content = protector.Protect($"Reset code: {secret}"),
                Status = DeliveryStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                MaxAttempts = 3
            };
            deliveryId = delivery.Id;
            db.DeliveryOutboxMessages.Add(delivery);
            await db.SaveChangesAsync();
        }

        var logger = new CapturingLogger<NotificationDeliveryDispatcher>();
        var emailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => throw new InvalidOperationException($"Provider request failed: {secret}")
        };
        var dispatcher = new NotificationDeliveryDispatcher(
            factory.Services,
            emailSender,
            new LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingSmsSender>.Instance),
            new NullRealtimeNotifier(),
            logger);

        var status = await dispatcher.ProcessDeliveryAsync(deliveryId);

        Assert.Equal(DeliveryStatus.Pending, status);
        Assert.DoesNotContain(secret, string.Join(Environment.NewLine, logger.Messages), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, string.Join(Environment.NewLine, logger.Exceptions), StringComparison.Ordinal);
    }

    private static ServiceProvider CreateProtectionProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationDataProtection(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<string> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception != null) Exceptions.Add(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

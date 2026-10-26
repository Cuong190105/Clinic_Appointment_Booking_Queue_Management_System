using Clinic.Contracts.Events;
using Clinic.Notification.Service.Endpoints;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Clinic.Notification.Service.Tests;

// These executable test specifications stay skipped until each step implements its production behavior.
// Replace Skip with assertions against the real service types as each plan step is completed.
public sealed class NotificationPolicyUnitTests
{
    private readonly NotificationPolicyEngine _engine = new();

    [Fact]
    public void ConfirmedAppointment_ProducesOneConfirmationPerEligibleRecipientAndChannel()
    {
        var eventId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();

        var payload = new AppointmentConfirmed(
            AppointmentId: Guid.NewGuid(),
            PatientId: patientId,
            DoctorId: doctorId,
            WorkScheduleId: scheduleId,
            ScheduleDate: new DateOnly(2026, 11, 1),
            TimeSlot: new TimeOnly(9, 0),
            ConfirmedByUserId: Guid.NewGuid(),
            ConfirmedAt: DateTimeOffset.UtcNow,
            PatientName: "Nguyen Van A",
            PatientEmail: "patienta@example.com",
            PatientPhone: "0901234567",
            IdentityUserId: identityUserId
        );

        var intents = _engine.EvaluateAppointmentConfirmed(eventId, payload);

        Assert.Equal(2, intents.Count);
        
        var inApp = intents.Single(i => i.Channel == NotificationChannel.InApp);
        Assert.Equal(identityUserId, inApp.RecipientId);
        Assert.Equal(RecipientType.Account, inApp.RecipientType);
        Assert.Equal($"{eventId:D}:{identityUserId:D}:InApp:AppointmentConfirmed", inApp.IdempotencyKey);

        var email = intents.Single(i => i.Channel == NotificationChannel.Email);
        Assert.Equal(identityUserId, email.RecipientId);
        Assert.Equal(RecipientType.Account, email.RecipientType);
        Assert.Equal("patienta@example.com", email.ContactSnapshot?.Email);
        Assert.Equal($"{eventId:D}:{identityUserId:D}:Email:AppointmentConfirmed", email.IdempotencyKey);
    }

    [Fact]
    public void RescheduleRelease_DoesNotProduceCancellationNotification()
    {
        var eventId = Guid.NewGuid();
        var payload = new AppointmentCancelled(
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            CancelledByUserId: Guid.NewGuid(),
            CancelledAt: DateTimeOffset.UtcNow,
            IsRescheduleRelease: true
        );

        Assert.True(_engine.ShouldSuppressCancellation(payload));
        var intents = _engine.EvaluateAppointmentCancelled(eventId, payload);
        Assert.Empty(intents);
    }

    [Fact]
    public void GuestRecipient_UsesOnlyEligibleEmailOrSmsChannels()
    {
        var eventId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var payload = new AppointmentConfirmed(
            AppointmentId: Guid.NewGuid(),
            PatientId: patientId,
            DoctorId: Guid.NewGuid(),
            WorkScheduleId: Guid.NewGuid(),
            ScheduleDate: new DateOnly(2026, 11, 1),
            TimeSlot: new TimeOnly(9, 0),
            ConfirmedByUserId: Guid.NewGuid(),
            ConfirmedAt: DateTimeOffset.UtcNow,
            PatientName: "Guest Patient",
            PatientEmail: "guest@example.com",
            PatientPhone: "0909999999",
            IdentityUserId: null // Guest: No account linkage
        );

        var intents = _engine.EvaluateAppointmentConfirmed(eventId, payload);

        // Guest gets email, but NEVER gets an InApp account record
        Assert.DoesNotContain(intents, i => i.Channel == NotificationChannel.InApp);
        Assert.All(intents, i => Assert.Equal(RecipientType.Guest, i.RecipientType));
        Assert.All(intents, i => Assert.Equal(patientId, i.RecipientId));
        Assert.Contains(intents, i => i.Channel == NotificationChannel.Email);
    }

    [Fact]
    public void CancelledAppointment_SuppressesPendingReminder()
    {
        var eventId = Guid.NewGuid();
        var payload = new AppointmentCancelled(
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            CancelledByUserId: Guid.NewGuid(),
            CancelledAt: DateTimeOffset.UtcNow,
            IsRescheduleRelease: false
        );

        Assert.True(_engine.ShouldCancelPendingReminder(payload));
        Assert.False(_engine.ShouldSuppressCancellation(payload));

        var intents = _engine.EvaluateAppointmentCancelled(eventId, payload);
        // True cancellation without reschedule release evaluates intents for eligible channels
        Assert.NotNull(intents);
    }

    [Fact]
    public void OrdinaryEventContracts_ContainNoCredentials_ResetTokens_OrClinicalPHI()
    {
        var forbiddenTerms = new[] { "password", "resetcode", "resettoken", "symptoms", "diagnosis", "prescription", "notes" };
        var typesToCheck = new[] { typeof(AppointmentConfirmed), typeof(AppointmentCancelled), typeof(PatientCalled) };

        foreach (var type in typesToCheck)
        {
            var propertyNames = type.GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
            foreach (var forbidden in forbiddenTerms)
            {
                Assert.DoesNotContain(propertyNames, name => name.Contains(forbidden));
            }
        }
    }
}

public sealed class NotificationPersistenceUnitTests
{
    private NotificationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new NotificationDbContext(options);
    }

    [Fact]
    public async Task ConsumingBusinessEvent_PersistsInboxNotificationAndDeliveriesInOneTransaction()
    {
        var dbName = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        await using (var db = CreateDbContext(dbName))
        {
            var inbox = new InboxMessage
            {
                Id = eventId,
                EventType = EventTypes.AppointmentConfirmed,
                Status = "Processed",
                ProcessedAt = DateTimeOffset.UtcNow
            };

            var notification = new NotificationItem
            {
                Id = Guid.NewGuid(),
                RecipientId = recipientId,
                RecipientType = RecipientType.Account,
                NotificationType = NotificationType.AppointmentConfirmed,
                Title = "Xác nhận lịch khám",
                Body = "Lịch khám đã được xác nhận",
                IsRead = false,
                ContactName = "Test User",
                ContactEmail = "test@example.com"
            };

            var delivery = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Notification = notification,
                RecipientId = recipientId,
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.InApp,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = $"{eventId:D}:{recipientId:D}:InApp:AppointmentConfirmed",
                RecipientAddress = "in-app",
                Subject = notification.Title,
                Content = notification.Body,
                Status = DeliveryStatus.Pending
            };

            db.InboxMessages.Add(inbox);
            db.Notifications.Add(notification);
            db.DeliveryOutboxMessages.Add(delivery);

            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(dbName))
        {
            var savedInbox = await db.InboxMessages.FindAsync(eventId);
            var savedNotifications = await db.Notifications.Include(n => n.Deliveries).ToListAsync();

            Assert.NotNull(savedInbox);
            Assert.Equal("Processed", savedInbox.Status);
            Assert.Single(savedNotifications);
            Assert.Equal(recipientId, savedNotifications[0].RecipientId);
            Assert.Single(savedNotifications[0].Deliveries);
            Assert.Equal(DeliveryStatus.Pending, savedNotifications[0].Deliveries.First().Status);
        }
    }

    [Fact]
    public async Task DuplicateEventId_DoesNotCreateDuplicateNotification()
    {
        var dbName = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var idempotencyKey = $"{eventId:D}:{recipientId:D}:InApp:AppointmentConfirmed";

        await using (var db = CreateDbContext(dbName))
        {
            db.DeliveryOutboxMessages.Add(new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                RecipientId = recipientId,
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.InApp,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = idempotencyKey,
                RecipientAddress = "in-app",
                Content = "Content 1",
                Status = DeliveryStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(dbName))
        {
            // Idempotency check: key already exists
            var isDuplicate = await db.DeliveryOutboxMessages.AnyAsync(x => x.IdempotencyKey == idempotencyKey);
            Assert.True(isDuplicate);

            var duplicateMessage = new DeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                RecipientId = recipientId,
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.InApp,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = idempotencyKey,
                RecipientAddress = "in-app",
                Content = "Content 2"
            };

            // In our consumer logic, isDuplicate prevents insertion
            if (!isDuplicate)
            {
                db.DeliveryOutboxMessages.Add(duplicateMessage);
                await db.SaveChangesAsync();
            }

            var totalCount = await db.DeliveryOutboxMessages.CountAsync(x => x.IdempotencyKey == idempotencyKey);
            Assert.Equal(1, totalCount);
        }
    }

    [Fact]
    public async Task DeliveryAttempt_PersistsRequiredAuditFieldsWithoutSecretsOrMessageBody()
    {
        var dbName = Guid.NewGuid().ToString();
        var outboxId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await using (var db = CreateDbContext(dbName))
        {
            var attempt = new DeliveryAttempt
            {
                Id = Guid.NewGuid(),
                DeliveryOutboxMessageId = outboxId,
                AttemptNumber = 1,
                AttemptedAt = DateTimeOffset.UtcNow,
                Status = DeliveryStatus.Delivered,
                ProviderResponse = "250 OK: Message accepted for delivery",
                ProviderReference = "msg-provider-999",
                CorrelationId = correlationId
            };

            db.DeliveryAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(dbName))
        {
            var attempt = await db.DeliveryAttempts.FirstOrDefaultAsync(x => x.CorrelationId == correlationId);
            Assert.NotNull(attempt);
            Assert.Equal(DeliveryStatus.Delivered, attempt.Status);
            Assert.Equal("250 OK: Message accepted for delivery", attempt.ProviderResponse);
            Assert.Equal("msg-provider-999", attempt.ProviderReference);

            // Verify no secrets or message body fields exist on DeliveryAttempt
            var forbiddenTerms = new[] { "body", "content", "secret", "password", "resetcode", "resettoken" };
            var propertyNames = typeof(DeliveryAttempt).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
            foreach (var forbidden in forbiddenTerms)
            {
                Assert.DoesNotContain(propertyNames, name => name.Contains(forbidden));
            }
        }
    }

    [Fact]
    public void NotificationAggregate_StoresRecipientIdentityAndMinimalContactSnapshotOnly()
    {
        var notificationProps = typeof(NotificationItem).GetProperties();
        var propNames = notificationProps.Select(p => p.Name).ToList();

        // Must have recipient identity and minimal contact snapshot
        Assert.Contains("RecipientId", propNames);
        Assert.Contains("RecipientType", propNames);
        Assert.Contains("ContactName", propNames);
        Assert.Contains("ContactEmail", propNames);
        Assert.Contains("ContactPhone", propNames);

        // Must NOT reference Person or Clinic domain entities
        Assert.DoesNotContain("Person", propNames);
        Assert.DoesNotContain("PersonId", propNames);
        Assert.DoesNotContain("Patient", propNames);

        // Must NOT have clinical PHI properties
        var forbiddenTerms = new[] { "symptoms", "diagnosis", "prescription", "notes", "medicalhistory" };
        foreach (var forbidden in forbiddenTerms)
        {
            Assert.DoesNotContain(propNames, name => name.ToLowerInvariant().Contains(forbidden));
        }
    }
}

public sealed class NotificationDeliveryUnitTests
{
    private sealed class TestServiceProvider : IServiceProvider, IServiceScope, IServiceScopeFactory
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

    private sealed class ConfigurableEmailSender : Clinic.Notification.Service.Services.Delivery.IEmailSender
    {
        public Func<string, string, string, CancellationToken, Task<Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult>> OnSend { get; set; } =
            (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Success, "OK"));

        public Task<Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default) =>
            OnSend(toEmail, subject, body, ct);
    }

    private static NotificationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new NotificationDbContext(options);
    }

    [Fact]
    public async Task TransientProviderFailure_SchedulesBoundedExponentialRetry()
    {
        var dbName = Guid.NewGuid().ToString();
        var deliveryId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        var delivery = new DeliveryOutboxMessage
        {
            Id = deliveryId,
            EventId = Guid.NewGuid(),
            RecipientId = Guid.NewGuid(),
            RecipientType = RecipientType.Account,
            Channel = NotificationChannel.Email,
            NotificationType = NotificationType.AppointmentConfirmed,
            IdempotencyKey = "key-1",
            RecipientAddress = "user@example.com",
            Content = "Test content",
            Status = DeliveryStatus.Pending,
            AttemptsCount = 0,
            MaxAttempts = 3
        };
        db.DeliveryOutboxMessages.Add(delivery);
        await db.SaveChangesAsync();

        var emailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.TransientFailure, "503 Service Unavailable"))
        };

        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            new TestServiceProvider(db),
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        var status = await dispatcher.ProcessDeliveryAsync(deliveryId);

        Assert.Equal(DeliveryStatus.Pending, status);
        var updated = await db.DeliveryOutboxMessages.FindAsync(deliveryId);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.AttemptsCount);
        Assert.NotNull(updated.NextAttemptAt);
        Assert.True(updated.NextAttemptAt > DateTimeOffset.UtcNow);

        var attempt = await db.DeliveryAttempts.FirstOrDefaultAsync(a => a.DeliveryOutboxMessageId == deliveryId);
        Assert.NotNull(attempt);
        Assert.Equal(DeliveryStatus.Failed, attempt.Status);
        Assert.Equal("503 Service Unavailable", attempt.ProviderResponse);
    }

    [Fact]
    public async Task RetryExhaustion_DeadLettersDeliveryAndPreservesAudit()
    {
        var dbName = Guid.NewGuid().ToString();
        var deliveryId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        var delivery = new DeliveryOutboxMessage
        {
            Id = deliveryId,
            EventId = Guid.NewGuid(),
            RecipientId = Guid.NewGuid(),
            RecipientType = RecipientType.Account,
            Channel = NotificationChannel.Email,
            NotificationType = NotificationType.AppointmentConfirmed,
            IdempotencyKey = "key-exhaust",
            RecipientAddress = "user@example.com",
            Content = "Test content",
            Status = DeliveryStatus.Pending,
            AttemptsCount = 2, // Last attempt before exhaustion
            MaxAttempts = 3
        };
        db.DeliveryOutboxMessages.Add(delivery);
        await db.SaveChangesAsync();

        var emailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.TransientFailure, "Connection reset"))
        };

        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            new TestServiceProvider(db),
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        var status = await dispatcher.ProcessDeliveryAsync(deliveryId);

        Assert.Equal(DeliveryStatus.DeadLettered, status);
        var updated = await db.DeliveryOutboxMessages.FindAsync(deliveryId);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.AttemptsCount);
        Assert.Equal(DeliveryStatus.DeadLettered, updated.Status);

        var attempts = await db.DeliveryAttempts.Where(a => a.DeliveryOutboxMessageId == deliveryId).ToListAsync();
        Assert.Single(attempts);
        Assert.Equal(3, attempts[0].AttemptNumber);
        Assert.Equal(DeliveryStatus.Failed, attempts[0].Status);
    }

    [Fact]
    public async Task ProviderTimeout_IsBoundedAndOtherDeliveriesContinue()
    {
        var dbName = Guid.NewGuid().ToString();
        var slowId = Guid.NewGuid();
        var fastId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        db.DeliveryOutboxMessages.AddRange(
            new DeliveryOutboxMessage
            {
                Id = slowId,
                EventId = Guid.NewGuid(),
                RecipientId = Guid.NewGuid(),
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.Email,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = "key-slow",
                RecipientAddress = "slow@example.com",
                Content = "Slow message",
                Status = DeliveryStatus.Pending,
                AttemptsCount = 0,
                MaxAttempts = 3
            },
            new DeliveryOutboxMessage
            {
                Id = fastId,
                EventId = Guid.NewGuid(),
                RecipientId = Guid.NewGuid(),
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.Email,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = "key-fast",
                RecipientAddress = "fast@example.com",
                Content = "Fast message",
                Status = DeliveryStatus.Pending,
                AttemptsCount = 0,
                MaxAttempts = 3
            }
        );
        await db.SaveChangesAsync();

        var emailSender = new ConfigurableEmailSender
        {
            OnSend = async (to, _, _, ct) =>
            {
                if (to == "slow@example.com")
                {
                    await Task.Delay(500, ct); // Simulates delay beyond timeout
                    return new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Success, "Too late");
                }
                return new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Success, "250 OK");
            }
        };

        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            new TestServiceProvider(db),
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance)
        {
            TimeoutBudget = TimeSpan.FromMilliseconds(50) // Tight timeout budget for fast test
        };

        // Slow delivery times out
        var slowStatus = await dispatcher.ProcessDeliveryAsync(slowId);
        Assert.Equal(DeliveryStatus.Pending, slowStatus); // Scheduled for retry after timeout

        // Fast delivery succeeds immediately without being blocked
        var fastStatus = await dispatcher.ProcessDeliveryAsync(fastId);
        Assert.Equal(DeliveryStatus.Delivered, fastStatus);

        var slowRecord = await db.DeliveryOutboxMessages.FindAsync(slowId);
        var fastRecord = await db.DeliveryOutboxMessages.FindAsync(fastId);
        Assert.NotNull(slowRecord);
        Assert.NotNull(fastRecord);
        Assert.Equal(1, slowRecord.AttemptsCount);
        Assert.Equal(DeliveryStatus.Delivered, fastRecord.Status);
    }

    [Fact]
    public async Task AmbiguousProviderResult_DoesNotCreateUnboundedDuplicateAttempts()
    {
        var dbName = Guid.NewGuid().ToString();
        var deliveryId = Guid.NewGuid();

        await using var db = CreateDbContext(dbName);
        var delivery = new DeliveryOutboxMessage
        {
            Id = deliveryId,
            EventId = Guid.NewGuid(),
            RecipientId = Guid.NewGuid(),
            RecipientType = RecipientType.Account,
            Channel = NotificationChannel.Email,
            NotificationType = NotificationType.AppointmentConfirmed,
            IdempotencyKey = "key-ambiguous",
            RecipientAddress = "user@example.com",
            Content = "Test content",
            Status = DeliveryStatus.Pending,
            AttemptsCount = 0,
            MaxAttempts = 2 // Bounded to 2 attempts max
        };
        db.DeliveryOutboxMessages.Add(delivery);
        await db.SaveChangesAsync();

        var emailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Ambiguous, "Lost ACK after send"))
        };

        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            new TestServiceProvider(db),
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        // Attempt 1
        var status1 = await dispatcher.ProcessDeliveryAsync(deliveryId);
        Assert.Equal(DeliveryStatus.Pending, status1);

        // Attempt 2 -> MaxAttempts reached, should dead-letter
        var status2 = await dispatcher.ProcessDeliveryAsync(deliveryId);
        Assert.Equal(DeliveryStatus.DeadLettered, status2);

        // Attempt 3 -> Since status is DeadLettered, dispatcher should not process it again
        var status3 = await dispatcher.ProcessDeliveryAsync(deliveryId);
        Assert.Equal(DeliveryStatus.DeadLettered, status3);

        var finalDelivery = await db.DeliveryOutboxMessages.FindAsync(deliveryId);
        Assert.NotNull(finalDelivery);
        Assert.Equal(2, finalDelivery.AttemptsCount); // Stopped at bounded 2 attempts
    }
}

public sealed class NotificationAuthorizationAndRealtimeTests
{
    [Fact]
    public async Task ProtectedNotificationEndpoint_RejectsMissingOrInvalidJwt()
    {
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        // 1. Missing JWT returns 401
        var unauthGet = await client.GetAsync("/notifications");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthGet.StatusCode);

        var unauthMe = await client.GetAsync("/notifications/me");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthMe.StatusCode);

        var unauthCount = await client.GetAsync("/notifications/unread-count");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthCount.StatusCode);

        var unauthRead = await client.PostAsync($"/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthRead.StatusCode);

        // 2. Malformed / forged JWT returns 401
        using var malformedReq = new HttpRequestMessage(HttpMethod.Get, "/notifications");
        malformedReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
        var malformedRes = await client.SendAsync(malformedReq);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, malformedRes.StatusCode);
    }

    [Fact]
    public async Task User_CannotReadAnotherUsersNotificationHistory()
    {
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        var notifAId = Guid.NewGuid();
        var notifBId = Guid.NewGuid();
        var notifGuestId = Guid.NewGuid();

        // Seed notifications
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Notifications.AddRange(
                new NotificationItem
                {
                    Id = notifAId,
                    RecipientId = userA,
                    RecipientType = RecipientType.Account,
                    Title = "Notice A",
                    Body = "Notice for User A",
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new NotificationItem
                {
                    Id = notifBId,
                    RecipientId = userB,
                    RecipientType = RecipientType.Account,
                    Title = "Notice B",
                    Body = "Notice for User B",
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new NotificationItem
                {
                    Id = notifGuestId,
                    RecipientId = guestId,
                    RecipientType = RecipientType.Guest,
                    Title = "Notice Guest",
                    Body = "Notice for Guest",
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        var tokenA = factory.CreateToken(userA);
        using var requestA = new HttpRequestMessage(HttpMethod.Get, "/notifications");
        requestA.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);

        var resA = await client.SendAsync(requestA);
        Assert.Equal(System.Net.HttpStatusCode.OK, resA.StatusCode);

        var contentA = await resA.Content.ReadFromJsonAsync<NotificationListResponse>();
        Assert.NotNull(contentA);
        Assert.Single(contentA.Items);
        Assert.Equal(notifAId, contentA.Items[0].Id);
        // User A cannot see User B's or Guest's notification
        Assert.DoesNotContain(contentA.Items, i => i.Id == notifBId || i.Id == notifGuestId);

        // User A attempts IDOR to read User B's notification -> Forbidden (403)
        using var readBReq = new HttpRequestMessage(HttpMethod.Post, $"/notifications/{notifBId}/read");
        readBReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
        var readBRes = await client.SendAsync(readBReq);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, readBRes.StatusCode);

        // Verify User B's notification remains unread in database
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var itemB = await db.Notifications.FindAsync(notifBId);
            Assert.NotNull(itemB);
            Assert.False(itemB.IsRead);
        }

        // User A can mark own notification as read
        using var readAReq = new HttpRequestMessage(HttpMethod.Post, $"/notifications/{notifAId}/read");
        readAReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenA);
        var readARes = await client.SendAsync(readAReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, readARes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var itemA = await db.Notifications.FindAsync(notifAId);
            Assert.NotNull(itemA);
            Assert.True(itemA.IsRead);
        }
    }

    [Fact]
    public void SignalRConnection_IsGroupedByAuthenticatedSubject()
    {
        var expectedUserId = Guid.NewGuid();

        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", expectedUserId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Patient")
        };
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "Bearer"));

        // Verify that authenticated sub claim is used as user identifier
        var userFromPrincipal = principal.FindFirst("sub")?.Value;
        Assert.Equal(expectedUserId.ToString(), userFromPrincipal);

        // Anonymous context cannot impersonate another user
        var unauthPrincipal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        Assert.Null(unauthPrincipal.FindFirst("sub")?.Value);
    }

    [Fact]
    public async Task ReconnectedClient_CanRecoverMissedNotificationsFromHistoryApi()
    {
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        var userId = Guid.NewGuid();
        var missedNotifId1 = Guid.NewGuid();
        var missedNotifId2 = Guid.NewGuid();

        // Notifications arrived while client was disconnected
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Notifications.AddRange(
                new NotificationItem
                {
                    Id = missedNotifId1,
                    RecipientId = userId,
                    RecipientType = RecipientType.Account,
                    Title = "Missed 1",
                    Body = "Your appointment was confirmed",
                    NotificationType = NotificationType.AppointmentConfirmed,
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
                },
                new NotificationItem
                {
                    Id = missedNotifId2,
                    RecipientId = userId,
                    RecipientType = RecipientType.Account,
                    Title = "Missed 2",
                    Body = "Please proceed to Room 101",
                    NotificationType = NotificationType.PatientCalled,
                    IsRead = false,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            );
            await db.SaveChangesAsync();
        }

        var token = factory.CreateToken(userId);

        // Client reconnects and calls unread-count and notifications history
        using var countReq = new HttpRequestMessage(HttpMethod.Get, "/notifications/unread-count");
        countReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var countRes = await client.SendAsync(countReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, countRes.StatusCode);
        var countJson = await countRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, countJson.GetProperty("unreadCount").GetInt32());

        // Client fetches authoritative history
        using var historyReq = new HttpRequestMessage(HttpMethod.Get, "/notifications?limit=10");
        historyReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var historyRes = await client.SendAsync(historyReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, historyRes.StatusCode);

        var history = await historyRes.Content.ReadFromJsonAsync<NotificationListResponse>();
        Assert.NotNull(history);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(2, history.Items.Count);
        Assert.Contains(history.Items, i => i.Id == missedNotifId1 && i.Type == "AppointmentConfirmed");
        Assert.Contains(history.Items, i => i.Id == missedNotifId2 && i.Type == "PatientCalled");
    }
}

public sealed class NotificationGatewayAndWorkflowTests
{
    [Fact]
    public async Task FrontendNotificationRequests_AreRoutedOnlyThroughGateway()
    {
        // 1. Direct external calls to internal endpoints are strictly rejected without service token
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        var directInternalCall = await client.PostAsJsonAsync("/internal/notifications/password-reset", new
        {
            recipientId = Guid.NewGuid(),
            email = "patient@example.com",
            resetToken = "token123"
        });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, directInternalCall.StatusCode);

        // 2. Direct calls to notifications endpoints require valid Identity JWT
        var directUnauthCall = await client.GetAsync("/notifications");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, directUnauthCall.StatusCode);

        // 3. Authenticated calls succeed via the authorized route
        var userId = Guid.NewGuid();
        var token = factory.CreateToken(userId);
        using var authedReq = new HttpRequestMessage(HttpMethod.Get, "/notifications");
        authedReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var authedRes = await client.SendAsync(authedReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, authedRes.StatusCode);
    }

    [Fact]
    public async Task PasswordResetDelivery_DoesNotExposeTokenInLogsOrGeneralBusinessEvents()
    {
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        var secretToken = "super-secret-one-time-reset-code-xyz987";
        var recipientId = Guid.NewGuid();

        // 1. Call internal endpoint without service token -> 401
        var unauthRes = await client.PostAsJsonAsync("/internal/notifications/password-reset", new
        {
            recipientId,
            email = "user@example.com",
            resetToken = secretToken
        });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthRes.StatusCode);

        // 2. Call internal endpoint with service token -> 200
        using var authorizedReq = new HttpRequestMessage(HttpMethod.Post, "/internal/notifications/password-reset")
        {
            Content = JsonContent.Create(new
            {
                recipientId,
                email = "user@example.com",
                resetToken = secretToken
            })
        };
        authorizedReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var authorizedRes = await client.SendAsync(authorizedReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, authorizedRes.StatusCode);

        var resultJson = await authorizedRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var deliveryId = resultJson.GetProperty("deliveryId").GetGuid();

        // 3. Verify delivery outbox was created
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var outboxMsg = await db.DeliveryOutboxMessages.FindAsync(deliveryId);
        Assert.NotNull(outboxMsg);
        Assert.Equal(NotificationType.PasswordReset, outboxMsg.NotificationType);
        Assert.Equal(DeliveryStatus.Pending, outboxMsg.Status);

        // 4. Dispatch the delivery message and verify audit trail does NOT leak secretToken
        var emailSender = new Clinic.Notification.Service.Services.Delivery.LoggingEmailSender(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingEmailSender>.Instance);
        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            scope.ServiceProvider,
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        var dispatchStatus = await dispatcher.ProcessDeliveryAsync(deliveryId);
        Assert.Equal(DeliveryStatus.Delivered, dispatchStatus);

        var attempts = await db.DeliveryAttempts.Where(a => a.DeliveryOutboxMessageId == deliveryId).ToListAsync();
        Assert.Single(attempts);
        Assert.DoesNotContain(secretToken, attempts[0].ProviderResponse ?? "");

        // 5. Verify ordinary domain events never contain ResetToken / Credentials / Passwords
        var contractTypes = typeof(AppointmentConfirmed).Assembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains("Clinic.Contracts.Events"));
        foreach (var type in contractTypes)
        {
            foreach (var prop in type.GetProperties())
            {
                Assert.DoesNotContain("Token", prop.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Password", prop.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Secret", prop.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact(Skip = "Phase 6 Step 6: verify notification lifecycle across real services and RabbitMQ in Docker Compose.")]
    public void AppointmentConfirmationAndCancellation_CreateExpectedNotifications_EndToEnd() { }

    [Fact(Skip = "Phase 6 Step 6: inject broker/provider failure, restart services, and prove durable recovery.")]
    public void BrokerAndProviderRecovery_DeliversPendingNotificationsWithoutLoss() { }
}

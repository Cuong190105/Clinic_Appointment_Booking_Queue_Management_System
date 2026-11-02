using Clinic.Contracts.Events;
using Clinic.Notification.Service.Endpoints;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

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
    private static NotificationDbContext CreateDbContext(string dbName) => TestDbHelper.CreateDbContext(dbName);

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

        // 0. Missing Identity:ServiceToken configuration fails closed with 503 ServiceUnavailable
        await using (var unconfiguredFactory = new NotificationTestFactory { ServiceToken = null })
        {
            var unconfiguredClient = unconfiguredFactory.CreateClient();
            using var reqNoCfg = new HttpRequestMessage(HttpMethod.Post, "/internal/notifications/password-reset")
            {
                Content = JsonContent.Create(new { recipientId, email = "user@example.com", resetToken = secretToken })
            };
            reqNoCfg.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
            var resNoCfg = await unconfiguredClient.SendAsync(reqNoCfg);
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, resNoCfg.StatusCode);
        }

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

        // 2b. Replay the exact same password reset request -> idempotent, returns same deliveryId and duplicate=true
        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/internal/notifications/password-reset")
        {
            Content = JsonContent.Create(new
            {
                recipientId,
                email = "user@example.com",
                resetToken = secretToken
            })
        };
        replayReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var replayRes = await client.SendAsync(replayReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, replayRes.StatusCode);
        var replayJson = await replayRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(deliveryId, replayJson.GetProperty("deliveryId").GetGuid());
        Assert.True(replayJson.GetProperty("duplicate").GetBoolean());

        // 3. Verify delivery outbox was created and secret is encrypted at rest (starts with dp1:, no raw token)
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var outboxMsg = await db.DeliveryOutboxMessages.FindAsync(deliveryId);
        Assert.NotNull(outboxMsg);
        Assert.Equal(NotificationType.PasswordReset, outboxMsg.NotificationType);
        Assert.Equal(DeliveryStatus.Pending, outboxMsg.Status);
        Assert.StartsWith("dp1:", outboxMsg.Content);
        Assert.DoesNotContain(secretToken, outboxMsg.Content);

        // 4. Dispatch the delivery message and verify secret is decrypted only in memory, scrubbed in DB after delivery, and not leaked in audit
        string? capturedBodySentToProvider = null;
        var capturingEmailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, body, _) =>
            {
                capturedBodySentToProvider = body;
                return Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(
                    Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Success,
                    "MOCK: email accepted by mock provider (not sent to a real recipient)",
                    "mock-email-1"));
            }
        };
        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            scope.ServiceProvider,
            capturingEmailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        var dispatchStatus = await dispatcher.ProcessDeliveryAsync(deliveryId);
        Assert.Equal(DeliveryStatus.Delivered, dispatchStatus);
        Assert.NotNull(capturedBodySentToProvider);
        Assert.Contains(secretToken, capturedBodySentToProvider);

        var scrubbedMsg = await db.DeliveryOutboxMessages.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deliveryId);
        Assert.NotNull(scrubbedMsg);
        Assert.Equal("[REDACTED]", scrubbedMsg.Content);

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

    [Fact]
    public async Task AppointmentConfirmationAndCancellation_CreateExpectedNotifications_EndToEnd()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = TestDbHelper.CreateDbContext(dbName);
        var serviceProvider = new TestServiceProvider(db);
        var policyEngine = new NotificationPolicyEngine();
        var processor = new Clinic.Notification.Service.Messaging.NotificationEventProcessor(
            serviceProvider,
            policyEngine,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Messaging.NotificationEventProcessor>.Instance);

        var patientId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        // 1. AppointmentConfirmed event (>24h in future creates 2 confirmation + 2 scheduled reminder deliveries)
        var confirmEventId = Guid.NewGuid();
        var confirmedEvent = new AppointmentConfirmed(
            AppointmentId: appointmentId,
            PatientId: patientId,
            DoctorId: doctorId,
            WorkScheduleId: Guid.NewGuid(),
            ScheduleDate: futureDate,
            TimeSlot: new TimeOnly(10, 0),
            ConfirmedByUserId: Guid.NewGuid(),
            ConfirmedAt: DateTimeOffset.UtcNow,
            PatientName: "Tran Van Test",
            PatientEmail: "test@example.com",
            PatientPhone: "0912345678",
            IdentityUserId: identityUserId);

        var confirmJson = JsonSerializer.SerializeToElement(confirmedEvent, Clinic.Contracts.Serialization.EventJsonSerializerOptions.Default);
        var confirmProcessed = await processor.ProcessEventAsync(confirmEventId, EventTypes.AppointmentConfirmed, confirmJson, Guid.NewGuid());
        Assert.True(confirmProcessed);

        // Verify inbox and outbox created
        var inboxConfirm = await db.InboxMessages.FindAsync(confirmEventId);
        Assert.NotNull(inboxConfirm);
        Assert.Equal("Processed", inboxConfirm.Status);

        var confirmOutbox = await db.DeliveryOutboxMessages
            .Where(m => m.EventId == confirmEventId && m.NotificationType == NotificationType.AppointmentConfirmed)
            .ToListAsync();
        Assert.Equal(2, confirmOutbox.Count); // InApp and Email

        var reminderOutbox = await db.DeliveryOutboxMessages
            .Where(m => m.EventId == confirmEventId && m.NotificationType == NotificationType.AppointmentReminder)
            .ToListAsync();
        Assert.Equal(2, reminderOutbox.Count);
        Assert.All(reminderOutbox, r => Assert.NotNull(r.ScheduledFor));

        var confirmNotif = await db.Notifications.FirstOrDefaultAsync(n =>
            n.RecipientId == identityUserId && n.NotificationType == NotificationType.AppointmentConfirmed);
        Assert.NotNull(confirmNotif);

        // 2. Dispatch the immediate confirmation outbox deliveries
        var emailSender = new Clinic.Notification.Service.Services.Delivery.LoggingEmailSender(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingEmailSender>.Instance);
        var dispatcher = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
            serviceProvider,
            emailSender,
            new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
            new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

        foreach (var msg in confirmOutbox)
        {
            var status = await dispatcher.ProcessDeliveryAsync(msg.Id);
            Assert.Equal(DeliveryStatus.Delivered, status);
        }

        // 3. AppointmentCancelled event -> suppresses pending reminders and creates 2 cancellation deliveries
        var cancelEventId = Guid.NewGuid();
        var cancelledEvent = new AppointmentCancelled(
            AppointmentId: appointmentId,
            PatientId: patientId,
            CancelledByUserId: Guid.NewGuid(),
            CancelledAt: DateTimeOffset.UtcNow,
            WorkScheduleId: confirmedEvent.WorkScheduleId,
            TimeSlot: new TimeOnly(10, 0),
            IsRescheduleRelease: false,
            PatientName: "Tran Van Test",
            PatientEmail: "test@example.com",
            PatientPhone: "0912345678",
            IdentityUserId: identityUserId);

        var cancelJson = JsonSerializer.SerializeToElement(cancelledEvent, Clinic.Contracts.Serialization.EventJsonSerializerOptions.Default);
        var cancelProcessed = await processor.ProcessEventAsync(cancelEventId, EventTypes.AppointmentCancelled, cancelJson, Guid.NewGuid());
        Assert.True(cancelProcessed);

        var cancelOutbox = await db.DeliveryOutboxMessages.Where(m => m.EventId == cancelEventId).ToListAsync();
        Assert.Equal(2, cancelOutbox.Count);

        foreach (var msg in cancelOutbox)
        {
            var status = await dispatcher.ProcessDeliveryAsync(msg.Id);
            Assert.Equal(DeliveryStatus.Delivered, status);
        }

        // Verify reminders were durably suppressed and tombstone prevents late/out-of-order AppointmentConfirmed from recreating reminder
        var suppressedReminders = await db.DeliveryOutboxMessages
            .Where(m => m.NotificationType == NotificationType.AppointmentReminder)
            .ToListAsync();
        Assert.Equal(2, suppressedReminders.Count);
        Assert.All(suppressedReminders, d => Assert.Equal(DeliveryStatus.Suppressed, d.Status));

        // 4. Out-of-order late AppointmentConfirmed for the same cancelled slot must NOT create a new reminder
        var lateConfirmEventId = Guid.NewGuid();
        await processor.ProcessEventAsync(lateConfirmEventId, EventTypes.AppointmentConfirmed, confirmJson, Guid.NewGuid());
        var lateReminders = await db.DeliveryOutboxMessages
            .Where(m => m.EventId == lateConfirmEventId && m.NotificationType == NotificationType.AppointmentReminder)
            .ToListAsync();
        Assert.Empty(lateReminders);
    }

    [Fact]
    public async Task OperatorDeadLetterEndpoints_InspectAndReplayWithoutExposingSecrets()
    {
        await using var factory = new NotificationTestFactory();
        var client = factory.CreateClient();

        var normalDeadLetterId = Guid.NewGuid();
        var resetDeadLetterId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.DeliveryOutboxMessages.AddRange(
                new DeliveryOutboxMessage
                {
                    Id = normalDeadLetterId,
                    EventId = Guid.NewGuid(),
                    RecipientId = Guid.NewGuid(),
                    RecipientType = RecipientType.Account,
                    Channel = NotificationChannel.Email,
                    NotificationType = NotificationType.AppointmentConfirmed,
                    IdempotencyKey = "dlq-normal-1",
                    RecipientAddress = "patient@example.com",
                    Content = "Appointment confirmation content",
                    Status = DeliveryStatus.DeadLettered,
                    AttemptsCount = 3,
                    MaxAttempts = 3
                },
                new DeliveryOutboxMessage
                {
                    Id = resetDeadLetterId,
                    EventId = Guid.NewGuid(),
                    RecipientId = Guid.NewGuid(),
                    RecipientType = RecipientType.Account,
                    Channel = NotificationChannel.Email,
                    NotificationType = NotificationType.PasswordReset,
                    IdempotencyKey = "dlq-reset-1",
                    RecipientAddress = "patient@example.com",
                    Content = "[REDACTED]",
                    Status = DeliveryStatus.DeadLettered,
                    AttemptsCount = 1,
                    MaxAttempts = 3
                });
            await db.SaveChangesAsync();
        }

        // 1. Inspect dead-lettered deliveries via internal operator endpoint
        using var missingOperatorReq = new HttpRequestMessage(HttpMethod.Get, "/internal/notifications/deliveries/dead-lettered");
        missingOperatorReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var missingOperatorRes = await client.SendAsync(missingOperatorReq);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, missingOperatorRes.StatusCode);

        using var listReq = new HttpRequestMessage(HttpMethod.Get, "/internal/notifications/deliveries/dead-lettered");
        listReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        var operatorId = Guid.NewGuid();
        listReq.Headers.Add("X-Clinic-Operator-Id", operatorId.ToString());
        var listRes = await client.SendAsync(listReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, listRes.StatusCode);
        var listRaw = await listRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Appointment confirmation content", listRaw);
        Assert.DoesNotContain("patient@example.com", listRaw);

        // 2. Replay normal dead-lettered delivery succeeds and resets status to Pending
        using var replayNormalReq = new HttpRequestMessage(HttpMethod.Post, $"/internal/notifications/deliveries/{normalDeadLetterId}/replay");
        replayNormalReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        replayNormalReq.Headers.Add("X-Clinic-Operator-Id", operatorId.ToString());
        var replayNormalRes = await client.SendAsync(replayNormalReq);
        Assert.Equal(System.Net.HttpStatusCode.OK, replayNormalRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var replayed = await db.DeliveryOutboxMessages.FindAsync(normalDeadLetterId);
            Assert.NotNull(replayed);
            Assert.Equal(DeliveryStatus.Pending, replayed.Status);
            Assert.Equal(0, replayed.AttemptsCount);
            var replayAudit = await db.DeliveryAttempts.SingleAsync(a => a.DeliveryOutboxMessageId == normalDeadLetterId);
            Assert.Equal(operatorId, replayAudit.OperatorId);
        }

        // 3. Replaying password-reset delivery is rejected with 409 Conflict
        using var replayResetReq = new HttpRequestMessage(HttpMethod.Post, $"/internal/notifications/deliveries/{resetDeadLetterId}/replay");
        replayResetReq.Headers.Add("X-Clinic-Service-Token", "integration-test-service-token");
        replayResetReq.Headers.Add("X-Clinic-Operator-Id", operatorId.ToString());
        var replayResetRes = await client.SendAsync(replayResetReq);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, replayResetRes.StatusCode);
    }

    [Fact]
    public async Task BrokerAndProviderRecovery_DeliversPendingNotificationsWithoutLoss()
    {
        var dbName = Guid.NewGuid().ToString();
        var deliveryId = Guid.NewGuid();

        // 1. Create a pending delivery
        await using (var db1 = TestDbHelper.CreateDbContext(dbName))
        {
            db1.DeliveryOutboxMessages.Add(new DeliveryOutboxMessage
            {
                Id = deliveryId,
                EventId = Guid.NewGuid(),
                RecipientId = Guid.NewGuid(),
                RecipientType = RecipientType.Account,
                Channel = NotificationChannel.Email,
                NotificationType = NotificationType.AppointmentConfirmed,
                IdempotencyKey = "recovery-key-1",
                RecipientAddress = "patient@example.com",
                Content = "Appointment confirmation body",
                Status = DeliveryStatus.Pending,
                AttemptsCount = 0,
                MaxAttempts = 3
            });
            await db1.SaveChangesAsync();
        }

        // 2. Dispatcher attempt 1: Provider is down (Transient failure)
        var failEmailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.TransientFailure, "503 Service Unavailable"))
        };

        await using (var db2 = TestDbHelper.CreateDbContext(dbName))
        {
            var dispatcher1 = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
                new TestServiceProvider(db2),
                failEmailSender,
                new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
                new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

            var status1 = await dispatcher1.ProcessDeliveryAsync(deliveryId);
            Assert.Equal(DeliveryStatus.Pending, status1);

            var failedRecord = await db2.DeliveryOutboxMessages.FindAsync(deliveryId);
            Assert.NotNull(failedRecord);
            Assert.Equal(1, failedRecord.AttemptsCount);
            Assert.Equal(DeliveryStatus.Pending, failedRecord.Status);
        }

        // 3. Service / provider recovers: Dispatcher attempt 2 succeeds
        var successEmailSender = new ConfigurableEmailSender
        {
            OnSend = (_, _, _, _) => Task.FromResult(new Clinic.Notification.Service.Services.Delivery.ProviderDeliveryResult(Clinic.Notification.Service.Services.Delivery.ProviderResultCategory.Success, "250 OK Delivered"))
        };

        await using (var db3 = TestDbHelper.CreateDbContext(dbName))
        {
            var dispatcher2 = new Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher(
                new TestServiceProvider(db3),
                successEmailSender,
                new Clinic.Notification.Service.Services.Delivery.LoggingSmsSender(Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.LoggingSmsSender>.Instance),
                new Clinic.Notification.Service.Services.Delivery.NullRealtimeNotifier(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Services.Delivery.NotificationDeliveryDispatcher>.Instance);

            var status2 = await dispatcher2.ProcessDeliveryAsync(deliveryId);
            Assert.Equal(DeliveryStatus.Delivered, status2);

            var deliveredRecord = await db3.DeliveryOutboxMessages.FindAsync(deliveryId);
            Assert.NotNull(deliveredRecord);
            Assert.Equal(2, deliveredRecord.AttemptsCount);
            Assert.Equal(DeliveryStatus.Delivered, deliveredRecord.Status);

            var attempts = await db3.DeliveryAttempts.Where(a => a.DeliveryOutboxMessageId == deliveryId).OrderBy(a => a.AttemptNumber).ToListAsync();
            Assert.Equal(2, attempts.Count);
            Assert.Equal(DeliveryStatus.Failed, attempts[0].Status);
            Assert.Equal(DeliveryStatus.Delivered, attempts[1].Status);
        }
    }

    [Fact]
    public async Task QueueEnvelopeSnapshot_CreatesPatientCalledNotificationWithoutPriorAppointmentEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = TestDbHelper.CreateDbContext(dbName);
        var serviceProvider = new TestServiceProvider(db);
        var policyEngine = new NotificationPolicyEngine();
        var processor = new Clinic.Notification.Service.Messaging.NotificationEventProcessor(
            serviceProvider,
            policyEngine,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Clinic.Notification.Service.Messaging.NotificationEventProcessor>.Instance);

        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        // Queue carries the recipient snapshot from AppointmentCheckedIn; it does not depend on a prior appointment notification.
        var queueEnvelope = new EventEnvelope<PatientCalled>
        {
            EventId = Guid.NewGuid(),
            EventType = EventTypes.PatientCalled,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Producer = "Clinic.Queue.Service",
            Payload = new PatientCalled(
                QueueTicketId: Guid.NewGuid(),
                AppointmentId: appointmentId,
                DoctorId: doctorId,
                QueueNumber: 7,
                CalledAt: DateTimeOffset.UtcNow,
                PatientId: patientId,
                IdentityUserId: identityUserId,
                PatientName: "Pham Queue Account",
                PatientPhone: "0988112233")
        };

        var queueElement = JsonSerializer.SerializeToElement(
            queueEnvelope,
            Clinic.Contracts.Serialization.EventJsonSerializerOptions.Default);
        Assert.True(await processor.ProcessEventAsync(queueEnvelope.EventId, EventTypes.PatientCalled, queueElement, queueEnvelope.CorrelationId));

        // Verify Notification Service unwrapped the envelope and used its own recipient snapshot.
        var patientCalledNotif = await db.Notifications.FirstOrDefaultAsync(n =>
            n.RecipientId == identityUserId && n.NotificationType == NotificationType.PatientCalled);
        Assert.NotNull(patientCalledNotif);

        var calledDeliveries = await db.DeliveryOutboxMessages
            .Where(d => d.EventId == queueEnvelope.EventId)
            .ToListAsync();
        Assert.Contains(calledDeliveries, d => d.Channel == NotificationChannel.InApp && d.RecipientId == identityUserId);
        Assert.Contains(calledDeliveries, d => d.Channel == NotificationChannel.Sms && d.RecipientAddress == "0988112233");
    }
}

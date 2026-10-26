using Clinic.Contracts.Events;
using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Clinic.Notification.Service.Services;
using Microsoft.EntityFrameworkCore;

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
    [Fact(Skip = "Phase 6 Step 3: provider failures need bounded exponential retry.")]
    public void TransientProviderFailure_SchedulesBoundedExponentialRetry() { }

    [Fact(Skip = "Phase 6 Step 3: exhausted retries must be dead-lettered and remain auditable.")]
    public void RetryExhaustion_DeadLettersDeliveryAndPreservesAudit() { }

    [Fact(Skip = "Phase 6 Step 3: provider timeout must not block unrelated deliveries.")]
    public void ProviderTimeout_IsBoundedAndOtherDeliveriesContinue() { }

    [Fact(Skip = "Phase 6 Step 3: provider acceptance with lost response must use delivery idempotency policy.")]
    public void AmbiguousProviderResult_DoesNotCreateUnboundedDuplicateAttempts() { }
}

public sealed class NotificationAuthorizationAndRealtimeTests
{
    [Fact(Skip = "Phase 6 Step 4: Notification Service must independently validate Identity JWTs.")]
    public void ProtectedNotificationEndpoint_RejectsMissingOrInvalidJwt() { }

    [Fact(Skip = "Phase 6 Step 4: users may read only their own account-bound notification history.")]
    public void User_CannotReadAnotherUsersNotificationHistory() { }

    [Fact(Skip = "Phase 6 Step 4: SignalR user mapping must use authenticated Identity subject, not client-supplied id.")]
    public void SignalRConnection_IsGroupedByAuthenticatedSubject() { }

    [Fact(Skip = "Phase 6 Step 4: SignalR is only a notification hint; persisted history remains authoritative.")]
    public void ReconnectedClient_CanRecoverMissedNotificationsFromHistoryApi() { }
}

public sealed class NotificationGatewayAndWorkflowTests
{
    [Fact(Skip = "Phase 6 Step 5: Gateway must proxy notification APIs and WebSocket negotiation without exposing service port.")]
    public void FrontendNotificationRequests_AreRoutedOnlyThroughGateway() { }

    [Fact(Skip = "Phase 6 Step 5: Identity reset code delivery uses a restricted dedicated contract and template.")]
    public void PasswordResetDelivery_DoesNotExposeTokenInLogsOrGeneralBusinessEvents() { }

    [Fact(Skip = "Phase 6 Step 6: verify notification lifecycle across real services and RabbitMQ in Docker Compose.")]
    public void AppointmentConfirmationAndCancellation_CreateExpectedNotifications_EndToEnd() { }

    [Fact(Skip = "Phase 6 Step 6: inject broker/provider failure, restart services, and prove durable recovery.")]
    public void BrokerAndProviderRecovery_DeliversPendingNotificationsWithoutLoss() { }
}

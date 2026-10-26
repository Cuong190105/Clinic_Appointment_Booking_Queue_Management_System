namespace Clinic.Notification.Service.Tests;

// These executable test specifications stay skipped until each step implements its production behavior.
// Replace Skip with assertions against the real service types as each plan step is completed.
public sealed class NotificationPolicyUnitTests
{
    [Fact(Skip = "Phase 6 Step 1: implement notification policy and event-to-recipient mapping.")]
    public void ConfirmedAppointment_ProducesOneConfirmationPerEligibleRecipientAndChannel() { }

    [Fact(Skip = "Phase 6 Step 1: reschedule-release cancellations must not notify as a user cancellation.")]
    public void RescheduleRelease_DoesNotProduceCancellationNotification() { }

    [Fact(Skip = "Phase 6 Step 1: guest recipients may use contact channels but have no account-bound inbox or SignalR user.")]
    public void GuestRecipient_UsesOnlyEligibleEmailOrSmsChannels() { }

    [Fact(Skip = "Phase 6 Step 1: scheduled reminders need a stable due time and cancellation policy.")]
    public void CancelledAppointment_SuppressesPendingReminder() { }
}

public sealed class NotificationPersistenceUnitTests
{
    [Fact(Skip = "Phase 6 Step 2: persist notification, inbox deduplication, and delivery outbox atomically.")]
    public void ConsumingBusinessEvent_PersistsInboxNotificationAndDeliveriesInOneTransaction() { }

    [Fact(Skip = "Phase 6 Step 2: duplicate event delivery must not create duplicate notification records.")]
    public void DuplicateEventId_DoesNotCreateDuplicateNotification() { }

    [Fact(Skip = "Phase 6 Step 2: delivery attempts must capture status, timestamp, provider response, and correlation id.")]
    public void DeliveryAttempt_PersistsRequiredAuditFieldsWithoutSecretsOrMessageBody() { }

    [Fact(Skip = "Phase 6 Step 2: recipient snapshots must not depend on Clinic Person or another service database.")]
    public void NotificationAggregate_StoresRecipientIdentityAndMinimalContactSnapshotOnly() { }
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

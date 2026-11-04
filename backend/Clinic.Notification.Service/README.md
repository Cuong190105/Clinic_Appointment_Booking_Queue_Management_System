# Clinic.Notification.Service

Notification Service owns notification persistence and delivery processing. It uses its own `ClinicNotificationDB`, validates Identity JWTs for account APIs and SignalR, consumes business events from RabbitMQ, and exposes REST/SignalR through `Clinic.API` as the Gateway. Email uses the configured SMTP provider; SMS remains a mock provider because no SMS service is in scope.

## Current capabilities

- Account notification history, unread count and mark-read APIs scoped to the authenticated Identity subject.
- Durable event inbox, notification records, delivery outbox and attempt audit.
- RabbitMQ consumer with bounded retries and a sanitized quarantine/DLQ path.
- Provider dispatch with bounded retry, password-reset payload protection/redaction, and reminder suppression records.
- SignalR notification hints; clients retrieve canonical notification content through Gateway REST.
- Internal password-reset and dead-letter delivery operations protected by the configured `Identity:ServiceToken`.

## Verification status

Phase 6 acceptance passed on 2026-11-01 in the Docker demo environment. Notification, Appointment and Queue suites passed (30/30, 35/35 and 51/51), Gateway operator authorization tests passed (2/2), and the full frontend suite passed (176/176). Live acceptance covered retry/DLQ through restart, reminder suppression on SQL Server, Admin-attributed delivery replay, authenticated SignalR user isolation/reconnect/history recovery, and persisted Data Protection keys. Email was subsequently verified end-to-end through the configured SMTP provider; SMS remains MOCK. See the [Phase 6 execution plan](../../tasks/PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md), Section 10.5, for the historical acceptance evidence and limits.

## Configuration notes

- Configure the service database, Identity authority/JWT audience, RabbitMQ connection, and `Identity:ServiceToken` through environment configuration.
- Do not use a fallback service token. The internal API returns `503` when the required token is not configured.
- In Docker Compose, the ASP.NET Data Protection key ring is persisted in the `notification_protection_keys` named volume mounted at `/var/lib/clinic/notification-protection-keys`. Set `DataProtection:KeyDirectory` to a durable, access-controlled location in other deployments.
- Configure `Email__Host`, `Email__Port`, `Email__Username`, and `Email__Password` from environment secrets. Gmail requires an App Password when two-step verification is enabled.
- Email is sent through the configured SMTP provider. Recipient addresses are masked in logs and message bodies are never logged.
- SMS remains an explicit mock adapter and does not send real messages.

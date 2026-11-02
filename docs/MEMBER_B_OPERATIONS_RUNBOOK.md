# Member B messaging and service operations runbook

## Scope

This runbook covers RabbitMQ, transactional outbox/inbox processing, Medical Records consumers, Notification delivery, dead-letter handling, replay, and observability. Business payloads and provider credentials must never be copied into tickets or logs.

## Local startup and health

1. Configure secrets in `.env`; do not commit SMTP, SMS, JWT, database, or service-token credentials.
2. Run `docker compose up -d --build`.
3. Verify `/healthz` and `/readyz` through ports 5000–5006, RabbitMQ management on port 15672, Prometheus on 9090, and Jaeger on 16686.
4. A service is ready only when its database is reachable. RabbitMQ consumers reconnect automatically after a broker restart.

Durable queues of interest:

- Medical Records: `clinic.medical-records.events.v2`; dead-letter queue `clinic.medical-records.events.dead`.
- Notification: `clinic.notification.events.v2`; retry queue `clinic.notification.events.retry`; dead-letter queue `clinic.notification.events.dlq`.
- Existing Scheduling, Appointment, and Queue service queues documented in their service topology.

## Delivery and retry policy

- The current Medical Records consumer rejects processing failures without requeue, so RabbitMQ sends them to `clinic.medical-records.events.dead`; there is no transient retry queue yet. Do not assume a failed event will be retried automatically.
- Notification event processing uses a bounded retry queue (up to three retries at 15-second TTL intervals) and quarantines exhausted/poison events as sanitized metadata in `clinic.notification.events.dlq`.
- Notification provider delivery uses its separate bounded retry and attempt-audit policy.
- Permanent failures (`recipient.missing`, `provider.unconfigured`, unsupported channel) go directly to `DeadLettered` delivery status.
- A delivery is claimed atomically with a 45-second processing lease. Another replica can reclaim an expired lease after a worker crash, while an active lease prevents concurrent provider calls.
- Every provider attempt stores timestamps, a non-sensitive provider response/error code, correlation ID, and outcome. Response bodies and medical/notification content are not logged.
- Inbox/idempotency prevents duplicate business effects. A late `ExamStarted` must not undo an encounter already marked completed; `ExamCompleted` does not finalize a MedicalReport.

## Investigation

1. Search logs by `X-Correlation-ID`, event ID, appointment ID, or delivery ID. Never search by clinical text or credentials.
2. Inspect Prometheus `/metrics`, RabbitMQ queue depth/unacked count, and Jaeger traces.
3. For medical messages, inspect the Inbox and `EncounterContexts` by `AppointmentId`; encounter status is independent of the MedicalReport Draft/Finalized lifecycle. Check `MedicalAccessAudits` for access/action history.
4. For notifications, inspect `GET /admin/notifications/dead-lettered`; the operator API returns sanitized delivery status/error metadata.
5. Correct the upstream data/configuration cause before replay.

## Safe replay

- Notification delivery replay: `POST /admin/notifications/deliveries/{deliveryId}/replay` as Admin. This requeues one delivery and retains prior attempts for audit.
- Medical event recovery: correct the cause, then replay the original event from its producer/outbox using the original `eventId`, correlation/causation metadata, schema version and routing key. There is no MedicalHistory projection rebuild endpoint in the accepted design.
- RabbitMQ DLQ replay must preserve the original envelope `eventId`, `correlationId`, `schemaVersion`, and routing key. Publish one message at a time to `clinic.events`; do not edit protected payload fields in the management UI.
- Inbox idempotency makes replay safe after a consumer crash following commit. If the inbox row exists, an event replay is acknowledged without creating a second record or notification.
- Never bulk replay until a single representative event succeeds and queue depth/consumer error rate remain stable.

## Reconciliation

- Compare Medical Records `EncounterContext` completion against Queue/Appointment completion by `AppointmentId`; report discrepancies without automatically finalizing or changing a MedicalReport.
- Compare notification business events against `NotificationMessage.EventId`, then messages against at least one delivery row. A missing delivery is repairable by replaying the original event; a dead-lettered provider attempt requires correcting the provider or recipient snapshot first.
- Reconciliation is read-only by default. Clinical record corrections require an authorized amendment workflow and audit entry.

# Member B messaging and service operations runbook

## Scope

This runbook covers RabbitMQ, transactional outbox/inbox processing, Medical Records consumers, Notification delivery, dead-letter handling, replay, and observability. Business payloads and provider credentials must never be copied into tickets or logs.

## Local startup and health

1. Configure secrets in `.env`; do not commit SMTP, SMS, JWT, database, or service-token credentials.
2. Run `docker compose up -d --build`.
3. Verify `/healthz` and `/readyz` through ports 5000–5006, RabbitMQ management on port 15672, Prometheus on 9090, and Jaeger on 16686.
4. A service is ready only when its database is reachable. RabbitMQ consumers reconnect automatically after a broker restart.

Durable queues of interest:

- `q.medical-records.events` and `q.medical-records.dlq`
- `q.notification.business-events` and `q.notification.dlq`
- Existing Scheduling, Appointment, and Queue service queues documented in their service topology.

## Delivery and retry policy

- Medical event processing retries four times with bounded exponential delay, then RabbitMQ routes the event to `q.medical-records.dlq`.
- Notification provider delivery times out after 15 seconds and retries transient failures at most five attempts with exponential backoff capped at five minutes.
- Permanent failures (`recipient.missing`, `provider.unconfigured`, unsupported channel) go directly to `DeadLettered` delivery status.
- A delivery is claimed atomically with a 45-second processing lease. Another replica can reclaim an expired lease after a worker crash, while an active lease prevents concurrent provider calls.
- Every provider attempt stores timestamps, a non-sensitive provider response/error code, correlation ID, and outcome. Response bodies and medical/notification content are not logged.
- Inbox `EventId` uniqueness prevents duplicate business effects. Medical workflow version prevents late `ExamStarted` from reverting a finalized encounter.

## Investigation

1. Search logs by `X-Correlation-ID`, event ID, appointment ID, or delivery ID. Never search by clinical text or credentials.
2. Inspect Prometheus `/metrics`, RabbitMQ queue depth/unacked count, and Jaeger traces.
3. For medical messages, confirm whether `InboxMessages` contains the event, whether the record workflow version is `1` (Draft) or `2` (Finalized), and whether the corresponding `MedicalHistory` projection exists for a finalized record.
4. For notifications, inspect `GET /admin/deliveries?status=DeadLettered`; the endpoint intentionally exposes only status and error codes.
5. Correct the upstream data/configuration cause before replay.

## Safe replay

- Notification delivery replay: `POST /admin/deliveries/{deliveryId}/replay` as Admin. This resets one delivery only and retains its previous attempts for audit.
- Medical history projection rebuild: `POST /admin/medical-history/rebuild` as Admin. It recreates the read model from finalized service-owned records without modifying clinical source rows.
- RabbitMQ DLQ replay must preserve the original envelope `eventId`, `correlationId`, `schemaVersion`, and routing key. Publish one message at a time to `clinic.events`; do not edit protected payload fields in the management UI.
- Inbox idempotency makes replay safe after a consumer crash following commit. If the inbox row exists, an event replay is acknowledged without creating a second record or notification.
- Never bulk replay until a single representative event succeeds and queue depth/consumer error rate remain stable.

## Reconciliation

- Compare finalized Medical Records encounters against `ExamCompleted`/Appointment completed state by `AppointmentId`; report discrepancies without automatically changing clinical data.
- Compare notification business events against `NotificationMessage.EventId`, then messages against at least one delivery row. A missing delivery is repairable by replaying the original event; a dead-lettered provider attempt requires correcting the provider or recipient snapshot first.
- Reconciliation is read-only by default. Clinical record corrections require an authorized amendment workflow and audit entry.

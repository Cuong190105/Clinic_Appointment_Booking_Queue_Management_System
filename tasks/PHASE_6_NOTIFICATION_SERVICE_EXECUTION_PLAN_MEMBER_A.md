# Phase 6 — Notification Service execution plan (Member A)

**Status:** design decisions accepted; service/test project scaffold created. Production notification behavior and traffic cutover have not started.

**Scope:** Member A owns Notification Service, Notification DB, API, migration, provider/template integration, SignalR ownership and traffic cutover. Member B coordinates event contracts and publisher/consumer reliability, failure/contract testing, telemetry, Gateway/frontend integration, and operational acceptance. Shared event contracts and routing changes require review from both members.

## 1. Accepted decisions

1. Keep `Clinic.API` as the existing API Gateway/BFF. Do not create a separate Gateway service in this phase. The frontend reaches REST and SignalR only through the Gateway. Notification Service independently validates Identity JWTs.
2. Notification DB is the durable source of in-app history. SignalR is a best-effort hint containing notification ID/type, not the only delivery or source of notification content. The client fetches content through Gateway and recovers missed updates through the history API.
3. Replace `Person` with a notification-owned recipient value: `RecipientId` and `RecipientType`. An account recipient uses Identity `UserId`; an unregistered patient uses stable `PatientId`. Keep an optional PatientId/IdentityUserId linkage only if needed for import and deduplication; never share a domain entity or query another service database at runtime.
4. An account recipient gets an in-app record according to event policy. Email/SMS are sent only when the event policy enables that channel and a valid contact snapshot is present. A patient without an account can receive email/SMS according to policy, but has no account inbox or SignalR connection.
5. Initial business notifications: Appointment confirmation, Appointment cancellation except reschedule-release, scheduled appointment reminder, and `PatientCalled`. Do not notify on every `AppointmentCreated` unless it represents a confirmed booking. Keep password reset delivery on a dedicated restricted command/template contract, separate from business events.
6. Event publishers provide only required recipient ID/type and channel-specific contact snapshot. Notification Service does not perform synchronous contact lookups across services. Do not include medical report content, credentials, reset tokens, or unnecessary PHI in logs or ordinary events.
7. Use durable inbox/idempotency and delivery outbox. Idempotency scope is `EventId + RecipientId + Channel + NotificationType`. Delivery retries use bounded exponential backoff, timeout and dead-letter handling. Do not claim exactly-once delivery from external providers.
8. Run one Notification Service/SignalR instance in the current Docker Compose setup. Add Redis backplane only if deployment scales to multiple instances.

## 2. Current state and known constraints

- `Clinic.API` still owns Notification entity/repository/handlers, `Channel<T>`, workers, password-reset queueing and `/hubs/notifications`; these use ClinicDB and/or `Person`.
- `NotificationHub` is empty, the current `InAppSender` uses `Clients.User`, and the Vue frontend does not currently include a SignalR client.
- Existing event contracts include appointment lifecycle and `PatientCalled`; current appointment events do not consistently contain a complete recipient/contact snapshot. Contract changes and snapshot ownership require Member B plus each publishing service owner.
- The current Phase 6 API behavior is not implemented in the scaffold. The added service exposes health/readiness only. Business-specification tests are deliberately skipped until the corresponding implementation lands.
- Notification history may exist in ClinicDB. Import and reconciliation must map account-bound legacy Person IDs to Identity user IDs and preserve guest Patient IDs/contact snapshots where the source data supports it. Never silently discard unmatched rows.
- Real provider delivery depends on configured credentials. Automated and E2E tests use provider fakes; acceptance must not send test messages to real patient contacts.

## 3. Step-by-step plan, requirements and tests

### Step 0 — Contract, recipient and notification policy agreement (A + B)

**Work**

- Inventory every `INotificationQueue` caller, template, query, scheduled job and frontend consumer. Build a matrix: source event/command, recipient type, dedupe key, notification type, allowed channels, scheduled time, cancellation/suppression behavior.
- Agree schemas for `NotificationRequested`/business event envelopes and the restricted password-reset delivery command. Include EventId, schema version, occurred time, correlation/causation IDs and bounded contact snapshot fields.
- Confirm the event fields that each owning service can provide: Appointment confirmation/cancellation/reminder and Queue `PatientCalled`. Mark reschedule-release cancellation explicitly.
- Define recipient mapping: account → Identity UserId; guest → PatientId; account and guest channel rules; email/SMS verification/availability behavior.
- Define provider-result categories, timeout budget, max attempts/backoff, dead-letter retention/replay policy and duplicate-provider ambiguity policy.

**Requirements to pass**

- Contract contains no domain entity, medical report content, password, reset token, or unnecessary personal data.
- Every initial event has exactly one documented recipient/policy mapping and idempotency scope.
- No notification is sent for a reschedule-release cancellation; a true cancellation has a stable policy.
- Password reset cannot be triggered by a general business-event payload and supports only the approved template.
- Contract versioning and compatibility/replay behavior are written down and reviewed by A/B.

**Tests**

- Contract unit tests: required IDs/timestamps, supported event/type/channel, schema version, recipient type and malformed/oversized snapshot rejection.
- Privacy contract test: serialized ordinary events contain no credential/reset token/clinical fields.
- Policy unit tests: confirmed appointment, true cancellation, reschedule release, reminder, `PatientCalled`, guest channel eligibility and duplicate EventId.

**Runtime scenario (after source events exist):** create a confirmed appointment and a true cancellation through Gateway and verify the agreed outbox events and notification policy; reschedule an appointment and verify the release event creates no cancellation notification. Use test recipients and fake providers.

### Step 1 — Isolated service and database boundary

**Work**

- Extend the scaffold with `NotificationDbContext`, migrations, SQL Server connection to `ClinicNotificationDB`, readiness database check and migration startup strategy consistent with other services.
- Add entities/configurations for recipient-owned notification, event inbox/deduplication, delivery outbox and delivery attempts. Add indexes/unique constraints for idempotency, unread history paging and due work.
- Add independent Identity JWT validation and service authorization policies. Configure structured logs, correlation context, OpenTelemetry traces/metrics and graceful hosted-worker shutdown.
- Keep Docker Compose service on port 5006, with a distinct database name and health/readiness ordering. Do not add Gateway routes yet.

**Requirements to pass**

- Service has no project reference to Clinic.Domain/Application/Infrastructure and cannot query ClinicDB.
- Migration creates a separate Notification schema; duplicate idempotency keys are rejected at DB level.
- Health and readiness distinguish process liveness from DB readiness.
- Database indexes support recipient history, unread count, idempotency and due delivery scans.
- JWT validation is independent of the Gateway and accepts the configured Identity issuer/audience only.

**Tests**

- EF model tests for required columns, lengths, indexes, unique constraints and no foreign key to another service.
- Migration test against SQL Server test container or disposable SQL database; verify independent database creation and upgrade.
- Readiness integration tests: healthy DB returns ready; unavailable DB returns 503 without leaking connection secrets.
- Auth integration tests: missing/invalid/expired/wrong-audience tokens return 401; health endpoints remain public.

**Runtime scenario:** `docker compose up -d db identity-service notification-service`; inspect that ClinicNotificationDB is distinct, service becomes healthy only after migration/DB connection, and invalid JWT cannot access a protected test route. Do not expose the Notification API port publicly beyond the local Compose development mapping.

### Step 2 — Notification policy, persistence and legacy import

**Work**

- Implement recipient value types, notification lifecycle, unread/read operations, scheduled state and notification type/channel templates.
- Implement account and guest recipient policy described in Section 1. Store immutable contact snapshots only for channels the policy intends to use.
- Build idempotent ClinicDB → NotificationDB importer for history to retain. Map linked Person to Identity UserId where available; preserve guest PatientId/contact snapshot where reliably identifiable. Produce an explicit mismatch report and do not drop unresolvable rows.
- Add import verifier for source/target counts, IDs, ownership mapping, read state, timestamps, channel/status and duplicates. Make reruns safe.

**Requirements to pass**

- No Notification entity has a `Person` navigation/FK or depends on Clinic Domain.
- Guest notifications are contact-channel-only; account notifications use account identity for in-app ownership.
- Snapshot fields are minimized, protected and not used for runtime cross-service reads.
- Import is idempotent, verifiable and stops on unexplained mismatch; no legacy table deletion before acceptance/backup.

**Tests**

- Unit tests for account/guest recipient mapping, channel eligibility, schedule/cancel/read transitions and privacy minimization.
- Persistence tests for unread paging, mark-read idempotence, stable ordering/cursor behavior and unique event-recipient-channel keys.
- Import tests for linked account, guest, duplicate source row, missing identity link, mismatch and rerun.

**Runtime scenario:** take a mock ClinicDB snapshot, run importer twice, verify same target rows and zero unexplained mismatch; verify account recipient maps to Identity UserId and guest remains PatientId without creating a Person dependency. Compare unread history before/after migration.

### Step 3 — Durable event consumption and scheduling

**Work**

- Implement RabbitMQ topology/consumer for the agreed event contracts with durable inbox and transactional notification/outbox writes.
- Add event-to-notification policy handlers, reminder scheduling/cancellation, deduplication and handling for delayed/out-of-order events.
- Coordinate source outbox/event fields with Appointment and Queue owners. Never call source services synchronously to build a notification graph.
- Add a delivery dispatcher that leases due outbox rows and survives restarts; use bounded concurrency and graceful shutdown.

**Requirements to pass**

- Inbox, notification and delivery outbox state commit atomically in NotificationDB.
- Redelivery of an EventId does not create extra notification records or deliveries.
- Event arrives only after source transaction committed; broker outage leaves durable source outbox pending for retry.
- Reminder is scheduled once, canceled/suppressed after true cancellation or reschedule, and never fires for a replaced appointment.
- Poison messages follow bounded retry/DLQ policy; no PHI/contact secrets in logs or unprotected DLQ payload.

**Tests**

- Unit tests for mapping and policy suppression.
- Integration tests with RabbitMQ and SQL Server: duplicate, malformed, out-of-order, retry, consumer crash before/after DB commit, broker restart, DLQ and recovery.
- Concurrency tests for two consumers processing the same EventId and reminder due row.
- Contract tests against Appointment and Queue publisher envelopes.

**Runtime scenario:** stop RabbitMQ, create appointment/check-in events, restore RabbitMQ, and verify source outbox publishes and Notification Service consumes once. Replay the same event and verify one notification/outbox per policy. Trigger reschedule and prove no false cancellation/reminder is emitted.

### Step 4 — Provider delivery, retry and delivery audit

**Work**

- Port email/SMS provider integration behind Notification-owned interfaces; preserve approved templates and move configuration/secrets to environment/secret configuration.
- Add provider timeout, bounded exponential backoff with jitter, retry exhaustion, dead-letter and operator replay path.
- Persist one delivery-attempt record per attempt with status, timestamps, sanitized provider response, correlation ID and provider reference. Do not persist secrets or unnecessary rendered message body.
- Handle ambiguous provider response explicitly; use provider idempotency where supported and mark unknown outcomes for safe operational review.
- Send account in-app record independently from email/SMS channel failures.

**Requirements to pass**

- Provider timeout cannot block unrelated recipients/channels indefinitely.
- Retry count and next-attempt time are bounded and durable across process restart.
- Every attempt is auditable without leaking credentials, reset token or clinical information.
- Retry exhaustion is visible and replay requires an explicit operator action; no infinite retry loop.

**Tests**

- Provider adapter contract tests for accepted, transient failure, permanent failure, timeout, malformed response and ambiguous result.
- Unit tests for exponential backoff bounds, jitter range, retry exhaustion and channel isolation.
- Integration tests proving pending delivery resumes after process restart and attempt records are complete/sanitized.

**Runtime scenario:** use a deterministic fake provider that times out/fails then succeeds; restart Notification Service while attempts are pending; verify attempts/audit survive and eventual success is recorded once according to idempotency policy. No real email/SMS is sent in this scenario.

### Step 5 — Notification REST API, authorization and SignalR

**Work**

- Add service endpoints for current account history, unread count, mark-read and SignalR hub. Guest contact-only notifications have no account history API.
- Authorize history by authenticated Identity subject; never accept recipient ID from caller to select another user's inbox.
- Validate JWT on Notification Service. Map SignalR user identity from authenticated `sub`; ignore client-supplied recipient/group IDs.
- Emit only notification ID/type and correlation metadata through SignalR; client fetches content via REST Gateway.
- Add reconnect/recovery cursor/history behavior. Run one instance in Compose; defer Redis until a multi-instance deployment is planned.

**Requirements to pass**

- Account can only read/mark its own notifications; receptionist/doctor roles cannot enumerate patients' notifications absent explicit policy.
- Guest notifications cannot be read through an account endpoint without an authenticated linked account.
- SignalR connect requires valid JWT; connection receives only events for its authenticated user.
- Reconnect or missed SignalR event does not lose history; REST is authoritative.

**Tests**

- API integration tests for own history, cross-user IDOR (403/404 policy), mark-read, unread count, paging, guest isolation, unauthorized access and validation.
- SignalR integration tests for JWT handshake, user mapping, event isolation, reconnect/recovery and no content leakage in push payload.

**Runtime scenario:** connect two authenticated test users through Gateway WebSocket, create a notification for only one user and verify only that connection receives the hint; disconnect/reconnect and retrieve the persisted item through history. Attempt user A's history as user B and verify denial.

### Step 6 — Restricted password-reset notification flow

**Work**

- Replace `QueuePasswordResetNotificationCommand` and its `Person` lookup with a dedicated Identity-to-Notification request/event contract.
- Permit only password-reset template/type; use account ID and minimal contact snapshot; do not expose arbitrary template/content input.
- Store reset code only as required for dispatch and protect it in transit/storage; never include it in logs, tracing tags, ordinary events or general dead-letter output. Apply short retention and restricted access.
- Preserve generic forgot-password response/timing so account enumeration is not introduced.

**Requirements to pass**

- Password reset remains functional after Clinic notification handlers are disabled.
- Invalid caller/service token cannot enqueue reset delivery; unauthorized notification type/template is rejected.
- Reset secret is absent from application logs, audit, metrics, ordinary event payload and unprotected DLQ.
- Replays follow the Identity reset-code one-time/expiry semantics and cannot create unlimited sends.

**Tests**

- Contract/API tests for service-token authorization, accepted reset template, rejection of arbitrary payload/template and duplicate request.
- Log-capture/privacy tests proving reset code redaction.
- End-to-end test with Identity, Gateway, Notification Service and fake email/SMS provider for request, delivery, reset, expiry and retry.

**Runtime scenario:** request reset for a synthetic account through Gateway, verify fake provider captured one message, complete reset, replay/expire the code and confirm rejection. Scan service logs/traces/DLQ test harness for the secret; do not use a real account or provider.

### Step 7 — Gateway/frontend cutover and retirement of Clinic notification pipeline

**Work**

- Add Gateway proxy routes for `/me/notifications`, unread/read operations and `/hubs/notifications`, including WebSocket upgrade, JWT/correlation forwarding, timeout and rate limits.
- Add Vue SignalR client and notification history UI; reconnect with backoff, use SignalR as hint only, then fetch through Gateway. Keep graceful fallback to history polling/loading where websocket is unavailable.
- Cut Identity reset notification, notification reads and all business notification callers to the service. Update each publishing service to write outbox events.
- After acceptance/import/backup, remove monolith Notification workers, `Channel<T>`, handlers, repository/DbSet/configuration, Hub/sender and old API routes. Add EF migration to drop ClinicDB Notifications table only after history reconciliation and rollback window.
- Keep Clinic.API as Gateway/BFF; ensure no frontend configuration points directly to Notification Service.

**Requirements to pass**

- Frontend REST and WebSocket traffic uses only Gateway; Notification Service independently validates JWT.
- No runtime caller injects `INotificationQueue` or reads/writes ClinicDB Notifications/Person for notification delivery/history.
- ClinicDB drop migration is backed up, reversible within agreed rollback window or explicitly documented as irreversible after archive, and data reconciliation passes first.
- Existing application email reset and notification history flows work after cutover.

**Tests**

- Gateway proxy tests for route, auth/correlation forwarding, error mapping, WebSocket upgrade and direct service route absence from frontend config.
- Frontend unit tests for connection lifecycle, reconnect, unread/list updates, duplicate hint handling, history recovery and auth logout cleanup.
- Regression tests proving retired Clinic notification worker/handler registrations and routes are absent.

**Runtime scenario:** use browser on the normal frontend origin; verify REST and WebSocket requests target Gateway, not port 5006. Confirm list/unread/read and reconnect recovery. Run password reset and appointment/queue notification flows with synthetic recipients and fake providers. Verify old Clinic path no longer sends or serves notification data after cutover.

### Step 8 — Full reliability acceptance and phase close

**Work**

- Run unit, integration, contract, frontend and service tests; run clean Docker build/health; apply migrations on a fresh empty NotificationDB and existing mock data database.
- Execute runtime fault scenarios below; record results, logs/metrics without PII, importer counts and rollback evidence.
- Update both member task files, this plan and operations/runbook; mark each item complete only with evidence.

**Definition of Done**

- Separate Notification DB is the only runtime owner of notifications/delivery audit; history migration/reconciliation has zero unexplained mismatch.
- Durable consumer/dispatcher survives duplicate event, broker outage/restart, provider timeout/failure, service restart and retry exhaustion.
- Delivery attempt, status and provider result can be traced by event/correlation ID without PHI/secrets.
- Password reset works through dedicated contract and is secret-safe.
- Frontend uses Gateway for REST/SignalR and recovers all notification history from persisted API after reconnect.
- No notification runtime dependency remains on Clinic `Person`, ClinicDB, or `Channel<T>`.
- Tests pass, compose services healthy, documentation/runbook complete, and rollback/cutover status recorded.

**Full-system acceptance scenarios (must run after service implementation):**

1. Appointment confirmation → broker → one account inbox record and policy-enabled fake email; duplicate event does not duplicate either delivery.
2. True cancellation sends one cancellation notification; `IsRescheduleRelease=true` sends none and cancels pending reminder.
3. Appointment reminder is delivered at due time once; restart service before due time and verify it still fires once.
4. Queue `PatientCalled` produces the correct patient hint; two users connected through Gateway do not receive each other's event.
5. RabbitMQ outage and recovery: source outboxes remain pending, then deliver; Notification Service restart does not lose inbox/outbox/delivery state.
6. Provider timeout → bounded retries → fake provider success; permanent failure → exhausted state/DLQ and operator replay; unrelated channel keeps progressing.
7. Identity forgot-password → dedicated Notification contract → fake provider; reset works once; no secret appears in captured logs, traces, general event/DLQ or metrics.
8. Gateway/frontend REST and WebSocket cutover, access-control/IDOR, reconnect recovery, CORS/rate limiting/correlation ID and no direct frontend-to-service requests.
9. Legacy import rerun/reconciliation, Clinic notification schema retirement after backup, and no old worker or endpoint emitting duplicate messages.

## 4. Test project and current test status

`backend/Clinic.Notification.Service.Tests` is in `Clinic.slnx`. `ServiceScaffoldIntegrationTests` runs now against health/readiness. `NotificationRequirementsTests` contains named, skipped executable specifications grouped by policy, persistence, delivery, authorization/realtime, Gateway and full workflow; replace each skip with assertions as the corresponding step is implemented. Skipped tests are not evidence that the requirement is met.

The current scaffold is intentionally not registered in Gateway and does not consume RabbitMQ or connect to SQL yet. Its Compose readiness is process-only until Step 1 adds DB migration/readiness. Do not count the scaffold as a Phase 6 cutover.

---

## 5. Báo cáo thực hiện theo từng bước (Execution Log)

### Bước 1 — Chuẩn hóa Hợp đồng, Định danh Người nhận & Động cơ Chính sách (Notification Policy & Event Contracts) (Hoàn tất: 2026-10-26)

1. **Chuẩn hóa Hợp đồng & Snapshot Người nhận (Event Contracts & Recipient Snapshots):**
   - Cập nhật hợp đồng sự kiện `AppointmentConfirmed` và `AppointmentCancelled` trong `Clinic.Contracts/Events` với các tham số snapshot liên hệ tùy chọn (`PatientName`, `PatientEmail`, `PatientPhone`, `IdentityUserId`), đảm bảo tính tương thích ngược 100% với các service hiện tại.
   - Bổ sung thông tin định danh và snapshot cho sự kiện `PatientCalled` trong `QueueEvents.cs`.
   - Bổ sung các hằng số tên sự kiện chuẩn vào `EventTypes`: `AppointmentReminderScheduled`, `NotificationRequested`, `PasswordResetNotificationRequested`.

2. **Triển khai Mô hình Định danh & Động cơ Chính sách Thông báo (`NotificationPolicyEngine`):**
   - Xây dựng các kiểu thực thể nền tảng trong `Clinic.Notification.Service/Entities`:
     - `RecipientType`: `Account` (Người dùng có tài khoản), `Guest` (Bệnh nhân vãng lai chưa liên kết tài khoản).
     - `NotificationChannel`: `InApp`, `Email`, `Sms`.
     - `NotificationType`: `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentReminder`, `PatientCalled`, `PasswordReset`.
     - `DeliveryStatus`: `Pending`, `Delivered`, `Failed`, `DeadLettered`.
     - `RecipientContactSnapshot`: Chứa các trường liên hệ tối thiểu (`FullName`, `Email`, `PhoneNumber`).
   - Xây dựng `NotificationPolicyEngine` (`INotificationPolicyEngine`):
     - `EvaluateAppointmentConfirmed`: Tự động phân loại `Account` (sinh InApp + Email/Sms) và `Guest` (chỉ sinh Email/Sms, **tuyệt đối không tạo bản ghi InApp hay gán kết nối SignalR**).
     - `EvaluateAppointmentCancelled`: Phân biệt rõ việc hủy do đổi lịch (`IsRescheduleRelease = true`) để **triệt tiêu hoàn toàn (suppress)** thông báo hủy sai lệch tới người dùng; chỉ phát thông báo khi là hủy thực sự.
     - `EvaluatePatientCalled`: Phát InApp cho tài khoản để đẩy tín hiệu realtime qua SignalR; phát SMS cho khách nếu có số điện thoại.
     - `ShouldCancelPendingReminder`: Tự động triệt tiêu/hủy reminder đang chờ cho ca khám bị hủy.
     - `GenerateIdempotencyKey`: Định chuẩn khóa chống trùng lặp theo cấu trúc `{EventId}:{RecipientId}:{Channel}:{Type}`.

3. **Hoàn thiện Bộ Kiểm thử Chấp nhận Chính sách & Bảo vệ Dữ liệu (TDD Unit Tests):**
   - Xóa bỏ trạng thái `Skip` và hoàn thiện toàn bộ mã kiểm thử trong nhóm `NotificationPolicyUnitTests` tại `NotificationRequirementsTests.cs`:
     - `ConfirmedAppointment_ProducesOneConfirmationPerEligibleRecipientAndChannel`: PASS
     - `RescheduleRelease_DoesNotProduceCancellationNotification`: PASS
     - `GuestRecipient_UsesOnlyEligibleEmailOrSmsChannels`: PASS
     - `CancelledAppointment_SuppressesPendingReminder`: PASS
     - `OrdinaryEventContracts_ContainNoCredentials_ResetTokens_OrClinicalPHI`: PASS (Quét phản xạ toàn bộ thuộc tính, chứng minh không chứa secret/token/PHI lâm sàng).
   - Toàn bộ test suite `Clinic.Notification.Service.Tests`: **7/7 tests PASS** (2 scaffold tests + 5 policy & contract tests).
   - Không gây ảnh hưởng tới các service khác: `Clinic.Contracts.UnitTests` (6/6 PASS), `Clinic.Appointment.Service.Tests` (34/34 PASS).

---

### Bước 2 — Tách biệt Cơ sở Dữ liệu, Lưu trữ Bền vững & Di chuyển Dữ liệu Lịch sử (Isolated Database, Persistence & Legacy Import) (Hoàn tất: 2026-10-26)

1. **Thiết kế & Tách biệt Cơ sở dữ liệu Độc lập (`ClinicNotificationDB`):**
   - Xây dựng `NotificationDbContext` sở hữu độc lập schema thông báo: `Notifications`, `InboxMessages`, `DeliveryOutboxMessages`, `DeliveryAttempts`.
   - Mô hình `NotificationItem` lưu trữ snapshot định danh người nhận (`RecipientId`, `RecipientType`, `ContactName`, `ContactEmail`, `ContactPhone`), tách rời hoàn toàn khỏi `Person` entity và ClinicDB.
   - Mô hình `DeliveryOutboxMessage` áp đặt ràng buộc duy nhất (`UNIQUE INDEX`) trên `IdempotencyKey` (`{EventId}:{RecipientId}:{Channel}:{Type}`), đảm bảo ngăn chặn triệt để trùng lặp ở tầng database.
   - Mô hình `DeliveryAttempt` lưu trữ dấu vết kiểm toán phát thông báo (`Status`, `AttemptedAt`, `ProviderResponse`, `ProviderReference`, `CorrelationId`), **tuyệt đối không lưu secret, reset code, password hay message body**.
   - Khởi tạo migration `InitialNotificationSchema` và áp dụng thành công lên cơ sở dữ liệu `ClinicNotificationDB`.

2. **Migration & Đối soát Dữ liệu Lịch sử (Legacy Data Import & Reconciliation):**
   - Xây dựng script `import-from-clinicdb.sql` an toàn, có transaction và bảo đảm tính idempotent qua mệnh đề `WHERE NOT EXISTS`. Tự động ánh xạ `PersonId` legacy sang `Identity.UserId` thông qua kết nối với `ClinicIdentityDB.dbo.Users`.
   - Xây dựng script đối soát `verify-notifications-import.sql` kiểm chứng số lượng, tính toàn vẹn và tỷ lệ trùng khớp.
   - Kết quả thực thi thực tế trên `clinic_sql_server`:
     - Số lượng bản ghi nguồn `ClinicDB`: 58
     - Số lượng bản ghi đích `ClinicNotificationDB`: 58
     - Sai lệch (CountDifference): 0
     - Thiếu Recipient (MissingRecipientCount): 0
     - Trùng lặp ID (DuplicateIdCount): 0
     - Khách chưa map (UnmappedAccountCount): 0
     - Chạy lại lần 2 (Rerun Idempotency Test): 0 dòng bị ghi đè, toàn vẹn 100%.

3. **Hoàn thiện Bộ Kiểm thử Chấp nhận Bền vững (Persistence & Import TDD Tests):**
   - Hoàn thiện 100% các test case trong nhóm `NotificationPersistenceUnitTests`:
     - `ConsumingBusinessEvent_PersistsInboxNotificationAndDeliveriesInOneTransaction`: PASS (Ghi nhận Inbox, Notification và Outbox trong cùng 1 transaction nguyên tử).
     - `DuplicateEventId_DoesNotCreateDuplicateNotification`: PASS (Chống trùng lặp theo IdempotencyKey).
     - `DeliveryAttempt_PersistsRequiredAuditFieldsWithoutSecretsOrMessageBody`: PASS (Audit delivery an toàn không rò rỉ secret).
     - `NotificationAggregate_StoresRecipientIdentityAndMinimalContactSnapshotOnly`: PASS (Xác nhận cấu trúc aggregate độc lập, không dính Person/ClinicDB/PHI lâm sàng).
   - Bổ sung `NotificationImportVerificationTests`: PASS (3/3 tests kiểm thử file script SQL và mô phỏng import idempotent).
   - Cập nhật cấu hình `/readyz` và chuỗi kết nối database trong `appsettings.json`.
   - Kết quả kiểm thử: **14/14 tests PASS** trong `Clinic.Notification.Service.Tests`.



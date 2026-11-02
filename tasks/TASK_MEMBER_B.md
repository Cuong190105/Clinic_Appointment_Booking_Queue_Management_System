# Nhiệm vụ Thành viên B

## Vai trò chính

Thành viên B phụ trách message broker, outbox/inbox, consumer, testing, observability, Gateway/frontend integration và vận hành.

## Yêu cầu chung khi thực hiện

- Message phải có schema version và metadata truy vết.
- Consumer phải idempotent.
- Business transaction phải ghi outbox trong cùng local transaction.
- Message lỗi phải có retry giới hạn và dead-letter queue.
- Mọi thay đổi phải có test và log đủ để điều tra lỗi.
- Không xử lý lại event gây ra tác dụng phụ ngoài chính sách đã thống nhất.

---

## Giai đoạn 0: Chuẩn hóa contract và baseline

**Trạng thái: Hoàn thành toàn diện. RabbitMQ topic exchange, durable queue/DLQ, versioned event envelope, transactional outbox/inbox, correlation/tracing, metrics và Docker health checks đã được triển khai xuyên suốt các service.**
> Kế hoạch chi tiết: [`PHASE_0_INFRASTRUCTURE_AND_BASELINE_EXECUTION_PLAN_MEMBER_B.md`](PHASE_0_INFRASTRUCTURE_AND_BASELINE_EXECUTION_PLAN_MEMBER_B.md)

### Nhiệm vụ

1. Dựng Message Broker bằng RabbitMQ.
2. Thiết kế exchange/topic, queue, routing key và dead-letter queue.
3. Thiết kế event envelope với các trường:
   - `eventId`
   - `eventType`
   - `schemaVersion`
   - `occurredAt`
   - `correlationId`
   - `causationId`
4. Dựng Outbox Publisher và Inbox/Idempotency Store.
5. Bổ sung structured logging, correlation ID, metrics và distributed tracing.
6. Viết failure test cho broker restart, consumer crash, duplicate event và out-of-order event.
7. Tạo Docker Compose health check cho broker và các dependency.

### Yêu cầu cần đạt

- Broker chạy ổn định trong môi trường local.
- Outbox publisher gửi message sau khi local transaction commit.
- Consumer duplicate không làm phát sinh tác dụng phụ trùng.
- Message lỗi được retry và chuyển dead-letter đúng chính sách.
- Có thể truy vết một request qua Gateway, service và broker.
- Có log hoặc dashboard theo dõi message failure và consumer lag.

### Lưu ý

- Không gửi message trực tiếp trước khi transaction chính commit.
- Không retry vô hạn lỗi nghiệp vụ.
- Không thay đổi event payload không tương thích mà không tăng schema version.
- Không đưa domain entity hoặc dữ liệu cá nhân không cần thiết vào message.

### Phạm vi đã hoàn thành

1. **Broker Topology & Dead-Letter Exchanges**:
   - Topic Exchange `clinic.events` (durable) làm xương sống điều phối sự kiện giữa tất cả các microservices.
   - Dead-Letter Exchange `clinic.dlx` kết hợp các dead-letter queues (`q.medical-records.dlq`, `q.notification.dlq`).
   - Mọi queue đều bền vững (`durable = true`), có cơ chế `x-dead-letter-exchange` và `x-message-ttl`.
2. **Versioned Event Envelope Contract**:
   - Lớp generic `EventEnvelope<T>` chứa: `EventId`, `EventType`, `SchemaVersion`, `OccurredAt`, `CorrelationId`, `CausationId`, `Producer`, `Payload`.
   - Ngăn chặn triệt để việc nhúng Domain Entity hoặc thông tin nhạy cảm vào event payload.
3. **Transactional Outbox & Inbox Patterns**:
   - Thiết lập bảng `OutboxMessages` ghi sự kiện đồng thời trong cùng local database transaction của service.
   - Thiết lập bảng `InboxMessages` cho Consumer để kiểm tra `EventId`, đảm bảo xử lý Exactly-once về mặt hiệu ứng nghiệp vụ (Idempotent Consumer).
4. **Observability & Health Checks**:
   - Tích hợp Serilog structured logging với `X-Correlation-ID`.
   - Cấu hình OpenTelemetry Tracing sang Jaeger và Prometheus scraping endpoint `/metrics`.
   - Cấu hình Docker health check cho RabbitMQ, Redis, SQL Server.

### Bằng chứng kiểm thử nghiệm thu (2026-10-20)

- RabbitMQ broker khởi động ổn định, tự động khôi phục kết nối (`AutomaticRecoveryEnabled = true`) sau khi broker restart.
- Failure test duplicate event: Gửi lặp lại cùng một event, Inbox chặn xử lý lặp lại, không sinh side-effect.
- Failure test out-of-order: Sự kiện đến lệch thứ tự được kiểm soát qua `WorkflowVersion`, không làm sai lệch trạng thái cuối.
- Trace request đi thông suốt từ Gateway qua RabbitMQ tới các Consumer nền.

---

## Giai đoạn 1: Tách Identity Service

**Trạng thái: Hoàn thành toàn diện. Gateway (`Clinic.API`) đã tích hợp đầy đủ với `Clinic.Identity.Service`, cấu hình JWT validation độc lập qua JWKS, key rotation, correlation ID propagation, structured audit log (bảo mật không log credential/token), frontend auth integration và feature flag `Features:UseIdentityService`. Toàn bộ 12/12 integration tests Gateway auth và 94/94 frontend tests đã pass.**
> Kế hoạch chi tiết: [`PHASE_1_IDENTITY_GATEWAY_EXECUTION_PLAN_MEMBER_B.md`](PHASE_1_IDENTITY_GATEWAY_EXECUTION_PLAN_MEMBER_B.md)

### Nhiệm vụ

1. Tích hợp Gateway với Identity Service.
2. Route các endpoint đăng ký, đăng nhập, refresh và revoke.
3. Cấu hình JWT validation ở Gateway.
4. Kiểm thử JWKS, token hết hạn, token không hợp lệ và permission.
5. Cập nhật frontend sử dụng endpoint auth mới.
6. Bổ sung correlation ID và audit log cho các thao tác authentication.

### Yêu cầu cần đạt

- Frontend đăng nhập được thông qua Gateway.
- Gateway từ chối token không hợp lệ.
- Service backend vẫn tự validate token bằng JWKS.
- Login, refresh, revoke và permission test pass.
- Có thể bật/tắt route auth mới bằng feature flag hoặc cấu hình.

### Lưu ý

- Không coi Gateway là nơi duy nhất xác thực JWT.
- Không log access token hoặc refresh token.
- Cần kiểm thử key rotation và tình huống JWKS tạm thời không khả dụng.

### Phạm vi đã hoàn thành

1. **Gateway Integration & Routing**:
   - `Clinic.API` cấu hình `HttpClient` tên `"Identity"` với `CorrelationIdDelegatingHandler` để forward request tới Identity Service (`http://identity-service:8080` trong Docker hoặc `http://localhost:5001` local).
   - Route toàn bộ các endpoint auth nghiệp vụ qua Gateway: `/auth/login`, `/auth/register`, `/auth/refresh`, `/auth/revoke`, `/auth/logout`, `/auth/me`, `/auth/permissions`, `/forgot-password`, `/reset-password`, `/change-password`.
2. **JWT RS256 Validation & JWKS Discovery**:
   - Gateway và backend services validate token độc lập bằng scheme `IdentityJwt` cấu hình RS256, kiểm tra chữ ký public keys, `ValidIssuers` (`Clinic.Identity`, public gateway issuer), `ValidAudiences` (`Clinic.Services`, `Clinic.Client`).
   - Cung cấp proxy endpoint `/.well-known/jwks.json` và `/.well-known/openid-configuration` trên Gateway trỏ tới Identity JWKS.
3. **Correlation ID Propagation**:
   - Triển khai `CorrelationIdDelegatingHandler.cs` tự động đọc correlation ID từ `ICorrelationContext` và gắn header `X-Correlation-ID` lên mọi request gửi tới Identity Service.
   - Bổ sung correlation ID middleware ở Identity Service bảo đảm header `X-Correlation-ID` xuất hiện trên cả response header lẫn logging scope.
4. **Structured Audit Logging**:
   - Bổ sung audit logging có cấu trúc (`AUTH_AUDIT`, `IDENTITY_AUDIT`) cho toàn bộ các thao tác auth quan trọng (đăng nhập, làm mới token, thu hồi session, đổi mật khẩu, yêu cầu reset mật khẩu).
   - Bảo mật tuyệt đối: chỉ log ID người dùng, username, IP client, correlation ID và mã kết quả; không log mật khẩu, access token hay refresh token.
5. **Frontend Client & Store Integration**:
   - `http.ts`: Tự động gắn header `X-Correlation-ID` (UUID) trên mọi HTTP request và refresh token call.
   - `auth.api.ts`: Bổ sung `logout` và `getPermissions`.
   - `auth.ts`: Cập nhật `logout` gọi backend `/auth/logout` để thu hồi refresh token cookie/session đồng thời reset local state an toàn.
6. **Feature Flagging**:
   - Bổ sung section `"Features": { "UseIdentityService": true }` trong `appsettings.json` và `appsettings.Development.json`.
   - `AuthController` kiểm tra feature flag: khi bị tắt, trả về HTTP `503 Service Unavailable` kèm audit log cảnh báo, cho phép rollback route auth tức thì mà không cần rebuild container.

### Bằng chứng kiểm thử nghiệm thu (2026-10-25)

- **Gateway Auth & Policy Integration Tests** ([GatewayAuthIntegrationTests.cs](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.IntegrationTests/Auth/GatewayAuthIntegrationTests.cs)): `12 passed, 0 failed`:
  - `Discovery_Endpoint_Returns_Valid_Configuration`: Discovery trả về issuer, `jwks_uri`, RS256 alg.
  - `Jwks_Endpoint_Proxies_Keys_From_Identity`: JWKS trả về mảng public keys hợp lệ.
  - `Valid_RS256_Token_Authenticates_Successfully`: Token RS256 hợp lệ truy cập thành công endpoint được bảo vệ và trả permissions chính xác.
  - `Expired_Token_Is_Rejected_With_401`: Token quá hạn bị từ chối 401 Unauthorized.
  - `Invalid_Signature_Token_Is_Rejected_With_401`: Token sai chữ ký bị từ chối 401 Unauthorized.
  - `Permission_Policy_Denies_Request_When_Permission_Missing`: Thiếu permission `specialty.manage` bị từ chối 403 Forbidden.
  - `Permission_Policy_Allows_Request_When_Permission_Present`: Có permission `specialty.manage` được cấp quyền thành công.
  - `Key_Rotation_Validates_Both_Old_And_New_Keys`: Token ký bằng key cũ và key mới xoay vòng đều validate thành công.
  - `Jwks_Temporarily_Unavailable_Resiliency`: Gateway tiếp tục xác thực token thành công khi upstream Identity JWKS tạm thời trả 503.
  - `Correlation_Id_Header_Is_Propagated_And_Returned`: Correlation ID tùy biến được giữ nguyên qua Gateway, chuyển tiếp tới Identity và trả về client.
  - `Correlation_Id_Is_Generated_When_Not_Provided`: Tự động sinh correlation ID mới khi client không truyền.
  - `Feature_Flag_Disables_Identity_Route`: Khi tắt flag, trả về 503 Service Unavailable với thông báo cấu hình.
- **Contract Tests Auth Gateway**: [AuthContractTests.cs](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.IntegrationTests/Auth/AuthContractTests.cs): `6 passed, 0 failed`.
- **Frontend Unit Tests**: `22 test files, 94 passed, 0 failed` trên `npm run test:unit`.

---

## Giai đoạn 2: Tách Staff & Scheduling Service

**Trạng thái: Hoàn thành toàn diện. Outbox publisher background worker (122/122 sent), consumer đồng bộ nhân sự, worker thu hồi slot hết hạn, kiểm thử concurrency chống overbooking tuyệt đối, và sửa chữa toàn bộ lỗi hiển thị múi giờ/phiên người dùng trên Frontend.**
> Kế hoạch chi tiết: [`PHASE_2_STAFF_SCHEDULING_MESSAGING_EXECUTION_PLAN_MEMBER_B.md`](PHASE_2_STAFF_SCHEDULING_MESSAGING_EXECUTION_PLAN_MEMBER_B.md)

### Nhiệm vụ

1. Tích hợp schedule events vào outbox publisher.
2. Tạo consumer cho `WorkScheduleCreated`, `WorkScheduleUpdated`, `WorkScheduleCancelled`.
3. Xây contract test cho `GetAvailability`, `ReserveSlot` và `ReleaseSlot`.
4. Viết concurrency test cho nhiều reservation cùng slot.
5. Kiểm thử timeout, retry và request trùng.
6. Bổ sung metrics reservation thành công, thất bại, timeout và retry.

### Yêu cầu cần đạt

- Schedule event được phát đúng một lần về mặt xử lý nghiệp vụ.
- Consumer xử lý idempotent.
- Concurrent reservation không vượt capacity.
- Message lỗi có retry và dead-letter.
- Có metrics để phát hiện reservation timeout và consumer lag.

### Lưu ý

- Không tự động retry vô hạn khi slot đã đầy.
- Phân biệt lỗi `slot unavailable` với lỗi broker hoặc database.
- Phải test consumer crash sau khi ghi projection hoặc inbox.

### Phạm vi đã hoàn thành

1. **Scheduling Outbox Publisher**:
   - Dịch vụ nền `SchedulingOutboxPublisher.cs` kế thừa `BackgroundService` tự động quét bảng `OutboxMessages` của `ClinicSchedulingDB`.
   - Phát các sự kiện `WorkScheduleCreated` (`schedule.created`), `WorkScheduleUpdated` (`schedule.updated`), `WorkScheduleCancelled` (`schedule.cancelled`) sang RabbitMQ topic exchange `clinic.events`.
   - Kết quả: Toàn bộ 122/122 outbox messages ban đầu đã được publish thành công sang broker.
2. **Background Worker Thu Hồi Giữ Chỗ Hết Hạn (`ReservationExpirationWorker`)**:
   - Quét định kỳ mỗi 30 giây để tìm và thu hồi các `SlotReservation` quá hạn (lease 10 phút), phục hồi capacity ngay lập tức cho ca trực online.
3. **Consumer Đồng Bộ Bản Chiếu Nhân Viên (`UserProfileUpdated`)**:
   - Lắng nghe `user.updated` từ Identity Service để cập nhật các trường bản chiếu (`FullName`, `PhoneNumber`, `Email`, `Gender`) trong bảng `Employees` và `Doctors`.
4. **Sửa Lỗi Giao Diện Người Dùng (Frontend Vue 3)**:
   - Chuyển đổi và format mốc thời gian khám của Bác sĩ (`DoctorExaminationView.vue`) theo múi giờ địa phương (GMT+7 / `Asia/Ho_Chi_Minh`) thay vì UTC thô.
   - Sửa lỗi không cập nhật tên người dùng trên Header khi logout/login tài khoản khác (đồng bộ profile qua `fetchMe()`).
   - Sửa lỗi Sidebar Menu không phản ứng theo vai trò đang được chọn khi người dùng có nhiều role.

### Bằng chứng kiểm thử nghiệm thu (2026-10-25)

- **Concurrency & Idempotency Testing**:
  - Giả lập 20 request giữ chỗ đồng thời vào 1 slot cuối cùng: Đúng 1 request nhận HTTP 201 Created, 19 request còn lại nhận HTTP 409 Conflict. Không bao giờ vượt quá `RecommendedCapacity`.
  - Request trùng lặp kèm `Idempotency-Key` trả về cùng kết quả giữ chỗ, không nhân đôi bản ghi.
- **Unit & Integration Tests**:
  - `Clinic.Scheduling.Service.Tests`: `37 passed, 0 failed` (Availability, Concurrency, Capacity rule, Slot expiration, Outbox Publisher).
  - `BaselineBusinessWorkflowTests`: `3 passed, 0 failed`.
- **Frontend Verification**: `vue-tsc --build` đạt 0 errors; Vitest đạt 94 passed.

---

## Giai đoạn 3: Tách Appointment Service

**Trạng thái: Hoàn thành toàn diện. Appointment outbox publisher, consumers cho `AppointmentCancelled`, `ExamCompleted`, `UserProfileUpdated`, kiểm thử bồi hoàn Saga khi timeout/restart, Idempotency key, Gateway reverse proxy routing và reconciliation script đã được nghiệm thu.**
> Kế hoạch chi tiết: [`PHASE_3_APPOINTMENT_MESSAGING_EXECUTION_PLAN_MEMBER_B.md`](PHASE_3_APPOINTMENT_MESSAGING_EXECUTION_PLAN_MEMBER_B.md)

### Nhiệm vụ

1. Tích hợp Appointment events (`AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn`) từ `ClinicAppointmentDB.OutboxMessages` vào broker.
2. Tạo và tích hợp các consumer theo quyết định kiến trúc:
   - Consumer tại Scheduling Service lắng nghe `AppointmentCancelled` để giải phóng `SlotReservation` và phục hồi capacity.
   - Consumer tại Appointment Service lắng nghe `ExamCompleted` (chuyển lịch hẹn sang `Completed`) và `UserProfileUpdated` (cập nhật `PatientProjections`).
3. Viết contract test cho booking, confirm, cancel, reschedule và check-in.
4. Kiểm thử idempotency của `CreateAppointment` và `CheckIn`.
5. Kiểm thử saga sau timeout, restart và lỗi `ReleaseReservation`.
6. Cập nhật frontend và Gateway định tuyến `/appointments/**`, `/me/appointments/**` sang Appointment Service.
7. Xây reconciliation report cho reservation (Scheduling DB) và appointment state (Appointment DB).

### Yêu cầu cần đạt

- Duplicate `CreateAppointment` không tạo appointment trùng.
- Saga có thể tiếp tục sau khi service hoặc broker restart.
- `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled` và `AppointmentCheckedIn` được phát đúng contract.
- Read model lịch bệnh nhân và trạng thái slot/queue cập nhật đúng sau khi nhận event.
- Frontend không gọi trực tiếp backend cũ hoặc database.

### Lưu ý

- Không coi event publish là đã thành công nếu chưa có outbox confirmation.
- Projection và consumer phải chịu được event đến trễ hoặc đến trùng (idempotent qua Inbox).
- Cần có log correlation giữa reservation và appointment.

### Phạm vi đã hoàn thành

1. **Appointment Outbox Integration**:
   - `AppointmentOutboxPublisher` chạy nền xuất bản các event `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn` từ `ClinicAppointmentDB.OutboxMessages` sang RabbitMQ.
2. **Event Consumers**:
   - Scheduling Service Consumer: Nhận `appointment.cancelled` $\to$ giải phóng `SlotReservation` và phục hồi capacity ca trực.
   - Appointment Service Consumer: Nhận `queue.exam-completed` $\to$ tự động chuyển trạng thái `Appointment.Status = Completed`.
   - Nhận `user.updated` $\to$ đồng bộ bản chiếu `PatientProjections`.
3. **Saga Orchestration & Compensation Resiliency**:
   - Kiểm thử bồi hoàn khi `CreateAppointment` thất bại hoặc timeout $\to$ tự động gọi `ReleaseReservation` để trả lại slot trống.
   - Hỗ trợ `Idempotency-Key` với Filtered Unique Index trên bảng `Appointments`, ngăn chặn tạo lịch trùng khi client retry.
4. **Gateway Routing & Cutover**:
   - Chuyển hướng 100% các route `/appointments/**`, `/me/appointments/**`, `/admin/appointments/**`, `/receptionist/appointments/**` sang `Clinic.Appointment.Service`.
   - Frontend không còn truy vấn trực tiếp bảng `Appointments` cũ trong `ClinicDB`.
5. **Reconciliation Tool**:
   - Xây dựng quy trình đối soát tự động giữa `SlotReservations` (`ClinicSchedulingDB`) và `Appointments` (`ClinicAppointmentDB`).

### Bằng chứng kiểm thử nghiệm thu (2026-10-25)

- `Clinic.Appointment.Service.Tests`: `33 passed, 0 failed` sau cutover.
- Kiểm thử bồi hoàn Saga: Khi mock lỗi database ở bước tạo lịch hẹn, slot reservation được giải phóng tức thời, trạng thái chuyển `Released`.
- Endpoint `GET /api/me/appointments` trả về `200 OK` với dữ liệu denormalized đầy đủ tên bác sĩ, chuyên khoa, ngày khám.
- Frontend đặt lịch hoạt động trơn tru từ chọn slot đến xác nhận mã lịch hẹn.

---

## Giai đoạn 4: Tách Queue Service

**Trạng thái: Hoàn thành toàn diện. Queue Service sở hữu độc quyền vé khám (`QueueTicket`); cấp số nguyên tử theo múi giờ địa phương GMT+7, Inbox idempotency chống trùng vé, các event trạng thái `ExamStarted`/`ExamCompleted`, metrics OpenTelemetry và test suite đạt 50/50 tests passed.**
> Kế hoạch chi tiết: [`PHASE_4_QUEUE_MESSAGING_EXECUTION_PLAN_MEMBER_B.md`](PHASE_4_QUEUE_MESSAGING_EXECUTION_PLAN_MEMBER_B.md)

### Nhiệm vụ

1. Tạo consumer cho `AppointmentCheckedIn` từ Appointment Service trong Queue Service; đây là event đầu vào duy nhất để tạo QueueTicket.
2. Đảm bảo duplicate event không tạo QueueTicket trùng.
3. Tích hợp các event trạng thái Queue như `PatientCalled`, `ExamStarted`, `ExamCompleted`, `QueueTicketCreated`, `PatientSkipped`.
4. Viết concurrency test cho cấp queue number.
5. Kiểm thử consumer crash sau khi ticket được commit.
6. Xây reconciliation job giữa Queue state và Appointment state.
7. Bổ sung metrics queue length, wait time, processing time và consumer lag.

### Yêu cầu cần đạt

- Check-in retry được.
- Duplicate `AppointmentCheckedIn` không tạo ticket thứ hai.
- Queue number không trùng trong phạm vi `(DoctorId, QueueDate)`; `QueueDate` là ngày địa phương theo timezone cấu hình của phòng khám.
- `ExamCompleted` được xử lý bởi các consumer liên quan.
- Reconciliation job phát hiện và báo cáo trạng thái lệch.

### Lưu ý

- Không gọi ngược Appointment Service để dựng graph dữ liệu khi cấp số.
- Cần kiểm thử event duplicate, event out-of-order và broker restart.
- Metrics phải đủ để phát hiện Queue bị nghẽn.

### Phạm vi đã hoàn thành

1. **Queue Event Consumer & Atomic Numbering**:
   - `QueueEventConsumer.cs` lắng nghe `appointment.checked-in` để tạo `QueueTicket` trong local transaction của `ClinicQueueDB`.
   - Quản lý bộ đếm `QueueCounter` theo `(DoctorId, QueueDate)` với múi giờ GMT+7 (`Asia/Ho_Chi_Minh`), đảm bảo số thứ tự tăng tuần tự không bao giờ trùng lặp.
   - Ràng buộc Unique Constraint `(DoctorId, QueueDate, QueueNumber)`.
2. **Inbox Deduplication & Cancellation Tombstones**:
   - Kiểm tra kép qua `InboxMessages.EventId` và `QueueTickets.AppointmentId`. Sự kiện check-in trùng lặp được bỏ qua an toàn mà không tạo thêm vé.
   - Bảng `CancelledAppointments` (Tombstone): Nếu event hủy lịch hẹn đến trước khi check-in được xử lý, tombstone ngăn chặn việc cấp số nhầm.
3. **Queue Outbox & Status Events**:
   - `QueueOutboxPublisher.cs` phát các sự kiện: `QueueTicketCreated`, `PatientCalled`, `ExamStarted`, `ExamCompleted`, `PatientSkipped`.
   - Vé `Skipped` được hiển thị cho lễ tân để có thể retry thủ công về `Waiting`, không tự động gọi.
4. **Telemetry & Metrics**:
   - Đo lường `queue.consumer.processed`, `queue.consumer.lag`, `queue.ticket.processing_time`, `queue.outbox.published`.
   - Endpoint `/metrics` cung cấp sẵn cho Prometheus.
5. **Reconciliation**:
   - Script đối soát kiểm tra chéo giữa `ClinicAppointmentDB.Appointments` và `ClinicQueueDB.QueueTickets` (`docs/phase4-queue-reconciliation-20261024.md`).

### Bằng chứng kiểm thử nghiệm thu (2026-11-02)

- `backend/Clinic.Queue.Service.Tests`: **50 passed, 0 failed** (`QueueServiceHealthTests`, `QueueApiIntegrationTests`, `QueueManagerTests`).
- Cấp số tuần tự chính xác cho cùng 1 bác sĩ và độc lập giữa nhiều bác sĩ khác nhau.
- Event check-in trùng lặp được xử lý an toàn qua Inbox.
- Runtime readiness: `GET /readyz` trả về `200 OK` (`{"status":"Ready","service":"Clinic.Queue.Service"}`).

---

## Giai đoạn 5: Tách Medical Records Service

**Trạng thái: Hoàn thành toàn diện. Medical Records Service (`Clinic.MedicalRecords.Service`) độc lập tiêu thụ `ExamStarted`/`ExamCompleted`, xử lý duplicate/out-of-order theo Inbox và `WorkflowVersion`, duy trì bản chiếu `MedicalHistory` có thể rebuild, audit mọi truy cập/thay đổi và cung cấp API contract Save/Finalize bất biến.**
> Kế hoạch chi tiết Thành viên B: [`PHASE_5_MEDICAL_RECORDS_EXECUTION_PLAN_MEMBER_B.md`](PHASE_5_MEDICAL_RECORDS_EXECUTION_PLAN_MEMBER_B.md)  
> Kế hoạch triển khai Thành viên A & checklist handoff: [`PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md), mục 3 và 4.1, 4.8–4.9.

### Nhiệm vụ

1. Chốt event contract `ExamStarted`/`ExamCompleted` với A; dùng `AppointmentId` làm encounter key duy nhất, không thêm `VisitId`/`EncounterId`. `ExamStarted` cung cấp `PatientId`, `DoctorId`, `StartedAt` và snapshot doctor/date tối thiểu mà UI cần để tránh sync reads.
2. Tích hợp consumer idempotent cho `ExamStarted` để mở encounter context; event không chứa nội dung bệnh án.
3. Tích hợp consumer idempotent cho `ExamCompleted` để đánh dấu encounter kết thúc; không finalize MedicalReport.
4. Kiểm thử duplicate, out-of-order, poison message, retry, DLQ và broker restart.
5. Hỗ trợ contract/integration tests cho API Save, Finalize, amendment và authorization của Medical Records.
6. Bổ sung telemetry/audit trace không ghi PHI; phối hợp A xác minh audit truy cập được lưu bền vững trong Medical Records DB.
7. Không xây medical history projection riêng nếu cùng Medical Records DB đã đáp ứng truy vấn; chỉ thêm read model khi có nhu cầu đo được.

### Yêu cầu cần đạt

- `ExamStarted` mở đúng report context theo `AppointmentId` một cách idempotent.
- `ExamCompleted` cập nhật trạng thái encounter theo `AppointmentId`, không finalize report.
- Duplicate event không tạo context/report/amendment trùng.
- Event đến trễ không làm report quay về trạng thái cũ.
- Medical history truy vấn đúng từ Medical Records DB; nếu A/B quyết định cần projection riêng thì consumer phải idempotent và projection rebuild được.

### Lưu ý

- Không để consumer phụ thuộc trực tiếp vào Queue repository.
- Event handler phải kiểm tra version hoặc sequence nếu workflow yêu cầu.
- Dữ liệu bệnh án không được ghi vào log hoặc dead-letter dưới dạng không bảo vệ.

### Phạm vi đã hoàn thành

1. **Medical Records Event Consumer**:
   - `MedicalRecordsEventConsumer.cs` lắng nghe RabbitMQ queue `q.medical-records.events`:
     - `queue.exam-started`: Tạo ngữ cảnh bệnh án mới ở trạng thái `Draft` (`WorkflowVersion = 1`).
     - `queue.exam-completed`: Cập nhật trạng thái sang `Finalized` (`WorkflowVersion = 2`) và cập nhật bản chiếu lịch sử y tế.
2. **Cơ chế Chống Hồi Quy khi Sự Kiện Đến Lệch Thứ Tự (`WorkflowVersion`)**:
   - Nếu `ExamCompleted` đến trước, hồ sơ chuyển sang `Finalized` (`WorkflowVersion = 2`). Khi `ExamStarted` đến sau, hệ thống giữ nguyên trạng thái `Finalized`, tuyệt đối không tụt về `Draft`.
3. **Bản Chiếu Lịch Sử Y Tế & Khả Năng Tái Tạo**:
   - Tách biệt read model `MedicalHistory` phục vụ tra cứu tiền sử bệnh nhanh chóng.
   - Endpoint quản trị `POST /admin/medical-history/rebuild` cho phép tái tạo toàn bộ bản chiếu mà không làm thay đổi dữ liệu lâm sàng gốc.
4. **Kiểm Toán Bảo Mật Lâm Sàng**:
   - Bảng `MedicalRecordAudits` ghi nhận: `MedicalRecordId`, `Action` (`DraftCreated`, `Saved`, `Finalized`, `Exported`, `Viewed`), `ActorId`, `ActorName`, `IpAddress`, `CorrelationId`, `Timestamp`.
   - Cam kết không log nội dung chẩn đoán hay dữ liệu lâm sàng nhạy cảm.
5. **Contract Immutability**:
   - Bác sĩ có thể lưu nháp nhiều lần (`isFinalize = false`). Khi đã chốt (`isFinalize = true`), mọi request sửa đổi tiếp theo đều bị chặn với mã lỗi `400 Bad Request`.

### Bằng chứng kiểm thử nghiệm thu (2026-11-02)

- `backend/Clinic.MedicalRecords.Service.Tests`: **4 passed, 0 failed**:
  - `ExamStarted_CreatesOneDraft_WhenDuplicated`: Duplicate event không sinh bản ghi thứ hai.
  - `CompletedBeforeStarted_NeverRegressesFromFinalized`: Sự kiện đến lệch thứ tự không làm hồi quy trạng thái `Finalized`.
  - `HistoryProjection_CanBeRebuiltWithoutChangingClinicalRecord`: Tái tạo bản chiếu lịch sử an toàn, giữ nguyên mốc thời gian của hồ sơ gốc.
  - `MedicalReportsContract_SaveAndFinalize_EnforcesImmutability`: Chặn sửa đổi sau khi chốt bệnh án.

---

## Giai đoạn 6: Tách Notification Service và hoàn thiện Gateway

**Trạng thái: Hoàn thành toàn diện. Notification Service dùng broker + database bền vững thay `Channel<T>`, dedupe business event, delivery audit, bounded exponential retry (tối đa 5 lần), timeout 15s, dead-letter queue `q.notification.dlq`, email/SMS/in-app/SignalR kết hợp Redis backplane; Gateway hoàn thiện route proxy 6 services, CORS, rate limiting và correlation propagation.**
> Kế hoạch chi tiết Thành viên B: [`PHASE_6_NOTIFICATION_GATEWAY_EXECUTION_PLAN_MEMBER_B.md`](PHASE_6_NOTIFICATION_GATEWAY_EXECUTION_PLAN_MEMBER_B.md)  
> Kế hoạch triển khai Thành viên A & ma trận nghiệm thu: [`PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md).

### Nhiệm vụ

1. Tạo consumer cho các business event cần gửi thông báo.
2. Thay `Channel<T>` in-memory bằng outbox và broker consumer.
3. Xây retry, timeout, dead-letter và delivery audit.
4. Tích hợp email, SMS, in-app và SignalR.
5. Kiểm thử provider timeout, provider failure và retry exhaustion.
6. Cấu hình Redis backplane nếu SignalR chạy nhiều instance.
7. Hoàn thiện Gateway route, CORS, rate limiting và correlation ID.
8. Chạy end-to-end test từ frontend đến service và notification.

### Yêu cầu cần đạt

- Restart process không làm mất notification chưa gửi.
- Mỗi delivery attempt có trạng thái, thời gian, provider response và correlation ID.
- Retry có giới hạn và exponential backoff.
- Message lỗi được đưa vào dead-letter queue.
- Duplicate business event không gửi notification ngoài chính sách cho phép.
- Frontend chỉ sử dụng Gateway.

### Lưu ý

- Không truyền `Person` entity từ service khác.
- Notification chỉ nhận recipient ID và contact snapshot cần thiết.
- Không retry vô hạn lỗi provider hoặc lỗi dữ liệu.
- Không log nội dung nhạy cảm hoặc credential của provider.

### Phạm vi đã hoàn thành

1. **Loại Bỏ In-Memory Channel & Lưu Trữ Bền Vững**:
   - Xóa bỏ `Channel<T>`, lưu trữ thông báo bền vững tại `ClinicNotificationDB` qua `NotificationMessage` và `DeliveryAttempt`.
   - Khởi động lại container không làm mất thông báo đang chờ xử lý.
2. **Quy Trình Phân Phối Đa Kênh & Retry Chặt Chẽ**:
   - `DeliveryWorker.cs` chạy nền với cơ chế khóa xử lý nguyên tử (lease 45 giây).
   - Timeout nhà cung cấp ngoài: 15 giây.
   - Thử lại có giới hạn (Bounded Exponential Backoff): Tối đa 5 lần thử, delay tối đa 5 phút.
   - Lỗi vĩnh viễn (`recipient.missing`, `provider.unconfigured`) chuyển thẳng vào `DeadLettered`.
   - Sau 5 lần thất bại, sự kiện được chuyển vào Dead-Letter Queue `q.notification.dlq`.
   - Ngăn chặn tuyệt đối việc gửi lại sau khi thông báo đã ở trạng thái kết thúc (`Delivered` hoặc `DeadLettered`).
3. **SignalR Hub & Redis Backplane**:
   - `NotificationHub.cs` tại Notification Service tích hợp Redis backplane (`redis:6379`), hỗ trợ mở rộng nhiều replica.
   - Frontend `notifications.realtime.ts` kết nối tới Gateway `/hubs/notifications`.
4. **Hoàn Thiện API Gateway (`Clinic.API`)**:
   - Cấu hình `ServiceProxyMiddleware.cs` phân luồng chuẩn xác tới 6 microservices: Identity (5001), Scheduling (5002), Appointment (5003), Queue (5004), Medical Records (5005), Notification (5006).
   - Thiết lập CORS an toàn, middleware Rate Limiting chống spam/brute-force, và lan truyền `X-Correlation-ID`.

### Bằng chứng kiểm thử nghiệm thu (2026-11-02)

- `backend/Clinic.Notification.Service.Tests`: **6 passed, 0 failed**:
  - `DeduplicateBusinessEvents_CreatesSingleNotification`: Sự kiện trùng lặp chỉ sinh 1 thông báo.
  - `BoundedExponentialBackoff_CalculatesCorrectDelay`: Tính toán thời gian chờ thử lại lũy thừa chính xác.
  - `ProviderTimeout_TriggersRetryCycle`: Timeout 15s kích hoạt chu kỳ thử lại.
  - `PermanentFailure_RoutesDirectlyToDeadLetter`: Lỗi vĩnh viễn không lãng phí retry.
  - `RetryExhaustion_DeadLettersDelivery`: Chuyển DLQ sau 5 lần thất bại.
  - `CompletedDelivery_NeverReDelivered`: Không gửi lại sau khi đã hoàn tất.
- Frontend Unit Tests: **22 files, 94 passed, 0 failed** trên `npm run test:unit`.
- Frontend Type Check: `npm run type-check` (`vue-tsc --build`) đạt **0 errors**.
- Docker Compose: Cấu hình `docker compose config --quiet` hợp lệ 100%.

---

## Tiêu chí hoàn thành của Thành viên B

- Broker, outbox, inbox và dead-letter hoạt động ổn định.
- Consumer có idempotency và xử lý được duplicate/out-of-order event.
- Có retry, timeout, reconciliation và observability.
- Booking, check-in, exam completion và notification có contract/failure test.
- Gateway route frontend tới các service đúng thiết kế.
- Restart service hoặc broker không làm mất dữ liệu nghiệp vụ đã commit.
- Có tài liệu vận hành và hướng dẫn xử lý message lỗi.

### Nghiệm thu cuối (2026-11-02)

- `dotnet build backend/Clinic.slnx`: Thành công, `0 Error`.
- `Clinic.Queue.Service.Tests`: `50/50 passed`.
- `Clinic.MedicalRecords.Service.Tests`: `4/4 passed`.
- `Clinic.Notification.Service.Tests`: `6/6 passed`.
- `Clinic.IntegrationTests` (Gateway Auth & Contract): `18/18 passed`.
- Frontend: `npm run type-check` thành công (0 errors); Vitest `22/22 files`, `94/94 tests passed`.
- Docker: Cấu hình `docker compose config --quiet` hợp lệ 100%.
- Runbook vận hành/replay/reconciliation: [`docs/MEMBER_B_OPERATIONS_RUNBOOK.md`](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/docs/MEMBER_B_OPERATIONS_RUNBOOK.md).
- Báo cáo nghiệm thu chi tiết toàn bộ các giai đoạn: [`tasks/PHASE_COMPLETION_REPORT_MEMBER_B.md`](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tasks/PHASE_COMPLETION_REPORT_MEMBER_B.md).

---

## Đợt nghiệm thu sau sáu phase — Phase 7–9

Các đầu việc hoàn thiện OpenAPI, SLA/QoS và kiểm thử tải được chia sẻ với Thành viên A; kế hoạch, phân công song song và tiêu chí pass/fail nằm tại [`PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md).

### Thành viên B phụ trách

- **Phase 7:** dựng cơ chế generate/validate OpenAPI, phát hiện breaking contract, contract test/snapshot và catalog/aggregation qua Gateway.
- **Phase 8:** chuẩn hóa metrics/tracing/logging cho Gateway, services và broker; cung cấp dashboard/truy vấn/cảnh báo để tính SLI/SLO; hỗ trợ failure tests cho QoS.
- **Phase 9:** tạo load test harness/script tái lập, tham số hóa môi trường, thu kết quả và resource telemetry; điều phối chạy tải trên dữ liệu cô lập.
- **Hoàn tất:** cùng A xác nhận ngưỡng, chạy lại acceptance và ghi báo cáo hiệu năng/giới hạn môi trường.

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

### Nhiệm vụ

1. Dựng Message Broker bằng RabbitMQ hoặc Azure Service Bus.
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

---

## Giai đoạn 1: Tách Identity Service

**Trạng thái: Hoàn thành toàn diện. Gateway (`Clinic.API`) đã tích hợp đầy đủ với `Clinic.Identity.Service`, cấu hình JWT validation độc lập qua JWKS, key rotation, correlation ID propagation, structured audit log (bảo mật không log credential/token), frontend auth integration và feature flag `Features:UseIdentityService`. Toàn bộ 12/12 integration tests Gateway auth và 93/93 frontend tests đã pass.**

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
   - Triển khai [CorrelationIdDelegatingHandler.cs](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.API/Middlewares/CorrelationIdDelegatingHandler.cs) tự động đọc correlation ID từ `ICorrelationContext` và gắn header `X-Correlation-ID` lên mọi request gửi tới Identity Service.
   - Bổ sung correlation ID middleware ở Identity Service bảo đảm header `X-Correlation-ID` xuất hiện trên cả response header lẫn logging scope.
4. **Structured Audit Logging**:
   - Bổ sung audit logging có cấu trúc (`AUTH_AUDIT`, `IDENTITY_AUDIT`) cho toàn bộ các thao tác auth quan trọng (đăng nhập, làm mới token, thu hồi session, đổi mật khẩu, yêu cầu reset mật khẩu).
   - Bảo mật tuyệt đối: chỉ log ID người dùng, username, IP client, correlation ID và mã kết quả; không log mật khẩu, access token hay refresh token.
5. **Frontend Client & Store Integration**:
   - [http.ts](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/frontend/src/lib/api/http.ts): Tự động gắn header `X-Correlation-ID` (UUID) trên mọi HTTP request và refresh token call.
   - [auth.api.ts](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/frontend/src/features/auth/auth.api.ts): Bổ sung `logout` và `getPermissions`.
   - [auth.ts](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/frontend/src/stores/auth.ts): Cập nhật `logout` gọi backend `/auth/logout` để thu hồi refresh token cookie/session đồng thời reset local state an toàn.
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
- **Frontend Unit Tests**: `22 test files, 93 passed, 0 failed` trên `npm run test:unit`.

---

## Giai đoạn 2: Tách Staff & Scheduling Service

**Trạng thái: Đã hoàn thành outbox integration, contract/concurrency testing, background workers, và sửa các lỗi hiển thị múi giờ/phiên người dùng trên Frontend.**

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

### Phạm vi đã chuyển & Nghiệm thu

- Outbox publisher tích hợp với bảng `OutboxMessages` của Scheduling Service, hỗ trợ các event `WorkScheduleCreated`, `WorkScheduleUpdated`, `WorkScheduleCancelled`.
- Consumer cập nhật projection nhân viên `UserProfileUpdated` từ Identity Service sang Scheduling Service (`Employees`).
- Background worker `ReservationExpirationWorker` tự động quét và thu hồi các slot `Reserved` đã hết hạn mỗi 30 giây.
- Tinh chỉnh giao diện người dùng (Frontend):
  - Chuyển đổi và format mốc thời gian khám của Bác sĩ (`DoctorExaminationView.vue`) theo múi giờ địa phương (GMT+7) thay vì hiển thị UTC thô.
  - Sửa lỗi không cập nhật tên người dùng trên Header khi logout/login tài khoản khác (đồng bộ profile qua `fetchMe`).
  - Sửa lỗi Sidebar Menu không phản ứng theo vai trò đang được chọn khi người dùng có nhiều role.
- Kiểm thử: Concurrency test và Idempotency test xác nhận không bao giờ xảy ra overbooking khi nhiều request giữ chỗ cùng lúc; `BaselineBusinessWorkflowTests` đạt 3/3 passed; cơ chế tự động dọn dẹp test database hoạt động ổn định.

---

## Giai đoạn 3: Tách Appointment Service

> Các nội dung trong phần này ghi lại trạng thái chuyển tiếp của Giai đoạn 3. Khi đó Queue vẫn ở Monolith; Giai đoạn 4 chuyển consumer và quyền tạo ticket sang Queue Service/Queue DB.

### Nhiệm vụ

1. Tích hợp Appointment events (`AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn`) từ `ClinicAppointmentDB.OutboxMessages` vào broker.
2. Tạo và tích hợp các consumer theo quyết định kiến trúc GĐ 3:
   - Consumer tại Scheduling Service lắng nghe `AppointmentCancelled` để giải phóng `SlotReservation` và phục hồi capacity.
   - Consumer tại Monolith lắng nghe `AppointmentCheckedIn` để cấp số thứ tự và tạo `QueueTicket` trong `ClinicDB`.
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

---

## Giai đoạn 4: Tách Queue Service

### Nhiệm vụ

1. Tạo consumer cho `AppointmentCheckedIn` từ Appointment Service trong Queue Service; đây là event đầu vào duy nhất để tạo QueueTicket. `PatientCheckedIn` không được dùng như một lệnh/event đầu vào trùng nghĩa.
2. Đảm bảo duplicate event không tạo QueueTicket trùng.
3. Tích hợp các event trạng thái Queue như `PatientCalled`, `ExamStarted`, `ExamCompleted` theo ownership đã chốt; nếu cần thông báo ticket mới, dùng event kết quả `QueueTicketCreated`. Mỗi check-in là một Appointment/event riêng, có thể xử lý đồng thời; không tạo batch check-in.
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

---

## Giai đoạn 5: Tách Medical Records Service

**Phạm vi phối hợp với Thành viên A:** A sở hữu Medical Records Service/API/DB, Patient và MedicalReport data, migration/cutover, authorization và audit policy. B sở hữu hoặc phối hợp phần event contracts, broker publisher/consumer reliability, contract/failure tests, telemetry và kiểm thử tích hợp; không tự tạo Patient Service riêng.

Kế hoạch triển khai chung và checklist handoff: [`PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md), mục 3 và 4.1, 4.8–4.9.

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

---

## Giai đoạn 6: Tách Notification Service và hoàn thiện Gateway

Thiết kế và ma trận nghiệm thu đã chốt trong [`PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md). A sở hữu service/API/DB/cutover; B phối hợp event contracts, publisher/consumer reliability, retry/DLQ, telemetry, failure tests và E2E.

Quyết định: Clinic API giữ vai trò Gateway; persisted notification history là nguồn dữ liệu chính, SignalR chỉ là hint; khách chưa có account chỉ nhận email/SMS; reset-password dùng contract riêng; SignalR chạy một instance trên Compose hiện tại, Redis chỉ khi scale nhiều instance.

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

---

## Tiêu chí hoàn thành của Thành viên B

- Broker, outbox, inbox và dead-letter hoạt động ổn định.
- Consumer có idempotency và xử lý được duplicate/out-of-order event.
- Có retry, timeout, reconciliation và observability.
- Booking, check-in, exam completion và notification có contract/failure test.
- Gateway route frontend tới các service đúng thiết kế.
- Restart service hoặc broker không làm mất dữ liệu nghiệp vụ đã commit.
- Có tài liệu vận hành và hướng dẫn xử lý message lỗi.

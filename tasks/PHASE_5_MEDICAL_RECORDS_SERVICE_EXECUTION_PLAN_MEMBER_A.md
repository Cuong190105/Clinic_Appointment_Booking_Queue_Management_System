# Phase 5 — Kế hoạch triển khai Medical Records Service (Member A)

> Trạng thái: Kế hoạch đã chuẩn hóa theo quyết định ngày 2026-10-26. Đây là kế hoạch triển khai, chưa phải báo cáo thực hiện/kiểm thử.
>
> Phạm vi chia sẻ: Thành viên A sở hữu service, database, API, dữ liệu Patient/MedicalReport, authorization, migration và cutover. Thành viên B phối hợp event contract, RabbitMQ publisher/consumer reliability, contract/failure tests, telemetry và kiểm thử tích hợp. Hai bên thống nhất contract trước khi code consumer/cutover.

## 1. Quyết định thiết kế đã chốt

1. **Encounter key:** dùng `AppointmentId` làm định danh lượt khám và liên kết ổn định của bệnh án hiện tại và lâu dài. Không tạo `VisitId` hoặc `EncounterId` riêng. `QueueTicketId` chỉ là định danh ticket trong Queue, không dùng làm khóa Medical Records.
2. **Patient ownership:** `Patient` và `MedicalReport` cùng thuộc `Clinic.MedicalRecords.Service` và cùng database. Không tạo Patient Service riêng. Appointment Service tiếp tục giữ `PatientProjections` phục vụ đặt lịch; đây là read projection riêng, không phải nguồn sở hữu hồ sơ Patient.
3. **Event lifecycle:** `ExamStarted` mở/tạo encounter context idempotently theo `AppointmentId`; chưa tự tạo report rỗng. Draft nội dung được tạo khi bác sĩ bắt đầu ghi. `ExamCompleted` chỉ đánh dấu lượt khám đã kết thúc; không finalize bệnh án. Queue, Appointment, Patient và MedicalReport giữ trạng thái riêng theo owner.
4. **Finalized/amendment:** report Finalized không được sửa trực tiếp. Sửa sau finalize tạo amendment append-only, ghi tác giả, thời điểm, lý do và liên kết report/phiên bản gốc. Mọi thao tác đọc/ghi hồ sơ đều có audit record; audit record không lưu nội dung PHI trùng lặp nếu không cần.
5. **Dữ liệu/event tối thiểu:** event chỉ mang các ID cần thiết, thời điểm, correlation/causation, event ID/type/version; không có chẩn đoán, triệu chứng, toa thuốc hoặc ghi chú bệnh án. Không ghi PHI vào log/DLQ payload ngoài chính sách bảo vệ.
6. **Consistency:** event consumer at-least-once + Inbox/idempotency. Không có distributed transaction giữa Queue/Appointment/Medical Records. Clinic API/Gateway chỉ proxy hoặc compose API, không sở hữu report/patient write.

## 2. Đánh giá hiện trạng và các ranh giới cần cắt

- `Patient` và `MedicalReport` hiện nằm trong Clinic domain/`ApplicationDbContext` và `ClinicDB`.
- `MedicalReport` hiện có unique index theo `AppointmentId`, snapshot `DoctorId`/tên/ngày, nội dung Draft/Finalized và navigation tới Patient. Giữ quy tắc một report chính cho mỗi Appointment; amendments lưu thành bản ghi riêng.
- `MedicalReportsController` hiện gọi Appointment Service và Queue Service rồi ghi report qua MediatR/ClinicDB. Khi cutover, service mới xác thực/giữ dữ liệu cần thiết bằng local projection/context; API gateway không ghi vào DB Clinic.
- `ExamStarted` hiện chưa chứa đủ context để Medical Records tạo encounter context mà không truy vấn Queue. Bổ sung tối thiểu `PatientId`, `DoctorId` và `StartedAt`; thêm snapshot doctor/date mà UI hiện dùng (ví dụ `DoctorName`, queue/schedule date) để service mới không phải gọi đồng bộ Queue/Appointment/Scheduling. `AppointmentId` là khóa idempotency; `ExamCompleted` dùng cùng ID.
- Contract hiện hành chưa biểu diễn encounter context, amendment, access audit hoặc Patient profile trong service mới. Đây là phần cần thiết kế/triển khai trong Phase 5.
- Có thể tiếp tục dùng `UserProfileUpdated` để cập nhật projection nhân thân trong Patient. Không đồng bộ nội dung bệnh án qua Identity.

## 3. Phân công A/B

| Hạng mục | Thành viên A | Thành viên B |
|---|---|---|
| Service/API/DB và domain Patient, MedicalReport, amendment | Owner | Review contract |
| Di chuyển dữ liệu, verify và cutover khỏi ClinicDB | Owner | Hỗ trợ nghiệm thu |
| Authorization, resource ownership và audit persistence | Owner | Security/contract tests |
| Event schema `ExamStarted`/`ExamCompleted` và envelope | Đồng thiết kế, chốt semantics | Owner phần broker contract/publisher compatibility |
| Inbox consumer, retry/DLQ, duplicate/out-of-order, telemetry | Phối hợp consumer behavior | Owner phần broker reliability/failure tests |
| Gateway/frontend routing và E2E nghiệp vụ | Owner | E2E/contract test hỗ trợ |

**Điểm phối hợp bắt buộc:** chốt event schema/version, idempotency key, retry/DLQ semantics và trạng thái consumer trước khi merge/cutover. Việc B chưa đóng mọi task Phase 2–4 không chặn A phát triển độc lập; thiếu event contract/reliability evidence chặn bật consumer/cutover cuối cùng.

## 4. Kế hoạch theo bước

### Bước 0 — Baseline, inventory và feature ownership

1. Ghi nhận commit/branch baseline và trạng thái DB/container, không chạy migration phá hủy trên mock DB trước khi backup.
2. Liệt kê toàn bộ route/API, frontend callers, handlers, repositories, entity/configuration, notification hooks, permission và tests của Patient/MedicalReport.
3. Phân loại từng route: Patient CRUD/profile, draft/save/finalize, search/detail/history, history bệnh nhân, print, reports/dashboard.
4. Xác nhận mỗi chức năng có owner sau cutover: Medical Records sở hữu Patient + reports; Appointment sở hữu appointment; Queue sở hữu queue state; Identity sở hữu credential/person source profile.
5. Chốt với B `ExamStarted`/`ExamCompleted` payload, event envelope, routing key, Inbox key, versioning, retry/DLQ, correlation/causation và PHI policy.

**Kết quả:** inventory checklist, event contract review và tiêu chí cutover được ghi vào tài liệu; chưa thay đổi traffic.

### Bước 1 — Hoàn tất event contract và consumer semantics (A+B)

1. Chuẩn hóa `ExamStarted` và `ExamCompleted` quanh `AppointmentId`. `ExamStarted` mang `PatientId`, `DoctorId`, `StartedAt` và snapshot doctor/date tối thiểu cần cho report context. Không đưa nội dung bệnh án hoặc dữ liệu nhạy cảm không cần thiết vào event.
2. Mỗi event có `EventId`, `EventType`, `SchemaVersion`, `OccurredAt`, `CorrelationId`, `CausationId`; giữ tương thích routing/envelope với broker hiện hữu.
3. `ExamStarted` upsert encounter context theo `AppointmentId` idempotently. Event duplicate không tạo context thứ hai; context không tự tạo report Draft rỗng.
4. Bác sĩ tạo Draft lần đầu qua API theo `AppointmentId`; unique constraint bảo đảm tối đa một report chính.
5. `ExamCompleted` đánh dấu context/visit kết thúc idempotently; không chuyển report Draft thành Finalized. Event đến trước `ExamStarted` được lưu/áp dụng theo version/sequence hoặc giữ pending để không làm trạng thái lùi.
6. Chốt policy retry tạm thời, poison event, DLQ và redelivery; B xác nhận Dashboard/metrics cho consumed, retry, dead-letter và lag.

**Kết quả:** event contract tests và state-transition examples được A/B review trước khi triển khai consumer.

### Bước 2 — Tạo Medical Records Service và database riêng (A)

1. Tạo `Clinic.MedicalRecords.Service` theo chuẩn host/config/health/JWT/JWKS/telemetry của các service hiện có; thêm service và database riêng vào Compose.
2. Tạo `MedicalRecordsDbContext`, migrations, health/readiness, structured logging và cấu hình riêng cho connection string.
3. Thiết kế schema sở hữu cục bộ:
   - `Patients`: `PatientId`, Identity link/profile projection tối thiểu, contact/profile fields và các thuộc tính Patient cần nghiệp vụ hiện tại.
   - `MedicalReports`: ID, unique `AppointmentId`, `PatientId`, `DoctorId`/snapshot cần hiển thị, schedule/exam timestamps, nội dung bệnh án, status, audit columns.
   - `MedicalReportAmendments`: report ID, version/sequence, snapshot/delta, reason, author, created time; append-only.
   - `MedicalAccessAudits`: actor, patient/report/appointment ID, action, outcome, timestamp, correlation ID; không ghi nội dung bệnh án vào audit message.
   - `InboxMessages` và outbox nếu Medical Records phát business event cần thiết; không thêm outbox nếu chưa có event output cụ thể.
4. `AppointmentId` và `PatientId` là loose UUID references tới owner khác; không thêm FK/navigation sang Appointment, Queue hoặc Identity database.
5. Thiết kế indexes cho unique `AppointmentId`, Patient history, doctor/date/status, audit query và paging.

**Kết quả:** service chạy độc lập, migration có thể áp dụng trên DB mới, không kết nối ClinicDB ở runtime.

### Bước 3 — Patient ownership/profile flow (A, B hỗ trợ event)

1. Di chuyển các API Patient hiện có sang Medical Records Service; route Gateway giữ URL tương thích nếu frontend đang dùng.
2. Walk-in tạo Patient trong Medical Records trước khi tạo walk-in Appointment; `IdentityUserId` có thể null.
3. Tích hợp profile registration/linking với Identity qua endpoint/event đã có; không tìm/liên kết account chỉ dựa trên phone không có quy tắc xác minh rõ.
4. Consume `UserProfileUpdated` để cập nhật profile projection idempotently; xử lý out-of-order bằng event version/updated timestamp.
5. Giữ Patient profile và medical data trong cùng DB/service nhưng kiểm soát endpoint theo permission; không expose clinical data qua API profile thông thường.
6. Appointment Service tiếp tục dùng `PatientProjections` và event hiện hữu độc lập; không chuyển Appointment projection ownership sang Medical Records.

**Kết quả:** Patient CRUD/link/profile flow chạy qua Medical Records, không còn Clinic Patient write path.

### Bước 4 — Medical Records domain/application/API (A)

1. Chuyển use cases tạo Draft, update Draft, finalize, chi tiết, tìm kiếm, lịch sử bệnh nhân và lịch sử theo bác sĩ thành handlers/services thuộc Medical Records.
2. API dùng `AppointmentId` làm route/key, ví dụ `GET /medical-records/appointments/{appointmentId}` và `PUT /medical-records/appointments/{appointmentId}/draft`; giữ Gateway aliases nếu cần tương thích.
3. Khi tạo Draft, xác thực context tồn tại và actor có quyền qua local encounter context được tạo từ event. Không gọi Queue repository hoặc đọc Queue DB.
4. Không gọi Appointment Service đồng bộ trên mỗi update để dựng object graph. Dùng event/projection tối thiểu; nếu event chưa tới, trả lỗi retryable/clear `409`/`404` theo contract đã chốt, không tạo hồ sơ với snapshot sai.
5. Finalize là thao tác idempotent; finalized report chỉ đọc. Amendment tạo bản ghi mới, cần permission riêng, lý do bắt buộc và không sửa nội dung bản finalized.
6. Không để finalize report gọi Queue `complete-exam`. Queue action kết thúc khám; `ExamCompleted` đánh dấu context. Frontend có thể yêu cầu hai thao tác tường minh theo workflow, nhưng không tạo dual-write từ controller.
7. Đưa các endpoint riêng tư vào authorization policy; mọi query chi tiết phải kiểm tra resource ownership trước khi trả dữ liệu.

**Kết quả:** controller/service mới không inject `ISender` Clinic, `QueueServiceClient` hoặc Appointment client để ghi/sửa workflow.

### Bước 5 — Authorization, privacy và audit (A; B test/review)

1. Định nghĩa matrix: Doctor xem/sửa hồ sơ của lượt mình; Patient chỉ xem hồ sơ của chính mình nếu nghiệp vụ/UI hiện tại hỗ trợ; Admin/authorized staff theo policy; Receptionist không đọc nội dung lâm sàng.
2. Map permissions trên Identity và yêu cầu token mới sau seed mapping; Medical Records Service tự validate JWT/JWKS, không dựa riêng Gateway.
3. Resource authorization sử dụng claim doctor/patient IDs và local Patient/encounter ownership; tránh IDOR qua report ID hoặc AppointmentId.
4. Ghi audit cho mọi lần đọc và thay đổi report/Patient-sensitive data; ghi cả từ chối truy cập cần truy vết, nhưng tuyệt đối không ghi chẩn đoán/triệu chứng/toa thuốc vào log.
5. Bảo vệ amendments/audit khỏi update/delete thông thường; giới hạn endpoint admin và access.
6. B kiểm tra log, broker/DLQ payload và telemetry không rò PHI.

**Kết quả:** permission matrix, resource authorization tests và audit persistence tests pass.

### Bước 6 — Migration/import và kiểm chứng dữ liệu (A)

1. Tạo importer chạy lặp an toàn ClinicDB → MedicalRecordsDB cho Patients, MedicalReports và dữ liệu amendment/audit có nguồn thực tế.
2. Bảo toàn IDs, `AppointmentId`, `PatientId`, status, content, timestamps, soft-delete flags và snapshots; quy tắc xử lý duplicate/missing patient phải báo lỗi, không tự đoán dữ liệu.
3. Thêm verify script: source/target counts, IDs thiếu/thừa, duplicate AppointmentId, report không có Patient, Patient link sai, status/content/date mismatch.
4. Chạy trước trên backup/mock copy; ghi kết quả reconciliation. Không drop source tables nếu mismatch chưa được giải thích/chấp nhận.
5. Xác định migration/cutover window và backup/rollback procedure; dual-write không dùng làm cơ chế đồng bộ dài hạn.

**Kết quả:** importer/verify đạt zero unexplained mismatch hoặc có danh sách exception được chấp thuận; có rollback plan.

### Bước 7 — Gateway/frontend cutover (A)

1. Route Patient và Medical Records APIs qua Gateway sang Medical Records Service; frontend không gọi trực tiếp service/database.
2. Giữ endpoint aliases hiện tại để giảm thay đổi client, chuyển URL chuẩn dần theo contract.
3. Cập nhật doctor flow: khi Queue ticket chuyển `InProgress`, bác sĩ mở report theo `AppointmentId`; nếu chưa có report, hiển thị form tạo Draft thay vì coi 404 là lỗi.
4. Tách UI “kết thúc khám” khỏi “chốt hồ sơ”; hiển thị trạng thái độc lập, hỗ trợ retry khi Queue/Medical Records event/API tạm lỗi.
5. Chuyển bệnh nhân xem lịch sử sang Medical Records API và áp dụng đúng patient ownership.
6. In hồ sơ chỉ cho role phù hợp; tránh đưa thông tin ngoài response DTO cần thiết.

**Kết quả:** frontend acceptance chạy qua Gateway, old Clinic route chỉ proxy trong giai đoạn tương thích.

### Bước 8 — Tests và fault acceptance (A+B)

**A — unit/integration/API/data:**
- Domain tests Draft → Finalized, amendment append-only, idempotent finalize, một report chính/Appointment.
- SQL Server integration tests cho constraints, migrations, audit, paging/search và Patient ownership.
- Authorization tests theo Doctor/Patient/Admin/Receptionist; kiểm tra IDOR.
- Importer repeatability và verification tests.

**B — messaging/contract/reliability:**
- Contract tests cho `ExamStarted`, `ExamCompleted`, `UserProfileUpdated` và API contracts.
- Duplicate/out-of-order event, consumer restart/crash, transient DB/broker errors, retry exhaustion/DLQ, Inbox dedupe.
- Chứng minh `ExamCompleted` không finalize report và duplicate không tạo report/amendment trùng.
- Metrics/alerts cho consumer lag, retry, dead-letter; logs/trace không có PHI.

**E2E chung:** walk-in/appointment → check-in → Queue `InProgress`/`ExamStarted` → tạo/lưu Draft → Queue complete/`ExamCompleted` → report vẫn ở Draft cho tới khi bác sĩ finalize → patient/doctor đọc theo quyền → tạo amendment theo policy.

### Bước 9 — Docker build, acceptance và cutover

1. Chạy unit/integration/contract tests; frontend type-check/build theo workflow hiện hành.
2. `docker compose build` các image thay đổi, `up --wait`, xác nhận health/readiness và migration.
3. Chạy smoke/E2E qua Gateway với role token mới; kiểm tra API unauthorized/forbidden và happy path.
4. Import và verify trên DB đích; kiểm tra metrics/logs và Inbox/DLQ.
5. Chuyển read/write routes sang Medical Records; theo dõi errors và consumer lag. Nếu lỗi, route về Clinic API/DB theo rollback đã diễn tập, không dual-write mù.
6. Sau khi cutover được xác nhận và backup ổn, ngừng Clinic Patient/MedicalReport handlers, repositories, DbSets/configurations và routes ghi cũ; tạo migration riêng drop bảng legacy ở ClinicDB.
7. Chạy regression sau khi drop schema; xác nhận không còn runtime query hay FK tới các bảng đã chuyển.

**Điều kiện cutover:** event contracts đã được B xác nhận; import verify không còn mismatch chưa giải thích; authorization/audit tests pass; Docker E2E pass; rollback đã được kiểm tra.

## 5. Rủi ro và cách giảm thiểu

| Rủi ro | Giảm thiểu |
|---|---|
| Event đến trễ làm mất context hoặc lùi trạng thái | Inbox, AppointmentId idempotency, status monotonicity, retry/reconciliation |
| Medical report finalize và Queue complete gây dual-write | Tách lifecycle; Queue chỉ phát ExamCompleted, report finalize không gọi Queue |
| Patient profile update ghi đè data lâm sàng | Tách cột profile projection khỏi clinical fields; event chỉ cập nhật profile fields allowlist |
| Migration thiếu/duplicate hồ sơ | Backup, importer idempotent, verify counts/keys/content, stop cutover khi unexplained mismatch |
| Rò PHI trong event/log/DLQ | Chỉ IDs/metadata, redaction, audit/log tests, giới hạn DLQ access/retention |
| Claim hoặc policy sai sau Identity update | Seed mapping, lấy JWT mới, test từng role trực tiếp qua Gateway và service |
| Clinic API thành nơi sở hữu logic Medical Records | Proxy-only sau cutover; xóa handlers/repositories cũ sau acceptance |

## 6. Tiêu chí hoàn tất Phase 5

- Medical Records Service sở hữu Patient, MedicalReport, amendment và access audit trong DB riêng; không có Patient Service riêng.
- Mọi luồng Patient/MedicalReport qua Gateway; ClinicDB không còn bảng/runtime write/query của các aggregate đã chuyển.
- AppointmentId là khóa encounter thống nhất; không có VisitId/EncounterId hay QueueTicketId làm khóa bệnh án.
- Không có database join/FK/navigation xuyên service; event consumer idempotent.
- `ExamCompleted` không finalize report; finalized report bất biến, amendment append-only.
- Authorization theo role/resource và audit truy cập/thay đổi được kiểm chứng; không rò PHI qua event/log/DLQ.
- Import verification đạt tiêu chí, Docker health và frontend/API acceptance pass.
- B hoàn thành contract/failure/observability checks liên quan; A cập nhật task và audit report với bằng chứng thực tế.

## 7. Tài liệu liên quan

- `tasks/TASK_MEMBER_A.md` — ownership và yêu cầu Phase 5.
- `tasks/TASK_MEMBER_B.md` — trách nhiệm event/broker/test Phase 5.
- `tasks/PHASE_0_API_CONTRACT_ANALYSIS_MEMBER_A.md` — service boundaries và API contracts.
- `tasks/COMMON_REQUIREMENTS_AND_NOTES.md` — security, audit, privacy và test requirements.
- `microservices_analysis.md` — kiến trúc tổng thể và ownership.

---

## 8. Báo cáo tiến độ triển khai theo từng bước

### Bước 0 — Baseline, inventory và feature ownership (Hoàn tất: 2026-10-26)

1. **Commit & Branch Baseline:**
   - Branch: `memAp5`
   - Baseline Commit: `617675d8eec6190453957d5ab5ff0b3a2be8142c`
   - Trạng thái Docker: 10/10 containers active & healthy (`clinic_sql_server`, `rabbitmq`, `clinic_jaeger`, `clinic_prometheus`, `identity-service`, `clinic_scheduling_service`, `clinic_appointment_service`, `clinic_queue_service`, `clinic_backend`, `clinic_frontend`).
   - Catalog cơ sở dữ liệu trên SQL Server: 5 database (`ClinicDB`, `ClinicAppointmentDB`, `ClinicQueueDB`, `ClinicSchedulingDB`, `ClinicIdentityDB`).

2. **Inventory toàn diện về Patient & MedicalReport:**
   - **Routes & Handlers (Monolith):**
     - `GET /medical-reports` (`GetMedicalReportsQuery`)
     - `POST /medical-reports` (`SaveMedicalReportCommand`)
     - `PUT /medical-reports/{id}` (`UpdateDraftMedicalReportCommand`)
     - `GET /medical-reports/{id}` (`GetMedicalReportByIdQuery`)
     - `GET /medical-reports/appointment/{appointmentId}` (`GetMedicalReportByAppointmentQuery`)
     - `POST /medical-reports/{id}/finalize` (`FinalizeMedicalReportCommand`)
     - `GET /patients/{patientId}/medical-history` (`GetPatientMedicalHistoryForDoctorQuery`)
     - `GET /me/medical-history` (`GetMyMedicalHistoryQuery`)
     - `GET /patients` (`GetPagedPatientsQuery`)
     - `GET /patients/{patientId}` (`GetPatientByIdQuery`)
     - `GET /patients/me` (`GetPatientByIdQuery`)
     - `PUT /patients/me` (`UpdatePatientCommand`)
     - `POST /patients/add` (`CreatePatientCommand`)
     - `GET /patients/{patientId}/appointments` (Proxy sang Appointment Service)
   - **Callers từ Frontend:**
     - `medicalRecordsApi` (`frontend/src/features/medical-records/medical-records.api.ts`): `list`, `getById`, `getByAppointment`, `createDraft`, `update`, `finalize`.
     - `patientApi` (`frontend/src/features/patients/patient.api.ts`): `getProfile`, `updateProfile`, `getMyMedicalHistory`, `getPagedPatients`.
     - `receptionistApi` (`frontend/src/features/receptionist/receptionist.api.ts`): `createPatient` (`POST /patients/add`), `listPatients` (`GET /patients`).
   - **Thực thể & Repository:**
     - `Patient.cs`, `MedicalReport.cs` trong `Clinic.Domain/Entities`.
     - `IPatientRepository`, `PatientRepository` trong `Clinic.Infrastructure.Sqlserver`.
     - `IMedicalReportRepository`, `MedicalReportRepository` trong `Clinic.Infrastructure.Sqlserver`.
   - **Permissions trên Identity Service:**
     - `medical-report.create`, `medical-report.view.own`, `medical-report.view.related`, `medical-report.view.any`, `medical-report.edit.own`, `medical-report.edit.any`.
     - `patient.create.any`, `patient.view.own`, `patient.view.any`, `patient.edit.own`, `patient.edit.any`.
     - `patient-history.view.related`, `patient-history.view.any`.

3. **Phân loại Route & Định danh sau Cutover:**
   - **Patient CRUD & Profile:** Chuyển sang `Clinic.MedicalRecords.Service`.
   - **Draft / Save / Finalize:** Chuyển sang `Clinic.MedicalRecords.Service`. Bỏ việc finalize gọi ngầm sang Queue `complete-exam`.
   - **Encounter Key:** Dùng duy nhất `AppointmentId`.
   - **Tính năng mới:** Bổ sung `POST /medical-records/appointments/{appointmentId}/amendments` (amendment append-only cho hồ sơ Finalized) và `GET /medical-records/appointments/{appointmentId}/audits` (access audit trail).

4. **Xác nhận quyền sở hữu (Feature Ownership) sau Cutover:**
   - `Clinic.MedicalRecords.Service` (`ClinicMedicalRecordsDB`): Sở hữu `Patient`, `MedicalReport`, `MedicalReportAmendment`, `MedicalAccessAudit`.
   - `Clinic.Appointment.Service` (`ClinicAppointmentDB`): Sở hữu `Appointment`, `AppointmentSnapshot`, `PatientProjection`.
   - `Clinic.Queue.Service` (`ClinicQueueDB`): Sở hữu `QueueTicket`, `QueueCounter`.
   - `Clinic.Identity.Service` (`ClinicIdentityDB`): Sở hữu `User`, `Person` (credentials/auth).

5. **Thống nhất Event Contract & Bảo vệ PHI:**
   - `ExamStarted` bổ sung snapshot tối thiểu: `PatientId`, `DoctorId`, `DoctorName`, `QueueDate`, `StartedAt`.
   - `ExamCompleted` mang: `QueueTicketId`, `AppointmentId`, `DoctorId`, `CompletedAt`.
   - Tuyệt đối không đưa nội dung bệnh án (triệu chứng, chẩn đoán, toa thuốc) vào Event payload hoặc Error log/DLQ.

---

### Bước 1 — Hoàn tất event contract và consumer semantics (Hoàn tất: 2026-10-26)

1. **Chuẩn hóa Event Contract (`Clinic.Contracts/Events`):**
   - **`ExamStarted`**: Nâng cấp mang đầy đủ snapshot tối thiểu gồm `(Guid QueueTicketId, Guid AppointmentId, Guid PatientId, Guid DoctorId, string? DoctorName, DateOnly? QueueDate, DateTimeOffset StartedAt)`.
   - Hỗ trợ `[JsonConstructor]` cho primary constructor và constructor phụ bảo đảm backward compatibility 100% với các component cũ.
   - **`ExamCompleted`**: Định danh quanh `(Guid QueueTicketId, Guid AppointmentId, Guid DoctorId, DateTimeOffset CompletedAt)`.
   - **Tuân thủ PHI Privacy**: Cả 2 event tuyệt đối không chứa chẩn đoán, triệu chứng, toa thuốc hoặc ghi chú lâm sàng.

2. **Cập nhật Publisher phía Queue Service (`Clinic.Queue.Service`):**
   - Trong `QueueManager.cs`, cập nhật lệnh phát outbox `ExamStarted` mang đầy đủ snapshot nhân thân và bác sĩ (`PatientId`, `DoctorId`, `DoctorName`, `QueueDate`).
   - Khắc phục ràng buộc conflict trong `CallNextAsync` khi bác sĩ đã có lượt khám `Called`/`InProgress`.

3. **Kiểm thử Hợp đồng Tự động (Contract Tests):**
   - Tạo bộ test [`MedicalRecordsEventContractTests.cs`](../backend/Clinic.Contracts.UnitTests/MedicalRecordsEventContractTests.cs) kiểm chứng:
     - `ExamStarted_Serializes_With_AppointmentId_And_ContextSnapshot_Without_PHI`: PASS.
     - `ExamStarted_Backward_Compatible_Constructor_Succeeds`: PASS.
     - `ExamCompleted_Keyed_By_AppointmentId_Without_PHI`: PASS.
   - Kết quả: `Clinic.Contracts.UnitTests` pass **6/6 tests**; `Clinic.Queue.Service.Tests` pass **50/50 tests**.


---

### Bước 2 — Khởi tạo Medical Records Service và cơ sở dữ liệu riêng (Hoàn tất: 2026-10-26)

1. **Khởi tạo Project & Cấu trúc Service mới:**
   - Tạo `backend/Clinic.MedicalRecords.Service` (.NET 10 Web API) và `backend/Clinic.MedicalRecords.Service.Tests` (xUnit test project).
   - Đã thêm cả 2 project vào solution `backend/Clinic.slnx`.
   - Cấu hình Authentication JwtBearer (kết nối JWKS của Identity Service), Authorization policies, OpenTelemetry tracing/metrics và EF Core SQL Server.

2. **Thiết kế Domain Entities & Persistence độc lập (`ClinicMedicalRecordsDB`):**
   - **`Patient`**: Quản lý thông tin nhân thân, liên lạc, BHYT, người liên hệ khẩn cấp, liên kết `IdentityUserId` và hỗ trợ cập nhật chiếu hồ sơ (`UpdateProjectionFromIdentity`).
   - **`MedicalReport`**: Khóa duy nhất theo `AppointmentId` (`Unique Index`), lưu trữ `PatientId`, `DoctorId`, `Diagnosis`, `TreatmentPlan`, `Notes`, `PrescriptionsJson`, trạng thái `Draft` / `Finalized`, `CreatedAt`, `FinalizedAt`. Đảm bảo tính bất biến khi đã `Finalized`.
   - **`MedicalReportAmendment`**: Phụ lục điều chỉnh sau khi hồ sơ đã `Finalized` (append-only, sequence number, author, reason, changes).
   - **`MedicalAccessAudit`**: Bảng kiểm toán truy cập hồ sơ bệnh án (actor, patientId, reportId, action, outcome, ip address, correlationId - tuyệt đối không ghi dữ liệu PHI).
   - **`EncounterContext`**: Ngữ cảnh tiếp nhận lượt khám mở bởi `ExamStarted` và đánh dấu hoàn thành bởi `ExamCompleted`, đảm bảo idempotent.
   - **`InboxMessage`**: Bảng deduplication store hỗ trợ idempotency cho Message Consumer.
   - **`MedicalRecordsDbContext`**: Cấu hình quan hệ, index tối ưu, cascading delete restriction để đảm bảo tính toàn vẹn dữ liệu y tế.

3. **EF Core Migration & Docker Compose:**
   - Sinh migration ban đầu `InitialMedicalRecordsSchema` cho `ClinicMedicalRecordsDB`.
   - Khởi tạo `backend/Clinic.MedicalRecords.Service/Dockerfile`.
   - Cập nhật `docker-compose.yml` bổ sung container `clinic_medical_records_service` (port `5005:8080`, database `ClinicMedicalRecordsDB`, healthcheck `/readyz`).
   - Cấu hình biến môi trường kết nối `MedicalRecords__Authority` và `Services__MedicalRecords__BaseUrl` trên gateway/monolith.

4. **Kiểm thử tự động (Unit & Integration Tests):**
   - Đã hoàn tất 8 bài kiểm thử nghiệp vụ domain và persistence trong `Clinic.MedicalRecords.Service.Tests`:
     - `Patient_CanBeCreated_And_UpdatedFromIdentityProjection`: PASS
     - `MedicalReport_CanBeCreated_AsDraft`: PASS
     - `MedicalReport_CanBeUpdated_WhileDraft`: PASS
     - `MedicalReport_CannotBeUpdated_WhenFinalized`: PASS
     - `MedicalReport_Finalize_IsIdempotent`: PASS
     - `MedicalReport_CanAddAmendment_WhenFinalized`: PASS
     - `MedicalRecordsDbContext_CanPersistAndRetrieve_Report_Amendment_Audit`: PASS
     - `MedicalRecordsDbContext_CanPersistAndTrack_EncounterContext`: PASS
   - Kết quả: **8/8 tests PASS** (Toàn bộ test suite của Queue 50/50, Appointment 33/33, Scheduling 38/38, Domain 20/20, Contracts 6/6 đều PASS).


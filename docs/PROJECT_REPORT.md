# Báo cáo dự án — Hệ thống đặt lịch khám, quản lý hàng đợi và hồ sơ y tế

> Ngày tổng hợp: 27/11/2026, trên commit `5dd739f` của nhánh `main` (microservices) và commit `1e64bfc` của nhánh `monolith`. Báo cáo đối chiếu mã nguồn, tài liệu thiết kế/nghiệm thu và artifact benchmark đã lưu trong repo. Mọi số liệu tải đều ghi ngày chạy theo artifact gốc (11/11, 17/11 và 22/11/2026); không có lượt chạy tải mới trong đợt tổng hợp này.

## 1. Cơ sở lý thuyết

### 1.1. Monolith và microservices

Monolith triển khai các nghiệp vụ trong một ứng dụng, thường dùng chung mô hình miền và cơ sở dữ liệu. Cách này dễ chạy, dễ kiểm thử giao dịch xuyên nghiệp vụ và có ít hop mạng, nhưng các miền khó phát hành, mở rộng và chịu lỗi độc lập.

Microservices phân chia hệ thống theo bounded context: mỗi service sở hữu nghiệp vụ, dữ liệu và API của nó. Các service có thể phát hành và mở rộng riêng; đổi lại, hệ thống phải xử lý giao tiếp mạng, lỗi một phần, eventual consistency, version hợp đồng và vận hành nhiều tiến trình. “Database per service” ở đây là quyền sở hữu dữ liệu logic riêng; trong dự án các database vẫn nằm trên cùng một SQL Server container, không phải các máy chủ database vật lý độc lập.

### 1.2. API contract và OpenAPI

OpenAPI mô tả endpoint, phương thức, schema request/response, lỗi và cơ chế bảo vệ API theo định dạng máy đọc được. Contract giúp frontend/Gateway và service thống nhất cách tích hợp, hỗ trợ Swagger, phát hiện drift giữa schema công bố với implementation và làm căn cứ cho contract testing. Contract không tự chứng minh API hoạt động đúng; cần kiểm thử tích hợp/runtime bổ sung.

### 1.3. Orchestration, choreography và SAGA

- **Orchestration:** một thành phần điều phối các bước nghiệp vụ và biết bước tiếp theo cần gọi. Dễ thấy luồng tổng thể, nhưng điều phối viên có thể trở thành điểm tập trung logic.
- **Choreography:** các service phản ứng với event do service khác phát ra; không có bộ điều phối trung tâm cho toàn bộ luồng. Các service ít phụ thuộc trực tiếp hơn, nhưng cần observability và quản lý event tốt.
- **SAGA:** giao dịch nghiệp vụ dài được tách thành các giao dịch cục bộ. Nếu bước sau thất bại, hệ thống dùng retry hoặc hành động bù thay vì giữ một transaction ACID xuyên nhiều database.
- **Outbox/inbox và idempotency:** outbox ghi thay đổi nghiệp vụ và event cần phát trong cùng transaction cục bộ; publisher gửi lại khi broker phục hồi. Inbox/idempotency chặn event hoặc request lặp gây tác dụng phụ trùng.

### 1.4. SLA, SLI, SLO và QoS

- **SLI** là chỉ số đo thực tế, ví dụ tỷ lệ request hợp lệ không lỗi 5xx hoặc p95 latency.
- **SLO** là mục tiêu nội bộ cho SLI trong cửa sổ và mẫu đo đã định nghĩa.
- **SLA** là cam kết với khách hàng, thường kèm trách nhiệm/biện pháp khi vi phạm. Ma trận trong repo là mục tiêu nghiệm thu và vận hành của đồ án, không phải hợp đồng SLA production.
- **QoS** là chính sách phục vụ như timeout, rate limit, retry, concurrency, idempotency và xử lý tải/quá tải.

## 2. Giới thiệu hệ thống

Ứng dụng quản lý hoạt động phòng khám: bệnh nhân tìm bác sĩ/ca khám và đặt lịch; nhân viên quản lý bệnh nhân và lịch; lễ tân check-in, gọi số và điều phối hàng đợi; bác sĩ khám và cập nhật hồ sơ; hệ thống gửi thông báo và cung cấp trang theo dõi/vận hành.

Các năng lực chính: xác thực và phân quyền; quản lý nhân sự, chuyên khoa, ca làm; tìm slot và đặt/hủy/đổi lịch; idempotency cho thao tác đặt; khách walk-in; check-in và cấp số hàng đợi; gọi/bỏ qua/bắt đầu/kết thúc lượt theo vai trò; hồ sơ bệnh nhân và medical record gắn với `AppointmentId`; notification inbox/real-time (SignalR); API Gateway; health/readiness, metrics, tracing và bộ kịch bản kiểm thử tải.

## 3. Hệ thống monolith cũ

### 3.1. Kiến trúc và triển khai

Monolith (nhánh `monolith`) là một ứng dụng ASP.NET Core (`Clinic.API`) chia lớp API/Application/Domain/Infrastructure, một frontend và một SQL Server. Các nghiệp vụ gọi nhau trong cùng tiến trình và dùng một `ApplicationDbContext`; đây là khác biệt chính so với ranh giới sở hữu dữ liệu của bản microservices. Để benchmark đối chứng, monolith được container hóa bằng `docker-compose.monolith.yml` (API ở cổng 5100) với tổng ngân sách CPU tương đương bản microservices.

```mermaid
flowchart LR
    U[Patient / Receptionist / Doctor / Admin] --> FE[Vue Frontend]
    FE --> API[Clinic.API monolith\nControllers + Application + Domain]
    API --> INF[Infrastructure / EF Core]
    INF --> DB[(SQL Server\nClinicDB / ApplicationDbContext)]
    API -. in-process calls .-> API
```

### 3.2. Thiết kế CSDL monolith

`ApplicationDbContext` đăng ký chung các nhóm bảng: `Persons`, `Users`, `Roles`, `Permissions`, bảng nối `UserRoles`/`RolePermissions`, `Employees`, `Doctors`, `Specialties`, `WorkHistories`, `WorkSchedules`, `ShiftRequests`, `Patients`, `Appointments`, `AppointmentSnapshots`, `QueueTickets`, `QueueCounters`, `MedicalReports`, `MedicalReportAmendments`, `MedicalAccessAudits`, `Notifications` và `RefreshTokens`. Cấu hình EF Core nằm ở `backend/Clinic.Infrastructure.Sqlserver/Configurations`.

ERD khái quát các quan hệ nghiệp vụ chính theo cấu hình EF (không liệt kê mọi cột/index/migration):

```mermaid
erDiagram
    PERSON ||--o| USER : account
    PERSON ||--o| EMPLOYEE : staff_profile
    PERSON ||--o| PATIENT : patient_profile
    PERSON ||--o{ NOTIFICATION : receives
    USER ||--o{ REFRESH_TOKEN : owns
    USER ||--o{ USER_ROLE : assigned
    ROLE ||--o{ USER_ROLE : grants
    ROLE ||--o{ ROLE_PERMISSION : contains
    PERMISSION ||--o{ ROLE_PERMISSION : assigned
    EMPLOYEE ||--o| DOCTOR : doctor_profile
    DOCTOR ||--o{ WORK_HISTORY : has
    SPECIALTY ||--o{ WORK_HISTORY : classifies
    DOCTOR ||--o{ WORK_SCHEDULE : works
    WORK_SCHEDULE ||--o{ APPOINTMENT : schedules
    PATIENT ||--o{ APPOINTMENT : books
    APPOINTMENT ||--o{ APPOINTMENT_SNAPSHOT : history
    APPOINTMENT ||--o| QUEUE_TICKET : checkin
    APPOINTMENT ||--o| MEDICAL_REPORT : encounter_record
    QUEUE_TICKET ||--o| MEDICAL_REPORT : visit_context
    MEDICAL_REPORT ||--o{ MEDICAL_REPORT_AMENDMENT : amended_by
```

Một số quan hệ là tùy chọn (ví dụ Person có thể chưa có User; khách walk-in vẫn có hồ sơ Person/Patient). `MedicalAccessAudits` nằm cùng database nhưng cấu hình chỉ khai báo khóa/index, không khai báo FK trong EF nên không vẽ cạnh ở đây.

## 4. Sản phẩm mới và phân rã nghiệp vụ

Frontend truy cập qua Clinic API Gateway (`Clinic.API` ở nhánh `main`, sau đợt cleanup chỉ còn vai trò Gateway). Gateway xác thực, áp QoS và proxy đến service sở hữu nghiệp vụ. Sáu bounded context:

| Service | Project | Sở hữu nghiệp vụ/dữ liệu chính | Database logic |
|---|---|---|---|
| Identity | `Clinic.Identity.Service` | Tài khoản, credential, token, role/permission | `ClinicIdentityDB` |
| Staff & Scheduling | `Clinic.StaffScheduling.Service` | Nhân sự/bác sĩ, chuyên khoa, ca làm, reservation slot | `ClinicSchedulingDB` |
| Appointment | `Clinic.Appointment.Service` | Vòng đời lịch hẹn, booking/đổi/hủy/check-in, walk-in, outbox | `ClinicAppointmentDB` |
| Queue | `Clinic.Queue.Service` | Queue ticket/counter, số thứ tự, trạng thái gọi/khám | `ClinicQueueDB` |
| Medical Records | `Clinic.MedicalRecords.Service` | Hồ sơ bệnh nhân, encounter, medical report, amendment, audit | `ClinicMedicalRecordsDB` |
| Notification | `Clinic.Notification.Service` | Inbox, delivery/outbox, provider result/replay, SignalR | `ClinicNotificationDB` |

Sáu database được cấu hình riêng nhưng cùng chạy trên service `db` (một SQL Server container) trong Compose. RabbitMQ vận chuyển event. Prometheus thu metrics (kèm `mssql-exporter`), Jaeger nhận trace OTLP; dashboard và alert rule nằm trong `observability/`. Mỗi container có giới hạn CPU/RAM trong `docker-compose.yml`.

### 4.1. Sơ đồ tổng quan

```mermaid
flowchart TB
    Users[Patient / Receptionist / Doctor / Admin] --> FE[Frontend]
    FE --> GW[Clinic.API Gateway\nPublic API + Auth + QoS + Proxy]
    GW --> ID[Identity Service]
    GW --> SCH[Staff & Scheduling Service]
    GW --> AP[Appointment Service]
    GW --> Q[Queue Service]
    GW --> MR[Medical Records Service]
    GW --> N[Notification Service / Hub]
    ID --> SQL[(SQL Server container\n6 database logic)]
    SCH --> SQL
    AP --> SQL
    Q --> SQL
    MR --> SQL
    N --> SQL
    AP <--> |reserve / confirm / release slot| SCH
    AP -->|Outbox events| MQ[(RabbitMQ)]
    Q -->|Queue events| MQ
    SCH -->|Scheduling events| MQ
    MQ --> Q
    MQ --> MR
    MQ --> N
    MQ --> AP
    MQ --> SCH
    P[Prometheus + Dashboards + Alerts] -. scrape .-> GW
    GW -. OTLP .-> J[Jaeger]
```

Mọi service đều được Prometheus scrape và gửi trace sang Jaeger; sơ đồ chỉ vẽ Gateway để gọn. Sơ đồ thể hiện quyền sở hữu và hướng giao tiếp logic, không hàm ý mỗi service có SQL Server riêng.

### 4.2. Luồng phối hợp: orchestration và choreography

**Đặt lịch — điều phối đồng bộ kết hợp SAGA nền:**

1. Gateway chuyển `POST /appointments` kèm idempotency key đến Appointment Service.
2. Appointment Service kiểm tra request và patient projection, gọi Scheduling để reserve slot. Đây là bước đồng bộ, có thể trả `409` khi tranh chấp slot. Reservation có hạn 10 phút; `ReservationExpirationWorker` thu hồi reservation quá hạn.
3. Appointment Service ghi appointment trạng thái chờ và outbox trong cùng transaction cục bộ.
4. `AppointmentConfirmationWorker` thăm dò định kỳ trên `ClinicAppointmentDB` (`NextConfirmationAttemptAt`, đánh thức tức thì qua `AppointmentWorkSignal`) và gọi đồng bộ Scheduling Service qua HTTP (`/confirm`) để xác nhận reservation (retry tối đa 4 lần với backoff; nếu vẫn thất bại thì release reservation) — bước xác nhận này không đi qua RabbitMQ. Khi thành công, appointment chuyển `Confirmed` và ghi `AppointmentConfirmed` vào Outbox để publisher phát qua RabbitMQ.
5. Notification phản ứng với `AppointmentConfirmed` để tạo inbox/delivery, có retry và chống event lặp. Nếu broker gián đoạn, Outbox giữ thông điệp đến khi broker phục hồi.

Đây là luồng **điều phối lai**: reserve/confirm do Appointment điều khiển trực tiếp qua API, còn việc lan truyền sang các bounded context khác dùng choreography qua event. Vì các database độc lập, đây không phải một transaction ACID xuyên toàn hệ thống.

**Hủy lịch:** Appointment ghi trạng thái hủy và `AppointmentCancelled` vào outbox, đồng thời gọi trực tiếp Scheduling để nhả slot (best-effort). Scheduling cũng consume event để nhả slot nên hai đường bổ trợ nhau.

**Check-in → Queue → Medical Records/Notification — choreography:**

1. Appointment kiểm tra check-in theo `AppointmentId`, ghi trạng thái và `AppointmentCheckedIn` vào outbox.
2. Queue consume event qua inbox (unique index), tạo đúng một ticket, cấp số theo ngày/bác sĩ. Lỗi tạm thời được đưa vào retry queue (tối đa 3 lần), quá hạn thì vào DLQ.
3. Queue phát event gọi số/bắt đầu/kết thúc khám; Medical Records cập nhật encounter theo `AppointmentId`, Notification tạo thông báo, Appointment nhận `queue.exam-completed` để đóng lịch hẹn.
4. Retry hoặc event trùng không tạo ticket/encounter trùng; thứ tự vòng đời được bảo vệ bằng state guard và idempotency.

### 4.3. OpenAPI và kiểm thử hợp đồng

Repo có OpenAPI snapshot cho Gateway và sáu service trong `tasks/contracts/` (kèm `api-catalog-baseline.json`), endpoint Swagger trong từng ứng dụng và `ApiContractDriftTests` trong `backend/Clinic.IntegrationTests`. Lượt nghiệm thu ngày 10/11/2026 ghi contract validation 13/13 pass. Trước khi nộp nên chạy lại `scripts/validate-api-contracts.ps1` trên đúng commit nộp và đính kèm kết quả.

## 5. SLA/SLO/QoS

Ma trận chi tiết: [`../tasks/SLI_SLO_MATRIX_MEMBER_A.md`](../tasks/SLI_SLO_MATRIX_MEMBER_A.md).

| SLI/QoS | Mục tiêu dự án | Cách hiểu |
|---|---:|---|
| Availability tại Gateway | ≥ 99,0% | Loại expected 4xx, 401/403, contention 409, throttling 429 và client abort khỏi lỗi hệ thống; 5xx/502/503/504 tính là failure |
| API latency | p95 ≤ 500 ms; p99 ≤ 1.500 ms | Đo riêng từng endpoint/boundary; percentile các hop không cộng lại |
| Server error | ≤ 1,0% | Theo cửa sổ và mẫu tối thiểu quy định trong ma trận |
| Event processing lag | p95 ≤ 5 s | Từ lúc publish đến lúc xử lý/ack |
| DLQ | 0 message hợp lệ bị bỏ lại | Poison event phải được cách ly, chẩn đoán và replay an toàn |
| Invariants | 0 overbooking; 0 queue ticket trùng | Kiểm tra bằng integration/load test và truy vấn đối soát |
| Rate limiting | Fixed window, mặc định 100 request/phút/partition | Đã xác minh trong `Clinic.API/Program.cs`; authenticated theo subject, anonymous theo remote IP |

Các ngưỡng này là mục tiêu đồ án trên môi trường Docker Compose, không phải SLA production hay cam kết uptime theo tháng.

## 6. Kiểm thử và kết quả

### 6.1. Kiểm thử chức năng và hợp đồng

Lượt nghiệm thu sau cleanup ngày 10/11/2026 ([`PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](../tasks/PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md)) ghi: toàn bộ backend solution **285/285 test pass**, contract validation **13/13**, QoS failure-policy **8/8**, cùng runtime acceptance cho baseline, booking, contention, broker degradation/recovery, soak, queue/medical record và event timing. Bộ test hiện có 267 phương thức `[Fact]`/`[Theory]` trải trên 12 project test (số test case lớn hơn do `[Theory]` có nhiều bộ dữ liệu).

Trong đợt review này chưa chạy lại được bộ test vì máy tổng hợp không có .NET 10 SDK và Docker không hoạt động (xem mục 7.3).

### 6.2. Benchmark booking 20 VU (12,5 CPU mỗi hệ thống)

Cả hai stack có tổng giới hạn 12,5 CPU, chạy lần lượt, cùng script `tests/load/booking.js` (lưu ý trên đường ghi đặt lịch, Monolith backend có 7,5 CPU còn đường booking Microservices — Gateway + Appointment + Scheduling — có khoảng 4 CPU vì ngân sách chung gánh thêm Frontend, Prometheus, Jaeger và SQL exporter; bản Monolith benchmark cũng đã được chỉnh đồng bộ hóa luồng xác nhận từ 10/11). Lượt burst đầu ngày 11/11 (`iterations: 20`, thấp hơn sample floor 100 requests cho p95 trong ma trận SLO) cho p95 booking API 594,7 ms ở microservices và 293,4 ms ở monolith. Sau khi tối ưu truy vấn reserve (mục 6.5), ngày 17/11 đo lại mỗi bên ba lượt:

| Hệ thống | Booking API p95: lượt 1 / 2 / 3 | POST → Confirmed p95: lượt 1 / 2 / 3 | Ngưỡng p95 < 500 ms |
|---|---:|---:|---|
| Microservices | 983,8 / 417,4 / 449,0 ms | 1.529 / 729,6 / 661,1 ms | Lượt 1 không đạt, lượt 2–3 đạt |
| Monolith | 796,7 / 335,6 / 297,9 ms | 908,2 / 408,0 / 368,6 ms | Lượt 1 không đạt, lượt 2–3 đạt |

Mọi lượt đạt 101/101 check, 20/20 booking Confirmed và cleanup, 0 HTTP 5xx. Lượt đầu của cả hai bên chậm do khởi động nguội. Median p95 microservices giảm khoảng 24,5% so với mốc 594,7 ms nhưng vẫn cao hơn monolith khoảng 34% (449,0 so với 335,6 ms).

### 6.3. Booking high-load 30 VU, 2.400 slot

| Chỉ số (median của 3 lượt) | Monolith (22/11) | Microservices (17/11) |
|---|---:|---:|
| p95 mọi booking API | 190,2 ms | 253,6 ms |
| p95 API tạo booking thành công | 214,6 ms | 387,8 ms |
| p95 POST → Confirmed | 235 ms | 762 ms |
| Thông lượng booking Confirmed | 29,84/s | ≈ 29,72/s |
| 5xx | 0% | 0% |

Cả hai đạt mọi threshold, dữ liệu test được cleanup. Thông lượng xác nhận gần bằng nhau, còn latency monolith thấp hơn. Lưu ý: monolith dùng chung DB qua các lượt nên lịch sử appointment đã hủy tích lũy và p95 tăng theo thứ tự chạy (122 → 190 → 254 ms); một lượt monolith bị loại khỏi nhóm acceptance do teardown timeout.

### 6.4. Mixed workload (đọc/ghi hỗn hợp nhiều persona)

| Profile | Hệ thống | Tỷ lệ HTTP đọc / ghi | p95 đọc / ghi / tạo booking | Workflow thành công / 5xx | Queue lifecycle |
|---|---|---:|---:|---:|---:|
| normal, 20 VU, 5 phút | Monolith | 68,75% / 31,25% | 82,2 / 111,3 / 50,3 ms | 100% / 0% | Không áp dụng |
| normal, 20 VU, 5 phút | Microservices | 71,30% / 28,70% | 26,8 / 91,3 / 107,2 ms | 100% / 0% | Không áp dụng |
| clinic-peak, 30 VU, 5 phút | Monolith | 64,00% / 36,00% | 276,7 / 362,4 / 287,8 ms | 94,81% / 2,90% | 0/865 (lỗi walk-in) |
| clinic-peak, 30 VU, 5 phút | Microservices | 67,73% / 32,27% | 49,7 / 187,8 / 343,8 ms | 99,976% / 0% | 836/836 hoàn tất |

Ở profile normal, microservices có p95 đọc và ghi thấp hơn, monolith có p95 tạo booking thấp hơn. Clinic-peak trên monolith **không đạt** do lỗi chức năng: luồng walk-in lấy `WorkSchedule` bằng `AsNoTracking` rồi thêm `Appointment` qua navigation graph, khiến EF Core insert lại schedule đã tồn tại (`DbUpdateException`). Vì vậy chưa thể so hiệu năng Queue giữa hai bên. Mỗi profile mới có một lượt mỗi bên.

### 6.5. Tối ưu đã thực hiện

- **Truy vấn kiểm tra capacity khi reserve** (`SchedulingManager.cs`): trước đây đọc toàn bộ `TimeSlot` của các reservation còn hiệu lực về ứng dụng; nay dùng một truy vấn aggregate theo `WorkScheduleId`, giữ nguyên khóa bản ghi schedule và filtered unique index. Hiệu quả đã đo ở mục 6.2.
- **Đánh thức worker theo tín hiệu** (`AppointmentWorkSignal.cs`): outbox publisher và confirmation worker được đánh thức ngay khi có appointment/outbox mới, giảm thời gian chờ POST → Confirmed.
- **Connection pool** cấu hình `Min Pool Size`/`Max Pool Size` cho các service chịu tải ghi (Scheduling, Appointment, Queue, Identity).

### 6.6. Nhận định so sánh

Với các lượt đo hiện có, monolith có latency booking thấp hơn (ít hop mạng, giao dịch cục bộ), thông lượng xác nhận hai bên tương đương, còn microservices xử lý được tải hỗn hợp có Queue mà monolith gặp lỗi. Các kết quả này **chưa đủ để kết luận kiến trúc nào tốt hơn tổng thể**, vì chưa khôi phục DB snapshot giữa các lượt, chưa thu CPU/RAM hai stack theo cùng cách, và mixed workload mới có một lượt mỗi bên.

## 7. Kết quả review hệ thống (27/11/2026)

Review tĩnh trên commit `5dd739f`, tập trung vào độ tin cậy messaging, invariant dữ liệu, bảo mật cấu hình và tính chính xác của bằng chứng.

### 7.1. Điểm đã xác minh tốt

- Ranh giới dữ liệu đúng như thiết kế: mỗi service chỉ có connection string tới database của mình; giao tiếp liên service qua HTTP client có cấu hình hoặc RabbitMQ.
- Invariant được bảo vệ ở tầng database: unique index cho queue ticket theo appointment và số thứ tự, unique idempotency key cho appointment, unique `EventId` cho outbox, unique inbox message ở Queue.
- Queue, Medical Records và Notification có retry queue + DLX; Notification có API liệt kê và replay delivery dead-lettered qua Gateway (`AdminController`).
- Reservation có thời hạn và worker thu hồi, nên confirm thất bại không giữ slot vĩnh viễn. Hủy lịch nhả slot bằng hai đường (gọi trực tiếp + event).
- Bảo mật cấu hình: JWT ký bằng khóa bất đối xứng lưu trong volume (`Jwt__SigningKeyDirectory`); mật khẩu RabbitMQ bắt buộc lấy từ `.env` (Compose báo lỗi nếu thiếu); repo chỉ có `.env.example`.

### 7.2. Vấn đề cần xử lý

| # | Mức độ | Vấn đề | Ảnh hưởng | Đề xuất |
|---|---|---|---|---|
| 1 | Cao | 10 file evidence đã commit trong `tests/load/evidence/` còn chứa JWT: `raw_contention.json`, `raw_load.json`, `raw_smoke.json`, `raw_spike.json`, `highload-micro-booking-k6.json` và 5 file `review-*-booking-k6.json`. Token là token dev (issuer `http://localhost:5000`), đã hết hạn từ 04/11/2026. | Không khai thác được trên môi trường khác, nhưng vi phạm quy tắc “không lưu token trong evidence” mà chính tài liệu dự án đặt ra, và lộ cấu trúc claim/role. | Sanitize hoặc xóa các file raw, chỉ giữ summary; xoay khóa ký Identity nếu khóa dev đã dùng ở môi trường dùng chung; cân nhắc làm sạch lịch sử git trước khi công khai repo. |
| 2 | Trung bình | `AppointmentEventConsumer` và `SchedulingEventConsumer` gọi `BasicNack(requeue: false)` với **mọi** exception, nhưng queue của chúng không khai báo dead-letter exchange. | Lỗi tạm thời (DB timeout, deadlock) làm broker **xóa hẳn** message. Mất `queue.exam-completed` khiến appointment không chuyển Completed; mất `appointment.cancelled` ở Scheduling chỉ được bù nếu lệnh release trực tiếp thành công. Trái với SLO “0 message hợp lệ bị bỏ lại”. | Áp dụng cùng mẫu retry queue + DLX như Queue/Medical Records; phân biệt lỗi tạm thời với poison message; thêm test mô phỏng lỗi DB khi consume. |
| 3 | Trung bình | Lỗi walk-in của monolith (mục 6.4) chưa được sửa: commit mới nhất trên nhánh `monolith` (`1e64bfc`) chỉ thêm harness và evidence. | Không có số liệu đối chứng Queue dưới tải; clinic-peak của monolith không hợp lệ để so sánh. | Attach `WorkSchedule` với state `Unchanged` (hoặc chỉ gán FK), thêm integration test hồi quy, chạy lại clinic-peak ba lượt. |
| 4 | Thấp | Sáu database logic chạy trên một SQL Server container (giới hạn 4 CPU). | Điểm lỗi đơn và tài nguyên dùng chung; một service tải nặng có thể làm chậm các service khác, không phản ánh khả năng mở rộng độc lập. | Ghi rõ là giới hạn của môi trường đồ án; nếu cần chứng minh cô lập lỗi, tách ít nhất DB của Appointment/Scheduling sang instance riêng. |
| 5 | Thấp | `DB_SA_PASSWORD` có giá trị mặc định trong `docker-compose.yml`, trong khi RabbitMQ bắt buộc cấu hình qua `.env`. Mọi service kết nối DB bằng tài khoản `sa`. | Chạy Compose không có `.env` vẫn dùng mật khẩu công khai; service có toàn quyền trên mọi database. | Bắt buộc `DB_SA_PASSWORD` như RabbitMQ (`:?`); tạo login riêng cho từng service, chỉ cấp quyền trên database của service đó. |

### 7.3. Giới hạn của đợt review

- Không chạy lại test backend: máy không có .NET 10 SDK (dù repo có `.tools/dotnet-install.ps1`) và Docker Desktop không chạy. Kết quả 285/285 dựa trên lượt nghiệm thu ngày 10/11/2026.
- Không chạy lại benchmark; mọi số liệu tải lấy từ artifact đã lưu.
- Review tĩnh tập trung vào backend và cấu hình; frontend chỉ kiểm tra qua việc test spec có trong repo.

## 8. Đánh giá mức hoàn thành

**Hoàn thành phần kiến trúc và triển khai chính.** Đã phân rã thành Gateway và sáu service có database riêng, có OpenAPI và drift test, outbox/inbox/idempotency, SAGA cho đặt lịch, choreography cho check-in/queue/hồ sơ/thông báo, SLI/SLO/QoS cùng metrics, tracing, dashboard, alert, và bộ kiểm thử tải gồm smoke, load, spike, soak, contention, high-load và mixed workload.

**Chưa đủ căn cứ để đóng hoàn toàn mục tiêu kiểm thử tải so sánh**, và còn hai lỗ hổng độ tin cậy/vệ sinh dữ liệu cần xử lý trước khi nộp.

### 8.1. Việc cần làm trước khi nghiệm thu toàn bộ

1. **Làm sạch evidence chứa JWT** (vấn đề 1), ưu tiên trước khi chia sẻ repo hoặc chiếu artifact.
2. **Bổ sung retry + DLX cho consumer Appointment và Scheduling** (vấn đề 2), rồi chạy lại test.
3. **Sửa lỗi walk-in monolith** (vấn đề 3) và chạy lại clinic-peak.
4. **Lặp benchmark có trạng thái dữ liệu tái lập:** khôi phục cùng DB snapshot/seed trước mỗi lượt, tối thiểu ba lượt mỗi profile, báo median và khoảng biến thiên.
5. **Thu tài nguyên ngang nhau:** chuỗi `docker stats` cùng chu kỳ cho mọi container của cả hai stack. CPU trên 100% nghĩa là dùng nhiều hơn một logical core, không phải vượt quota.
6. **Chạy lại toàn bộ test và contract validator** trên commit nộp, ghi kết quả vào báo cáo nghiệm thu cuối.

## 9. Tài liệu và nguồn đối chiếu

- Thiết kế/task tổng thể: [`../tasks/TASK_MEMBER_A.md`](../tasks/TASK_MEMBER_A.md), [`../tasks/TASK_MEMBER_B.md`](../tasks/TASK_MEMBER_B.md), các execution plan Phase 0–9 trong `tasks/`.
- SLI/SLO/QoS: [`../tasks/SLI_SLO_MATRIX_MEMBER_A.md`](../tasks/SLI_SLO_MATRIX_MEMBER_A.md).
- Kiểm thử tải: [`../tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md`](../tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md) (mục 18–22), [`PHASE_9_LOAD_TEST_REPORT_MEMBER_B.md`](PHASE_9_LOAD_TEST_REPORT_MEMBER_B.md), [`../tests/load/README.md`](../tests/load/README.md).
- Nghiệm thu sau cleanup: [`../tasks/PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](../tasks/PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md).
- Lịch sử benchmark: [`../tasks/BENCHMARK_MONOLITH_VS_MICROSERVICES_FULL_HISTORY.md`](../tasks/BENCHMARK_MONOLITH_VS_MICROSERVICES_FULL_HISTORY.md).
- OpenAPI: [`../tasks/contracts/`](../tasks/contracts/); drift test: `backend/Clinic.IntegrationTests/ApiContractDriftTests.cs`.
- Vận hành: [`MEMBER_B_OPERATIONS_RUNBOOK.md`](MEMBER_B_OPERATIONS_RUNBOOK.md), [`PHASE_0_OPERATIONS_AND_ACCEPTANCE.md`](PHASE_0_OPERATIONS_AND_ACCEPTANCE.md).
- Mã monolith đối chứng: nhánh `monolith`, nhất là `backend/Clinic.Infrastructure.Sqlserver/Persistence/ApplicationDbContext.cs`, `Configurations/` và `docker-compose.monolith.yml`.

## 10. Cập nhật rà soát & khắc phục — 27/11/2026

Sau rà soát mã nguồn và hạ tầng trên cả hai nhánh, các điểm sau đã được sửa trực tiếp trong codebase (lưu ý toàn bộ số liệu benchmark ngày 17/11 và 22/11 ở mục 6 đều đo trước đợt cập nhật này):

- **Kiểm soát truy cập & xác thực nội bộ (Microservices):** Bổ sung kiểm tra quyền sở hữu (`patientId`) cho `Cancel` và `Reschedule` tại `AppointmentService`; bảo vệ toàn bộ API reservation và `/internal/events/appointment-cancelled` của `StaffScheduling.Service` bằng `X-Clinic-Service-Token`; chặn người dùng thường giả mạo `IsWalkIn=true` hoặc hồ sơ bệnh nhân khi đặt lịch; loại bỏ publish port trực tiếp (`5001`–`5006`) khỏi host trong `docker-compose.yml` (chuyển sang `expose` nội bộ mạng Docker).
- **Toàn vẹn SAGA, Idempotency & Optimistic Concurrency:** Phát hành và giải phóng slot theo `SchedulingReservationId` (thay vì xóa nhầm slot của bệnh nhân khác); `ConfirmAppointmentAsync` gọi xác nhận slot với Scheduling; khóa kiểm tra idempotency theo `schedule/slot/patient` và trạng thái còn hiệu lực; cập nhật `SchedulingReservationId` khi `Reschedule`, chặn reschedule lịch `CheckedIn` (đồng thời hủy queue ticket nếu nhận `IsRescheduleRelease`); bổ sung `RowVersion` (`IsRowVersion()`) cho `SlotReservation`, `Appointment`, `QueueTicket` kèm concurrency guard giữa `AppointmentConfirmationWorker` và thao tác hủy lịch; sửa quy tắc hủy 24 giờ sang giờ địa phương (`ClinicTime.ToUtc`), chặn `ProcessExamCompletedAsync` ghi đè lịch `Cancelled` và kích hoạt bộ lọc `specialtyId` trong `GetSummaryAsync`.
- **Quan sát, SLO, RabbitMQ DLQ & Contract:** Khai báo explicit histogram buckets cho `clinic.queue.consumer.lag` và `clinic.queue.ticket.processing` để đo lag thực tế thay vì bị kẹp bucket 5s; chuẩn hóa `alert.rules.yml` theo `job="clinic-api"`, loại `400|401|403|404|409|422|429`, sửa `for:` (`2m`/`1m`/`3m`); cấu hình Prometheus scrape `/metrics/per-object` của RabbitMQ và sửa regex DLQ (`.*([.-]dlq|\.dead)$`) để bắt đủ cả `outbox-dlq`; tách dọn queue `.events` v1 sang channel riêng tránh đóng nhầm channel consumer khi khởi động trên broker sạch (nếu nâng cấp trên volume cũ có queue v1 còn message tồn, cần purge/xóa thủ công một lần qua RabbitMQ UI `:15672`); giới hạn `ReservationsController` trên Gateway về `Roles = "Admin"` và chuyển script lấy token/degradation sang đăng nhập qua Gateway `:5000`; khôi phục mặc định `RATE_LIMIT_PERMIT_LIMIT=100` trong `docker-compose.yml` (khi chạy k6 dùng chung token bệnh nhân cần nâng tạm `RATE_LIMIT_PERMIT_LIMIT=100000` rồi trả về `100`); đồng bộ OpenAPI Gateway (`PATCH` trả `200 OK` với `AppointmentDto`, giữ nguyên mã lỗi Scheduling proxy và `403` trên `/reservations`).
- **Monolith walk-in & Tối ưu SQL:** Sửa `CreateWalkInAppointment` trên nhánh `monolith` chỉ chọn ca khám đúng ngày địa phương hôm nay còn hiệu lực (`ws.Date == todayLocal && ws.ShiftEnd > nowTime`), bổ sung integration test trực tiếp với SQL Server; trước đó đã bật RCSI và cấp login riêng cho 6 DB logic trên container SQL Server.

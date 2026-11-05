# Ma trận SLI/SLO & Chính sách QoS — Thành viên A (Phase 8)

> **Trạng thái:** Bản nghiệm thu toàn diện chính thức (Official Fully Integrated & Verified Specification)<br>
> **Áp dụng cho:** Clinic API Gateway và 6 Microservices (Identity, Scheduling, Appointment, Queue, Medical Records, Notification) + Hạ tầng (SQL Server, RabbitMQ)<br>
> **Cam kết:** Tuân thủ quyết định D3 (Ngưỡng đo baseline môn học trên môi trường Docker Compose), D5 (QoS Retry/Idempotency an toàn), không đưa PHI/token/credentials vào telemetry/logs/traces.

---

## 1. Nguyên tắc cốt lõi và Quy ước tính toán

### 1.1. Phạm vi và Đối tượng đo (Request Population)
- **Hệ thống mục tiêu:** Chạy trên môi trường Docker Compose (12 containers, mạng bridge `clinic_net`).
- **Phân định ranh giới đo:**
  - **Gateway Boundary:** Điểm vào duy nhất cho client bên ngoài (frontend SPA, external clients). Đo trải nghiệm người dùng cuối (End-to-End).
  - **Service Boundary:** Đo hiệu năng và tính ổn định nội tại của từng service owner (Service-to-Service, Service-to-DB, Event Consumers).
  - **Percentile Non-Additivity:** Tuyệt đối không cộng gộp các số đo percentile (p50, p95, p99) giữa các hop mạng; mỗi hop đo độc lập.

### 1.2. Quy ước phân loại Status Code & Công thức Availability (Status Treatment theo D3)
Theo quyết định D3, Availability phải đo trên **tập request đủ điều kiện được kỳ vọng phục vụ (Eligible Requests)**, không được trộn lẫn lỗi nghiệp vụ thông thường vào lỗi hệ thống:

- **Tập request đủ điều kiện (Eligible Requests):**
  $$\text{Eligible} = \text{Total} - (\text{Expected 4xx}) - (\text{401/403 Auth}) - (\text{409 Contention}) - (\text{429 Throttled}) - (\text{Client Abort})$$
- **Công thức Tính Availability:**
  $$\text{Availability} = \frac{\text{Eligible} - \text{System Failures (5xx + 502 + 503 + 504)}}{\text{Eligible}}$$
  *(Nếu $\text{Eligible} = 0$: Báo cáo `N/A` (Invalid / All Throttled window), tuyệt đối không tính là 100%).*

| Nhóm Status | Ý nghĩa nghiệp vụ | Phân loại trong Availability SLI | Ghi nhận Telemetry |
|---|---|---|---|
| **2xx** (200, 201, 204) | Thành công hoàn tất | **Thành công (Success)** | Đếm vào Tử số & Mẫu số |
| **401, 403** | Sai thông tin xác thực, token hết hạn, không đủ quyền | **Loại khỏi Availability lỗi hệ thống**; báo cáo riêng theo Security/Auth Audit | Đếm vào Mẫu số Audit, không tính là downtime |
| **400, 422** | Dữ liệu đầu vào sai validation, schema không hợp lệ | **Client Error (Expected)** | Báo cáo riêng theo Client Error rate, loại khỏi 5xx |
| **409** | Xung đột nghiệp vụ (Slot đã kín, bác sĩ bận, double booking) | **Business Contention (Expected)**; minh chứng invariant được bảo vệ | Báo cáo riêng theo Contention Metric, loại khỏi 5xx |
| **429** | Vượt ngưỡng Rate Limit Gateway (100 req/phút/partition) | **QoS Throttled (Expected Policy)** | Báo cáo riêng theo Throttling Metric, loại khỏi 5xx. Nếu 100% request bị throttled, báo cáo `N/A (All Throttled)`. |
| **500, 502, 503, 504** | Lỗi server nội bộ, downstream down, timeout vượt ngân sách | **Lỗi hệ thống (System Failure)** | Tính là Thất bại (Failure) trong Availability SLO |
| **Client Abort** | Client đóng tab/ngắt kết nối trước khi server phản hồi | **Loại khỏi SLI** (`CancellationToken.IsCancellationRequested`) | Bỏ qua |

---

## 2. Bảng Quyết định Thống nhất Ngưỡng Cam kết SLO (Unified SLO Decision Table)

Để tránh bất kỳ xung đột nào giữa các đề xuất cục bộ và khuyến nghị thiết kế D3, hai bên thống nhất phân định rõ giữa **Ngưỡng nghiệm thu chính thức (Formal SLO Gate)** và **Mục tiêu tối ưu hóa nội bộ (Internal Optimization Target)**:

| Hạng mục / Chỉ số | Ngưỡng Cam kết Nghiệm thu Chính thức (SLO Gate theo D3) | Mục tiêu Tối ưu hóa Nội bộ (Service Target) | Cửa sổ đo & Sample Floor | Điều kiện vi phạm / Alert Trigger | Rationale & Căn cứ kỹ thuật |
|---|---|---|---|---|---|
| **API Availability (E2E Gateway)** | **≥ 99.0%** | ≥ 99.5% | 5 phút steady; tối thiểu 300 requests | Availability < 99% trong 2 phút liên tiếp | Đảm bảo tính khả dụng cao cho người dùng cuối trên Docker môn học. Loại trừ 4xx/409/429. |
| **Độ trễ API Đồng bộ (p95)** | **p95 ≤ 500 ms** | Identity ≤ 300 ms<br>Catalog ≤ 350 ms<br>Booking ≤ 450 ms | 5 phút steady; tối thiểu 100 requests / endpoint | p95 > 500 ms trong 2 phút liên tiếp | Baseline thực tế đạt 2.49ms – 9.46ms; mức 500ms tạo dư địa an toàn gấp >50 lần khi có tải. |
| **Độ trễ API Đồng bộ (p99)** | **p99 ≤ 1500 ms** | Identity ≤ 800 ms<br>Scheduling ≤ 1000 ms | 5 phút steady; tối thiểu 100 requests / endpoint | p99 > 1500 ms trong 1 phút | Hấp thụ các đợt contention lock tại cơ sở dữ liệu SQL Server. Baseline thực tế p99 ≤ 20.50 ms. |
| **Tỷ lệ Lỗi Server (5xx / Timeout)** | **≤ 1.0%** | ≤ 0.5% | 5 phút steady; tối thiểu 300 requests | 5xx rate > 1.0% trong 2 phút | Downstream timeout được bọc 504 GatewayTimeout; sự cố mạng bọc 502 BadGateway. |
| **Độ trễ Xử lý Sự kiện (Event Lag p95)** | **p95 ≤ 5.0 s** | Queue Ticket ≤ 3.0 s<br>Consultation Sync ≤ 4.0 s | 5 phút steady; tối thiểu 50 events | Event Lag p95 > 5.0 s trong 3 phút | **Đo lường:** `ConsumerAckTimestamp - EventPublishTimestamp`. Tính toán nội suy toán học P95 = 4.75s (≤ 5.0s). Không nhầm lẫn với queue depth. |
| **Hàng đợi Dead-Letter (DLQ)** | **0 messages** (với mọi event hợp lệ) | 0 messages | Đo liên tục mỗi 15s | Bất kỳ queue `q.*.dlq` nào có message > 0 | Đảm bảo không có poisoned event hoặc schema serialization drift (10/10 DLQs = 0). |
| **Bảo toàn Invariant: Overbooking** | **0 vi phạm** | 0 vi phạm | Toàn bộ quá trình chạy test tải | Số lượng lịch hẹn > Slot capacity | Ràng buộc DB constraint và transaction lock bảo đảm tuyệt đối. |
| **Bảo toàn Invariant: Duplicate Ticket** | **0 vi phạm** | 0 vi phạm | Toàn bộ quá trình chạy test tải | Trùng lặp `(DoctorId, QueueDate, QueueNumber)` | Idempotency deduplication table ngăn chặn cấp trùng vé. |

---

## 3. Ma trận SLI/SLO Chi tiết theo Service Owner & Hiện trạng Scrape

> **Ghi chú hiện trạng Scrape Coverage & Tích hợp Toàn diện Toàn bộ Hệ thống:**
> Toàn bộ **7/7 dịch vụ microservices** trong cụm Docker Compose (`clinic-api`, `clinic-queue-service`, `clinic-identity-service`, `clinic-scheduling-service`, `clinic-appointment-service`, `clinic-medical-records-service`, `clinic-notification-service`) đã hoàn tất tích hợp package `OpenTelemetry.Exporter.Prometheus.AspNetCore`, expose endpoint `/metrics`, và được cấu hình scrape thành công trong `observability/prometheus.yml`.
> Toàn bộ 7 targets đều đang ở trạng thái **`UP`** (`health: up`, `lastError: ""` rỗng) trên Prometheus daemon.
> Toàn bộ 7 alert rules chuẩn xác (`HighApiLatencyP95`, `LowApiAvailability`, `HighErrorRate5xx`, `QueueConsumerLagHigh`, v.v.) đã được nạp thành công từ `alert.rules.yml` với trạng thái `health: ok`, `state: inactive`.

| Phân hệ / Workflow | Chỉ số (SLI) + Công thức toán học | Nguồn Metric & Cơ chế đo lường | Denominator & Xử lý Ngoại lệ | SLO Gate (Chính thức) | Trạng thái Scrape Runtime | Service Owner |
|---|---|---|---|---|---|---|
| **API Gateway** (Public Edge) | **API Availability:**<br>`(Eligible - 5xx - 504) / Eligible`<br><br>**API Latency:**<br>p95 / p99 request duration | `http_server_request_duration_seconds_bucket`<br>`http_server_request_duration_seconds_count`<br><br>*Labels:* `http_response_status_code`, `http_route` | Loại trừ 400, 401, 403, 404, 409, 429 khỏi lỗi hệ thống. Throttled 429 theo dõi riêng. Nếu Eligible = 0 trả `N/A`. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>p99 ≤ 1500 ms<br>5xx rate ≤ 1.0% | **verified** (Prometheus UP, Baseline 800 mẫu trong cửa sổ 5m 19s đạt 100% 2xx, p95 ≤ 9.46ms) | Member A (Gateway/Route)<br>Member B (Infra/Network) |
| **Identity Service** (Auth & Token) | **Login/Refresh Success Rate:**<br>`(Eligible - SystemFailures) / Eligible`<br><br>**Auth Latency:**<br>p95 duration `/auth/login`, `/auth/refresh` | `http_server_request_duration_seconds_*` tại `identity-service:8080/metrics` | Tách biệt 401 (sai mật khẩu) và 403 (bị khóa/thiếu quyền) khỏi lỗi hạ tầng. Tuyệt đối không log credentials. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>p99 ≤ 1500 ms<br>5xx rate ≤ 1.0% | **verified** (Prometheus Target UP, `/metrics` endpoint hoạt động, 10 user tokens xác thực hợp lệ) | Member A (Identity Domain)<br>Member B (Prometheus Scrape) |
| **Staff & Scheduling Service** (Slots & Schedules) | **Catalog & Slot Query Latency:**<br>p95 duration `/schedules`, `/slots`<br><br>**Availability:**<br>`(Eligible - SystemFailures) / Eligible` | `http_server_request_duration_seconds_*` tại `scheduling-service:8080/metrics` | Cache hit/miss được phép sai lệch nhưng p95 không vượt ngưỡng. 404 slot not found là nghiệp vụ. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>p99 ≤ 1500 ms<br>5xx rate ≤ 1.0% | **verified** (Prometheus Target UP, `/metrics` endpoint hoạt động, baseline p95 ≤ 9.46ms) | Member A (Scheduling Domain)<br>Member B (Prometheus Scrape) |
| **Appointment Service** (Booking & Contention) | **Booking Completion Rate:**<br>`(Eligible - SystemFailures) / Eligible`<br><br>**Invariant Preservation:**<br>Số vi phạm overbooking / trùng lịch = 0 | `http_server_request_duration_seconds_*` tại `appointment-service:8080/metrics` | 409 Conflict khi tranh chấp ghế/slot được coi là **Hành vi đúng**, KHÔNG tính là lỗi. Không có reservation mồ côi. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>p99 ≤ 1500 ms<br>**Zero Overbooking** | **verified** (Prometheus Target UP, `/metrics` endpoint hoạt động, 13 contract & drift tests pass) | Member A (Appointment Domain)<br>Member B (Prometheus Scrape) |
| **Queue Service** (Ticket Issuance & Calls) | **Check-in to Ticket Latency:**<br>p95 thời gian cấp số thứ tự<br><br>**Queue Event Ingestion Lag:**<br>p95 thời gian từ publish check-in event đến hoàn tất xử lý ticket | `http_server_request_duration_seconds_bucket{http_route=~"/queue.*"}`<br>Event Lag duration metric:<br>`clinic_queue_consumer_lag_seconds_bucket`<br>*(PromQL: `histogram_quantile(0.95, sum(rate(clinic_queue_consumer_lag_seconds_bucket[5m])) by (le))`)*<br>Backlog Queue Depth:<br>`clinic_queue_length` | Idempotency retry cấp cùng số thứ tự được tính là thành công. Unique `(DoctorId, QueueDate, QueueNumber)`. Queue depth theo dõi backlog, không thay thế cho event lag duration. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>**Event Lag p95 ≤ 5.0 s**<br>Duplicate Ticket = 0 | **verified** (Prometheus UP, 60 mẫu event lag tính toán nội suy toán học P95 = 4.75s, DLQ = 0, queue depth backlog = 5) | Member A (Queue Domain)<br>Member B (Infra/Exporter) |
| **Queue → Medical Records** (Consultation Events) | **Exam State Sync Lag:**<br>Thời gian xử lý `ExamStarted` / `ExamCompleted`<br><br>**Integrity Invariant:**<br>`ExamCompleted` không tự ý finalize hồ sơ | Event processing duration (`ConsumerAckTimestamp - EventPublishTimestamp`). Queue Backlog: RabbitMQ queue depth | Outbox / Inbox pattern đảm bảo Exactly-Once processing về mặt nghiệp vụ. | **Event Lag p95 ≤ 5.0 s**<br>**Finalized Report unaltered**<br>DLQ = 0 | **verified** (Prometheus Target UP, Event lag P95 = 4.75s, 0 DLQ) | Member A (Medical & Queue Domain) |
| **Medical Records Service** (Clinical Records) | **Record Read/Draft/Finalize Latency:**<br>p95 `/encounters/{id}/medical-record`<br><br>**Audit Trail Write Outcome:**<br>Tỷ lệ ghi audit thành công = 100% | `http_server_request_duration_seconds_*` tại `medical-records-service:8080/metrics` | 403 Forbidden khi bác sĩ khác truy cập trái phép là đúng nghiệp vụ. Không lộ PHI trong log/trace. | **Availability ≥ 99.0%**<br>p95 ≤ 500 ms<br>p99 ≤ 1500 ms<br>Audit failure = 0 | **verified** (Prometheus Target UP, `/metrics` endpoint hoạt động, PHI sanitization verified) | Member A (Medical Records Domain)<br>Member B (Prometheus Scrape) |
| **Notification Service** (Realtime & Inbox) | **Event-to-Inbox Dispatch Lag:**<br>Thời gian từ event đến inbox DB<br><br>**WebSocket Delivery Rate:**<br>Tỷ lệ đẩy thành công qua SignalR Hub | `http_server_request_duration_seconds_*` tại `notification-service:8080/metrics` | Provider email/SMS sử dụng MOCK/Console logger trong môi trường kiểm thử. | **Event Lag p95 ≤ 5.0 s**<br>Inbox persistence ≥ 99.9%<br>**DLQ Messages = 0** | **verified** (Prometheus Target UP, `/metrics` endpoint hoạt động, DLQ = 0) | Member A (Notification Domain)<br>Member B (Prometheus Scrape) |
| **Hạ tầng: SQL Server** (Database Dependency) | **Connection Pool Latency & Health:**<br>Thời gian mở kết nối và query thực thi | Database ping / readiness checks:<br>`/readyz` latency và SQL Error counter | Lỗi deadlock được retry tối đa 3 lần ở tầng DbContext trước khi báo lỗi. | **Pool Availability ≥ 99.9%**<br>Query latency p95 ≤ 50 ms<br>Zero connection leak | **verified** (Health Probes UP, 7/7 databases kết nối ổn định) | Member B (Platform Infra) |
| **Hạ tầng: RabbitMQ** (Message Broker) | **Broker Queue Health & Message Lag:**<br>Tổng số message unacknowledged và DLQ depth | `clinic.events` exchange & Management API (`:15672`) | Mọi queue chính phải có dead-letter-exchange gắn kèm; DLQ chỉ nhận poisoned messages. | **Broker Availability ≥ 99.9%**<br>**DLQ hợp lệ = 0**<br>Total unacked < 100 | **verified** (RabbitMQ API: 10 DLQs = 0 messages, 0 unacked) | Member B (Platform Infra) |

---

## 4. Chính sách QoS (Quality of Service) & Trust Boundary

### 4.1. Rate Limiting & Trust Boundary (Tại API Gateway)
- **Thuật toán:** Fixed Window Rate Limiter.
- **Hạn mức mặc định:** 100 requests / 1 phút (`Window = 60s`, `QueueLimit = 0`).
- **Phân vùng (Partitioning Key) & Chống Spoofing IP:**
  - **Authenticated User:** Phân vùng độc lập theo User Subject ID (`sub` hoặc `NameIdentifier` claim).
  - **Anonymous Client:** Phân vùng theo IP kết nối thật (`Connection.RemoteIpAddress`).
  - **Bảo mật Trust Boundary:** Gateway là điểm vào công khai trực tiếp và không đứng sau reverse proxy trung gian trong môi trường Compose; do đó Gateway **không kích hoạt `UseForwardedHeaders`** và **không tin tưởng header `X-Forwarded-For`** do client gửi. Mọi request ẩn danh được phân vùng rate limiting hoàn toàn dựa trên IP kết nối TCP thật (`context.Connection.RemoteIpAddress ?? "anonymous"`). Mọi nỗ lực giả mạo `X-Forwarded-For` đều bị vô hiệu hóa và vẫn bị tính vào cùng một quota của IP kết nối thật (đã được kiểm chứng qua test `RateLimiter_CannotBeBypassed_BySpoofingXForwardedForHeader`).
- **Danh sách miễn trừ (Exemptions):**
  - `/healthz` (Liveness probe)
  - `/readyz` (Readiness probe)
  - `/metrics` (Prometheus Scraper)
  - `/openapi/*` (Contract discovery & API catalog)
  - `/swagger/*` (Interactive UI documentation)
- **Hành vi khi vượt hạn mức (Rejection Policy):**
  - Trả về mã HTTP `429 Too Many Requests`.
  - Bổ sung response header: `Retry-After: 60`.
  - Body trả về theo đúng envelope chuẩn `ApiResponse` (`isSuccess = false`).

### 4.2. Ngân sách Thời gian chờ (Timeout Budgets)
Tổng thời gian xử lý toàn trình (End-to-End Budget) tại Gateway tối đa là **15 giây**:
- **Gateway → Identity Service:** 10 giây.
- **Gateway → Staff & Scheduling Service:** 10 giây.
- **Gateway → Appointment Service:** 10 giây.
- **Gateway → Queue Service:** 10 giây.
- **Gateway → Medical Records Service:** 10 giây.
- **Chính sách phân loại lỗi Downstream:**
  - **Downstream Timeout:** Khi downstream phản hồi vượt ngân sách hoặc ném `TaskCanceledException` / `TimeoutException`, Gateway bọc lỗi trả về HTTP `504 Gateway Timeout` trong envelope chuẩn `ApiResponse`.
  - **Downstream Unavailable:** Khi downstream mất kết nối hoặc ném `HttpRequestException`, Gateway bọc lỗi trả về HTTP `502 Bad Gateway` trong envelope chuẩn `ApiResponse`.
  - Tuyệt đối không để rò rỉ mã 500 không phân loại ra client bên ngoài.

### 4.3. Lan truyền Ngữ cảnh & Phân tán Dấu vết (Distributed Tracing & Correlation)
- **Header chuẩn hóa:** `X-Correlation-ID`.
- **Cơ chế lan truyền:**
  - Nếu client gửi header `X-Correlation-ID`, Gateway bảo tồn nguyên vẹn giá trị client cung cấp.
  - Nếu client không gửi, Gateway tự sinh một GUID duy nhất.
  - Delegating handler (`CorrelationIdDelegatingHandler`) tự động gán `X-Correlation-ID` vào tất cả các outgoing HTTP request tới các microservices downstream.
  - Response trả về cho client luôn luôn đính kèm header `X-Correlation-ID`.
  - Toàn bộ hoạt động tracing tích hợp `ActivityListener` qua OpenTelemetry, sẵn sàng đẩy dữ liệu sang Jaeger daemon (`clinic_jaeger:4317`).

### 4.4. Khử nhạy cảm Dữ liệu Telemetry & Logs (Sanitization & Zero Leakage)
- **Quy tắc tuyệt đối:**
  - Không ghi mật khẩu (`password`, `confirmPassword`), refresh token, access token vào logs, metrics labels, hoặc trace baggage.
  - Không ghi dữ liệu sức khỏe định danh cá nhân (PHI - Protected Health Information) như chẩn đoán bệnh, triệu chứng, ghi chú bệnh án (`Diagnosis`, `ClinicalNotes`, `TreatmentPlan`) vào bất kỳ span tag, event name hay metric label nào.
  - Toàn bộ test ranh giới bảo mật `SensitiveDataSanitization_LogsAndTelemetryNeverContainPasswordsOrPHI` đã xác minh bộ đệm log và `ActivityListener` hoàn toàn sạch dữ liệu nhạy cảm.

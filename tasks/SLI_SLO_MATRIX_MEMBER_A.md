# Ma trận SLI/SLO & Chính sách QoS — Thành viên A (Phase 8)

> **Trạng thái:** Bản sửa đổi 27/11/2026 — SLO phân tầng theo service, SLA 30 ngày theo giờ phòng khám, error budget/burn-rate, chính sách resilience và ma trận kiểm chứng (§6).<br>
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
| **429** | Vượt ngưỡng Rate Limit Gateway (theo hạn mức §5.3) | **QoS Throttled (Expected Policy)** | Báo cáo riêng theo Throttling Metric, loại khỏi 5xx. Nếu 100% request bị throttled, báo cáo `N/A (All Throttled)`. |
| **500, 502, 503, 504** | Lỗi server nội bộ, downstream down, timeout vượt ngân sách | **Lỗi hệ thống (System Failure)** | Tính là Thất bại (Failure) trong Availability SLO |
| **Client Abort** | Client đóng tab/ngắt kết nối trước khi server phản hồi | **Loại khỏi SLI** (`CancellationToken.IsCancellationRequested`) | Bỏ qua |

---

## 2. SLO phân tầng theo service (Tiered SLOs)

### 2.1. Phân tầng mức độ quan trọng

| Tầng | Service / luồng | Lý do |
|---|---|---|
| **Tier 1 — Critical** | Identity (đăng nhập/refresh), Appointment + Scheduling (đặt/hủy/đổi lịch), Queue (check-in, gọi số, khám) | Chặn trực tiếp hoạt động phòng khám: bệnh nhân không đặt được lịch, lễ tân không check-in được khi bệnh nhân đang đứng tại quầy |
| **Tier 2 — High** | Medical Records | Dữ liệu y tế: toàn vẹn và audit quan trọng hơn độ trễ; bác sĩ có thể ghi chú tạm khi gián đoạn ngắn |
| **Tier 3 — Standard** | Notification, báo cáo/thống kê Admin | Có thể trễ vài phút mà không ảnh hưởng nghiệp vụ lõi; không được làm hỏng luồng Tier 1 |

### 2.2. Availability theo service (cửa sổ SLA §4: 30 ngày, giờ phòng khám)

Availability của một hành trình nối tiếp xấp xỉ **tích** availability các thành phần trên đường đi, vì vậy mỗi thành phần phải đặt mục tiêu **cao hơn** mục tiêu đầu-cuối.

| Thành phần | SLO availability | Error budget / 30 ngày (≈ 334 giờ phòng khám) | Ghi chú |
|---|---:|---:|---|
| API Gateway | **99.9%** | ≈ 20 phút | Điểm vào duy nhất |
| Identity Service | **99.9%** | ≈ 20 phút | JWT được kiểm tra cục bộ bằng JWKS đã cache → Identity không nằm trên đường nóng của request đã đăng nhập |
| Appointment Service | **99.7%** | ≈ 60 phút | |
| Staff & Scheduling Service | **99.7%** | ≈ 60 phút | |
| Queue Service | **99.7%** | ≈ 60 phút | |
| SQL Server (container dùng chung 6 DB) | **99.9%** | ≈ 20 phút | Trần availability của mọi service vì 6 DB logic cùng một instance |
| RabbitMQ | **99.9%** | ≈ 20 phút | Outbox đệm sự kiện khi broker gián đoạn (§5.4) |
| Medical Records Service | **99.5%** | ≈ 100 phút | Tier 2 |
| Notification Service | **99.0%** | ≈ 200 phút | Tier 3 |

**Mục tiêu đầu-cuối (dẫn xuất; đây là cam kết SLA ở §4):**

| Hành trình | Thành phần trên đường đi | Tích các SLO thành phần | **SLO E2E** |
|---|---|---:|---:|
| Đặt lịch (booking) | Gateway × Appointment × Scheduling × SQL | 0.999 × 0.997 × 0.997 × 0.999 ≈ **99.2%** | **≥ 99.0%** |
| Check-in & hàng đợi | Gateway × Appointment × Queue × SQL × RabbitMQ | ≈ **99.1%** | **≥ 99.0%** |
| Đăng nhập | Gateway × Identity × SQL | ≈ **99.7%** | **≥ 99.5%** |
| Hồ sơ bệnh án | Gateway × Medical Records × SQL | ≈ **99.3%** | **≥ 99.0%** |
| Toàn bộ request đủ điều kiện tại Gateway | — | — | **≥ 99.0%** |

### 2.3. Độ trễ đồng bộ (đo tại Gateway, cửa sổ 5 phút, tối thiểu 100 mẫu/nhóm)

Ngưỡng theo **nhóm endpoint**, không áp một con số chung cho mọi API. Percentile không cộng qua các hop (§1.1).

| Nhóm endpoint | Ví dụ | p95 | p99 |
|---|---|---:|---:|
| **Đọc (read)** | `/specialties`, `/doctors`, `/doctors/{id}/available`, `/me/appointments`, `/me/queue-status`, `/notifications/unread-count`, bảng hàng đợi | **≤ 300 ms** | **≤ 1,000 ms** |
| **Ghi đồng bộ (write)** | `POST /appointments`, hủy/đổi lịch, check-in, walk-in, gọi số/bắt đầu/kết thúc khám, lưu nháp bệnh án | **≤ 500 ms** | **≤ 1,500 ms** |
| **Đăng nhập** | `POST /auth/login` | **≤ 1,000 ms** | **≤ 2,500 ms** |
| **Refresh token** | `POST /auth/refresh` | **≤ 300 ms** | **≤ 1,000 ms** |

*Lý do ngưỡng đăng nhập riêng:* mật khẩu được kiểm tra bằng BCrypt (cố ý tốn CPU để chống brute-force), nên p95 ≤ 300 ms là không khả thi và cũng không mong muốn về bảo mật. Refresh token không chạy BCrypt nên giữ ngưỡng của nhóm đọc.

> **Năng lực đăng nhập (Identity capacity).** Lần đo ngày 27/11/2026 (BCrypt cost 11, Identity 0.5 CPU): chỉ **2–3 lượt đăng nhập/giây**; ở 4 lượt/s p95 ≈ 9 s và có 504, nên `spike.js` (đăng nhập "lạnh") không đạt. Đã xử lý theo ba hướng, **không chỉ tăng CPU**:
> 1. **BCrypt cost 11 → 10** (`Security:BcryptWorkFactor`, mức tối thiểu OWASP khuyến nghị): giảm một nửa chi phí mỗi lần kiểm tra. Hash cũ (cost 11 từ dữ liệu monolith) được **re-hash trong suốt** ở lần đăng nhập thành công kế tiếp.
> 2. **Bulkhead + load shedding thay vì xếp hàng tới timeout:** tối đa `QoS:PasswordHashConcurrency` (= số core − 1, Compose: **1**) phép băm đồng thời + hàng chờ `QoS:PasswordHashQueueLimit` (**4** ≈ 0,4 s chờ ở năng lực tối đa); vượt quá → **503 + `Retry-After: 1` + `X-Clinic-Load-Shed`** ngay lập tức. Gateway chuyển nguyên 503/`Retry-After` cho client và **không** tính các response này vào circuit breaker, nên refresh/JWKS vẫn hoạt động trong đợt đăng nhập dồn.
> 3. **Identity 0.5 → 1.5 CPU** (1 core cho BCrypt + 0,5 dự phòng cho xử lý request), lấy từ frontend (nginx tĩnh, ~0% CPU: 1.0 → 0.25) và Scheduling (I/O-bound: 1.5 → 1.0) nên tổng ngân sách CPU không đổi.
>
> **Kết quả đo bằng `login-capacity.js` (27/11/2026, ramp 2 → 20 lượt/s, mỗi bậc 35 s):**
>
> | Cấu hình | Trong SLO tới | Bão hòa (lượt nhận/s) | p95 lượt nhận khi quá tải | p95 lượt bị shed | 504 |
> |---|---:|---:|---:|---:|---:|
> | Cost 11 · 0,5 CPU (ban đầu) | ~2/s | ~2,9 | ≈ 9 s | — (chưa có shed) | có |
> | Cost 10 · 1,0 CPU · 2 băm song song · hàng chờ 16 | 6/s | ~9–10 | 2,6–6,8 s ✗ | 1,3 s ✗ | 0 |
> | **Cost 10 · 1,5 CPU · 1 băm · hàng chờ 4 (hiện tại)** | **10/s** (p95 577 ms) | **~10** | **≤ 756 ms ✓** | **14 ms ✓** | **0** |
>
> Lượt đo thứ hai cho thấy chỉ giới hạn đồng thời là chưa đủ: hai luồng BCrypt chiếm trọn quota 1 CPU nên cả đường từ chối nhanh cũng bị chậm. Bulkhead để dư CPU (băm ≤ số core − 1) cùng hàng chờ ngắn mới giữ được cả hai SLO. `spike.js` mặc định **làm nóng phiên** trong `setup()` (người dùng thật phần lớn đã đăng nhập: access token 15 phút, refresh token 7 ngày) nên đo đúng đặt lịch/duyệt dưới đột biến; năng lực đăng nhập dồn được đo riêng bởi `login-capacity.js` với SLO: p95 lượt được nhận ≤ 1 s, lượt bị shed có p95 ≤ 300 ms và luôn có `Retry-After`, **0 timeout (504)**.

### 2.4. Workflow bất đồng bộ đầu-cuối (đo bằng synthetic probe `workflow-slo.js`)

| Workflow | SLI (từ … đến …) | SLO |
|---|---|---|
| Xác nhận đặt lịch | `POST /appointments` trả 201 → `GET /appointments/{id}` có `status=Confirmed` | **p95 ≤ 5 s**, ≥ 99% xác nhận trong 15 s |
| Check-in → số thứ tự | Walk-in thành công → `GET /queue/appointment/{id}` trả ticket | **p95 ≤ 3 s** |
| Kết thúc khám → lịch hẹn hoàn tất | `POST /queue/{id}/complete-exam` → lịch hẹn `status=Completed` | **p95 ≤ 5 s** |
| Bắt đầu khám → mở encounter bệnh án | `POST /queue/{id}/start-exam` → bác sĩ lưu được nháp theo `AppointmentId` | **p95 ≤ 5 s** |
| Sự kiện → inbox thông báo | Lịch hẹn Confirmed → số thông báo chưa đọc của bệnh nhân tăng | **p95 ≤ 10 s** |
| Gửi qua provider (email/SMS) | Delivery outbox tạo → attempt thành công | **p95 ≤ 60 s** (provider mock trên Compose; chưa có probe tự động) |
| Độ trễ consumer (server-side) | `ConsumerTime − OccurredAt` (`clinic_queue_consumer_lag_seconds`, bucket tường minh 0.005–30 s) | **p95 ≤ 5 s** |
| Tồn outbox | Tuổi bản ghi outbox chờ lâu nhất (`clinic_queue_outbox_oldest_pending_age_seconds`) | **≤ 60 s** |

### 2.5. Tỷ lệ lỗi, toàn vẹn dữ liệu và an toàn

| Chỉ số | SLO | Kiểm chứng |
|---|---|---|
| Tỷ lệ 5xx/timeout tại Gateway | **≤ 1.0%** mỗi cửa sổ 5 phút (Tier 1 mục tiêu nội bộ ≤ 0.5%) | Alert `HighErrorRate5xx`, mọi test k6 |
| Overbooking | **0** — reservation hiệu lực/ca ≤ `RecommendedCapacity`, ≤ 1 reservation hiệu lực/slot | `scripts/check-slo-invariants.ps1`, `contention.js` |
| Phiếu hàng đợi trùng | **0** — trùng `(DoctorId, QueueDate, QueueNumber)` hoặc > 1 ticket/appointment | `check-slo-invariants.ps1` |
| Reservation mồ côi | **0** reservation `Reserved` quá hạn > 1 phút chưa chuyển `Expired` | `check-slo-invariants.ps1` |
| Lịch hẹn treo `Pending` | **0** lịch online `Pending` quá 10 phút (worker phải xác nhận hoặc hủy trước khi hold 10 phút hết hạn) | `check-slo-invariants.ps1` |
| Lịch xác nhận không có chỗ | **0** lịch online `Confirmed` mà reservation không ở trạng thái `Confirmed` | `check-slo-invariants.ps1` |
| Mất sự kiện | **0** message hợp lệ trong DLQ (`.*([.-]dlq\|\.dead)$`) | Alert `DeadLetterQueueNonEmpty` |
| Audit bệnh án | **100%** lượt truy cập/từ chối được ghi `MedicalAccessAudits` | Unit/integration tests Medical Records |
| PHI/credential trong log/trace | **0** | `SensitiveDataSanitization_*` test |

---

## 3. Ma trận SLI theo service owner & nguồn metric

| Service | SLI chính | Nguồn metric / cách đo | SLO (tham chiếu §2) | Owner |
|---|---|---|---|---|
| **API Gateway** | Availability đủ điều kiện; p95/p99 theo nhóm endpoint; tỷ lệ 429 | `http_server_request_duration_seconds_{bucket,count}{job="clinic-api"}`, nhãn `http_route`, `http_response_status_code` | 99.9%; §2.3 | A (route) / B (infra) |
| **Identity** | Tỷ lệ đăng nhập/refresh thành công (loại 401/403); p95 login, refresh | `…{job="clinic-identity-service"}` | 99.9%; login ≤ 1 s, refresh ≤ 300 ms | A |
| **Staff & Scheduling** | Availability reserve/confirm/release; p95 truy vấn slot | `…{job="clinic-scheduling-service"}`; tỷ lệ 503 circuit-open tại Appointment | 99.7%; read ≤ 300 ms | A |
| **Appointment** | Availability đặt lịch (409 là đúng nghiệp vụ); booking → Confirmed | `…{job="clinic-appointment-service"}`; probe `workflow-slo.js` | 99.7%; write ≤ 500 ms; Confirmed ≤ 5 s | A |
| **Queue** | Check-in → ticket; consumer lag; backlog; tồn outbox | `clinic_queue_consumer_lag_seconds_bucket`, `clinic_queue_length`, `clinic_queue_outbox_oldest_pending_age_seconds` | 99.7%; ticket ≤ 3 s; lag ≤ 5 s | A / B |
| **Medical Records** | p95 đọc/ghi nháp; audit; start-exam → encounter | `…{job="clinic-medical-records-service"}`; probe | 99.5%; write ≤ 500 ms; encounter ≤ 5 s | A |
| **Notification** | Event → inbox; delivery attempt; DLQ | `…{job="clinic-notification-service"}`; probe; `rabbitmq_queue_messages` | 99.0%; inbox ≤ 10 s; delivery ≤ 60 s | A |
| **SQL Server** | Readiness, lỗi kết nối | `/readyz` của từng service, `mssql-exporter` | 99.9% | B |
| **RabbitMQ** | DLQ depth, unacked, consumer gắn đủ | `/metrics/per-object` (`rabbitmq_queue_messages{queue=…}`) | 99.9%; DLQ = 0; unacked < 100 | B |

---

## 4. SLA — cam kết dịch vụ (phạm vi đồ án)

> Đây là **SLA mô phỏng cho đồ án** trên Docker Compose, không phải hợp đồng production. Mục này định nghĩa cửa sổ đo, loại trừ, error budget và hành động khi vi phạm để SLO có hệ quả vận hành rõ ràng.

### 4.1. Phạm vi và cửa sổ đo
- **Giờ phục vụ cam kết:** 07:00–20:00 (Asia/Ho_Chi_Minh), Thứ Hai–Thứ Bảy. Ngoài giờ: best-effort, vẫn đo để tham khảo nhưng không tính vào SLA.
- **Chu kỳ SLA:** cửa sổ trượt **30 ngày** (≈ 25.7 ngày làm việc × 13 giờ ≈ **334 giờ** phục vụ).
- **Cam kết SLA:** các SLO E2E ở §2.2 (đặt lịch, check-in/hàng đợi, hồ sơ ≥ 99.0%; đăng nhập ≥ 99.5%), p95 theo §2.3 và mọi invariant ở §2.5.
- **Loại trừ:** bảo trì có kế hoạch thông báo trước ≥ 24 giờ và thực hiện ngoài giờ phục vụ; lỗi do client (4xx theo §1.2); sự cố hạ tầng ngoài phạm vi stack (mất điện/mạng của máy chủ demo).

### 4.2. Error budget

| SLO E2E | Budget / 30 ngày giờ phục vụ |
|---|---:|
| 99.0% | ≈ 3.3 giờ (≈ 200 phút) |
| 99.5% | ≈ 1.7 giờ (≈ 100 phút) |

**Chính sách khi tiêu thụ budget:**
- **≥ 50%** budget trong chu kỳ → ưu tiên sửa lỗi độ tin cậy trong sprint kế tiếp; mọi release phải chạy lại `smoke.js` + `workflow-slo.js` trước khi deploy.
- **≥ 100%** budget → **đóng băng tính năng mới**; chỉ cho phép thay đổi sửa lỗi/độ tin cậy cho tới khi tỷ lệ lỗi 7 ngày gần nhất trở lại dưới mục tiêu; lập postmortem.
- Vi phạm **invariant** (§2.5) bất kể budget → sự cố nghiêm trọng: dừng release, chạy `check-slo-invariants.ps1` và đối soát dữ liệu.

### 4.3. Cảnh báo theo burn-rate (multi-window)
Cảnh báo theo tốc độ tiêu thụ budget thay vì một ngưỡng tức thời (cấu hình trong `observability/alert.rules.yml`):

| Alert | Điều kiện (SLO 99.0%, budget 1%) | Ý nghĩa | Mức |
|---|---|---|---|
| `GatewayErrorBudgetFastBurn` | tỷ lệ lỗi > **14.4%** trên cả cửa sổ **1h** và **5m** | Tiêu ≈ 2% budget/giờ | critical (page) |
| `GatewayErrorBudgetSlowBurn` | tỷ lệ lỗi > **6%** trên cả cửa sổ **6h** và **30m** | Tiêu ≈ 5% budget/6 giờ | warning (ticket) |
| `BookingErrorBudgetFastBurn` | như fast burn nhưng chỉ route đặt lịch `appointments*` | Hành trình Tier 1 | critical |

Các alert ngưỡng tức thời (`LowApiAvailability`, `HighApiLatencyP95/P99`, `HighErrorRate5xx`, `QueueConsumerLagHigh`, `DeadLetterQueueNonEmpty`, `OutboxBacklogStale`) giữ vai trò phát hiện nhanh.

### 4.4. Escalation (vai trò trong nhóm)
1. Alert critical → Member B (hạ tầng) kiểm tra Prometheus/Jaeger/RabbitMQ trong 15 phút giờ phục vụ.
2. Nếu nguyên nhân thuộc domain service → chuyển Member A (owner theo §3).
3. Sau khi khôi phục: ghi postmortem (thời gian, budget tiêu thụ, nguyên nhân, hành động phòng ngừa) vào `docs/`.

---

## 5. Chính sách QoS

### 5.1. Lan truyền ngữ cảnh & tracing
- Header chuẩn `X-Correlation-ID`: Gateway giữ nguyên giá trị client gửi **nếu là GUID hợp lệ**; nếu thiếu hoặc không phải GUID (chặn log injection) thì tự sinh GUID mới. Giá trị được gắn vào mọi request downstream (`CorrelationIdDelegatingHandler`) và trả lại trong response (kiểm chứng: `qos-policy.js`, `CorrelationId_Propagates_*`).
- OpenTelemetry trace xuất qua OTLP tới Jaeger (`clinic_jaeger:4317`); metrics Prometheus tại `/metrics` của từng service.

### 5.2. Deadline, retry và circuit breaker cho lời gọi đồng bộ
Triển khai bằng `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`); `HttpClient.Timeout` đặt vô hạn để chỉ pipeline resilience quyết định deadline. Deadline lồng nhau để hop trong luôn hết hạn trước hop ngoài.

| Hop | Total deadline | Per-attempt | Retry | Circuit breaker | Mã trả về khi lỗi |
|---|---:|---:|---|---|---|
| Gateway → mọi service (trừ Appointment) | **5 s** | **3 s** | Tối đa **2** lần, backoff mũ 200 ms có jitter, **chỉ method an toàn** (GET/HEAD/OPTIONS) | Mở khi ≥ **50%** lỗi trong **30 s** (tối thiểu **20** lời gọi), mở **15 s** rồi half-open | Hết deadline → **504**; circuit mở → **503** + `Retry-After`; mất kết nối → **502** |
| Gateway → Appointment | **5 s** | **4 s** | như trên (POST không bao giờ retry ở Gateway) | như trên | như trên |
| Appointment → Scheduling | **3 s** | **1.5 s** | **1** lần, backoff 100 ms có jitter — áp cho cả POST vì mọi lệnh Scheduling đều idempotent (reserve theo `Idempotency-Key`, confirm/release theo reservation ID) | như trên | Appointment trả **503** (+`Retry-After: 15`) / **504** / **502** thay cho 500; release bù trừ là best-effort (không làm hỏng request đã commit) |
| Gateway → Identity | **10 s** | **9 s** | như trên (đăng nhập là POST nên không retry) | như trên; response có `X-Clinic-Load-Shed` **không** tính là lỗi | Deadline dài là lưới an toàn; quá năng lực BCrypt thì Identity tự shed bằng **503 + `Retry-After`** trước khi chạm deadline (§2.3) |
| Gateway → Notification hub (SignalR) | 130 s | — | không | không | Client riêng `NotificationHub` vì long-polling/SSE kéo dài theo thiết kế |

**Không lan truyền lỗi của phụ thuộc (cascade):** khi Appointment trả 5xx vì *Scheduling* gặp sự cố, response mang header `X-Clinic-Dependency-Failure: scheduling`. Retry và circuit breaker của Gateway bỏ qua các response này vì bản thân Appointment vẫn khỏe; nếu không, sự cố Scheduling sẽ làm mở breaker Gateway→Appointment và chặn luôn các route không liên quan như xem lịch hẹn đã có (lỗi này đã được phát hiện bằng `fault-isolation.js` và kiểm chứng bởi `DependencyFlaggedFailures_DoNotOpenTheBreaker_*`). Tương tự, 503 có header `X-Clinic-Load-Shed` (Identity chủ động từ chối vì hết năng lực băm mật khẩu) không mở breaker Gateway→Identity (`IdentityLoadShedding_IsPassedThrough_AndDoesNotOpenTheBreaker`).

Các tham số chỉnh được qua cấu hình `QoS:Downstream:*` và `QoS:Scheduling:*` (dùng trong integration test).

### 5.3. Rate limiting (Gateway)
Hai lớp limiter nối chuỗi, cửa sổ cố định 60 s, `QueueLimit = 0`; request vượt hạn mức nhận **429** + `Retry-After` (số giây còn lại của cửa sổ) trong envelope `ApiResponse`.

| Lớp | Phân vùng | Hạn mức mặc định / phút | Biến môi trường Compose |
|---|---|---:|---|
| Chung — bệnh nhân / user thường | `sub` claim | **100** | `RATE_LIMIT_PERMIT_LIMIT` |
| Chung — nhân viên (Admin/Receptionist/Doctor) | `sub` claim | **600** (màn hình hàng đợi polling liên tục) | `STAFF_RATE_LIMIT_PERMIT_LIMIT` |
| Chung — ẩn danh | IP kết nối thật | **100** | `ANONYMOUS_RATE_LIMIT_PERMIT_LIMIT` |
| Nhạy cảm — `/auth/login`, `/auth/register`, `/forgot-password`, `/reset-password` | IP kết nối thật | **10** (chống dò mật khẩu) | `AUTH_RATE_LIMIT_PERMIT_LIMIT` |
| Nhạy cảm — `POST /appointments` | `sub` claim | **10** (chặn giữ chỗ hàng loạt) | `BOOKING_RATE_LIMIT_PERMIT_LIMIT` |

- **Lớp phòng thủ thứ hai tại Identity:** các route xác thực của Identity giới hạn **20 lượt/phút/client** (`QoS__AuthPermitLimit`, biến Compose `IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT`). Phía sau Gateway, IP kết nối TCP luôn là Gateway, nên Gateway chuyển IP thật qua header `X-Clinic-Client-IP` kèm service token; Identity chỉ tin header này khi token hợp lệ (client bên ngoài không giả mạo được). Trước bản sửa này toàn hệ thống dùng chung **20 lượt đăng nhập/phút** (kiểm chứng: `IdentityAuthRateLimitTests`).
- **Giới hạn đồng thời theo tài nguyên (Identity):** ngoài hạn mức theo thời gian ở trên, mọi thao tác BCrypt (đăng nhập, đăng ký, đổi/đặt lại mật khẩu, provisioning) đi qua một `ConcurrencyLimiter` dùng chung (`QoS:PasswordHashConcurrency` = số core − 1, trên Compose là 1; `QoS:PasswordHashQueueLimit` = 4). Rate limit chặn lạm dụng *theo client*; concurrency limit bảo vệ *CPU của Identity* khi nhiều client hợp lệ cùng đăng nhập. Bị từ chối → 503 + `Retry-After` + `X-Clinic-Load-Shed`, đếm bằng metric `clinic_identity_password_hash_rejected` (kiểm chứng: `PasswordHashingTests`, `login-capacity.js`).
- **Miễn trừ:** `/healthz`, `/readyz`, `/metrics`, `/openapi/*`, `/swagger/*`.
- **Trust boundary:** Gateway không đứng sau reverse proxy trong Compose nên **không** bật `UseForwardedHeaders` và không tin `X-Forwarded-For` (test `RateLimiter_CannotBeBypassed_BySpoofingXForwardedForHeader`).
- **Khi chạy tải:** k6 phát mọi request từ **một IP**, nên chỉ nâng tạm các hạn mức theo IP: `AUTH_RATE_LIMIT_PERMIT_LIMIT`, `ANONYMOUS_RATE_LIMIT_PERMIT_LIMIT` và `IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT`. Hạn mức **theo user** giữ nguyên giá trị production vì test dùng pool tài khoản riêng biệt (xem `tests/load/README.md`).

### 5.4. Chế độ suy giảm (degraded modes) theo phụ thuộc

| Phụ thuộc gặp sự cố | Hành vi bắt buộc | Kiểm chứng |
|---|---|---|
| **Notification** down | Đặt lịch, check-in, hàng đợi **vẫn đạt SLO**; chỉ thông báo bị trễ (sự kiện nằm trong RabbitMQ/outbox) | `fault-isolation.js` (`FAULT_TARGET=notification-service`) |
| **Medical Records** down | Đặt lịch/hàng đợi không bị ảnh hưởng; `ExamStarted/ExamCompleted` được giữ trong queue và xử lý khi phục hồi | `fault-isolation.js` (`FAULT_TARGET=medical-records-service`) |
| **Scheduling** down | Đặt lịch mới thất bại **nhanh** với 503/504 (không treo, không 500); xem lịch hẹn đã có, hàng đợi, hồ sơ **vẫn hoạt động**; không để lại reservation mồ côi; tự phục hồi khi service lên lại (circuit half-open) | `fault-isolation.js` (`FAULT_TARGET=scheduling-service`), `GatewayQoSPolicyTests`, `check-slo-invariants.ps1` |
| **RabbitMQ** down | API đồng bộ vẫn trả lời; sự kiện được giữ trong outbox và phát lại khi broker phục hồi; DLQ = 0 | `scripts/run-degradation-test.ps1` |
| **Identity** down | Request đã có token **tiếp tục hoạt động** tới khi token hết hạn (JWT kiểm tra cục bộ bằng JWKS đã cache); chỉ đăng nhập/refresh thất bại | `fault-isolation.js` (`FAULT_TARGET=identity-service`) |

### 5.5. Retry bất đồng bộ & idempotency

| Cơ chế | Chính sách |
|---|---|
| Consumer Appointment / Scheduling / Medical Records | Retry **3** lần qua queue trễ 5 s → 30 s → 2 phút; lỗi định dạng (`JsonException`/`FormatException`) vào DLQ ngay; quá 3 lần → DLQ |
| Consumer Notification | Tối đa **3** retry rồi DLQ `clinic.notification.events.dlq` |
| Outbox publisher (Appointment/Scheduling/Queue) | Publisher confirm; message lỗi lặp lại được chuyển `*.outbox-dlq` |
| `AppointmentConfirmationWorker` | **4** lần thử, trễ 10 s → 1 phút → 5 phút (tổng ≈ 6 phút 10 s, **nhỏ hơn hold 10 phút** của reservation); thất bại → hủy lịch và release reservation; bị circuit/deadline từ chối → giữ `Pending` và thử lại theo lịch |
| Hold reservation | Hết hạn sau **10 phút**; `ReservationExpirationWorker` quét mỗi 15 s |
| Notification delivery | Tối đa **3** attempt với backoff |
| Idempotency `POST /appointments` | Header `Idempotency-Key` (khuyến nghị); khóa được kiểm tra theo appointment và theo (schedule, slot, patient) ở Scheduling; lặp lại trả cùng appointment |
| Idempotency check-in / walk-in | `Idempotency-Key` **bắt buộc** tại Gateway |
| Idempotency consumer | Bảng inbox theo `(EventId, ConsumerName)` + ràng buộc unique nghiệp vụ (1 ticket/appointment) |

### 5.6. Khôi phục dữ liệu (RPO / RTO)

Thiết kế đầy đủ, runbook và cách demo: [`docs/BACKUP_RECOVERY.md`](../docs/BACKUP_RECOVERY.md). Sidecar `db-backup` (SQL Server Express không có SQL Agent) lập lịch theo `backup/policy.conf`, kết hợp nhiều phương pháp: **full hằng tuần + differential + transaction-log** (khôi phục tới từng thời điểm), kiểm tra `CHECKSUM` + `RESTORE VERIFYONLY` sau mỗi bản full/diff, **bản sao off-site** nén gzip kèm sha256, lưu trữ volume khóa (JWT signing keys, data-protection keys), retention theo chuỗi và **diễn tập khôi phục định kỳ** (restore cạnh bên + `DBCC CHECKDB`, đo RPO/RTO thật).

| Dữ liệu | Recovery model | Lịch backup | RPO mục tiêu | Mất dữ liệu tối đa theo thiết kế | RTO mục tiêu |
|---|---|---|---:|---:|---:|
| Medical Records DB | `FULL` | full 7 ngày · diff 4 giờ · **log 5 phút** | ≤ 15 phút | ≈ 6 phút (log + 1 tick) | ≤ 1 giờ |
| Appointment / Scheduling / Queue DB | `FULL` | full 7 ngày · diff 6 giờ · **log 15 phút** | ≤ 1 giờ | ≈ 16 phút | ≤ 1 giờ |
| Identity DB (+ volume khóa ký JWT) | `SIMPLE` | full 7 ngày · diff 6 giờ · khóa: mỗi khi đổi / hằng ngày | ≤ 24 giờ | ≈ 6 giờ | ≤ 1 giờ |
| Notification DB (+ volume data-protection keys) | `SIMPLE` | full 7 ngày · diff 12 giờ | ≤ 24 giờ | ≈ 12 giờ | ≤ 4 giờ |

- **Khôi phục tại chỗ khi DB còn đọc được:** `restore.sh --replace` chụp **tail-log** trước khi khôi phục → RPO = 0 cho các DB `FULL`.
- **SLI độ bền (durability):** `status.sh` tính *exposure* = tuổi của bản backup mới nhất trên đĩa (lượng dữ liệu sẽ mất nếu đĩa hỏng ngay lúc này) và so với RPO; cũng là healthcheck của container và một mục trong `check-slo-invariants.ps1`.
- **Bằng chứng:** `scripts/backup.ps1 drill` ghi `tests/load/evidence/backup-drill.json` (thời gian restore = RTO đo được, RPO đo được, kết quả `CHECKDB`, cho cả bản chính và bản off-site).
- **Giới hạn phạm vi đồ án:** "off-site" là thư mục host (`BACKUP_OFFSITE_DIR`) — trỏ sang ổ đĩa khác hoặc thư mục đồng bộ cloud để có bản sao thật sự ở nơi khác. Hàng đợi RabbitMQ không được backup: sự kiện chưa phát nằm trong outbox của DB nên được khôi phục cùng DB.

### 5.7. Khử nhạy cảm telemetry & log
- Không ghi mật khẩu, refresh/access token vào log, metric label hoặc trace baggage.
- Không ghi PHI (`Diagnosis`, `ClinicalNotes`, `TreatmentPlan`, …) vào span tag, event hay metric label.
- Kiểm chứng bởi `SensitiveDataSanitization_LogsAndTelemetryNeverContainPasswordsOrPHI`.

---

## 6. Ma trận kiểm chứng (policy → test)

| Chính sách / SLO | Kiểm chứng tự động | Loại |
|---|---|---|
| Availability, p95/p99 theo nhóm endpoint, 5xx ≤ 1% | Mọi script k6 dùng chung ngưỡng trong `tests/load/lib/slo.js` | Tải |
| So sánh công bằng monolith ↔ microservices | `load.js` (constant-arrival-rate, cùng tốc độ cho hai hệ thống) | Tải (open model) |
| Năng lực tối đa / điểm gãy SLO | `breakpoint.js` (ramping-arrival-rate, dừng khi vi phạm p95/5xx) | Tải (open model) |
| Hàng nghìn người dùng đồng thời, hạn mức theo user bật | `mixed-workload.js` profile `many-users` (pool tài khoản riêng, think time 15–45 s) | Tải (user model) |
| Hành vi người dùng giờ thường / giờ cao điểm | `mixed-workload.js` profile `normal` / `clinic-peak` | Tải (user model) |
| Đột biến mở lịch | `spike.js` (bệnh nhân khác nhau dồn vào ít bác sĩ; phiên được làm nóng trước, `SPIKE_WARM_SESSIONS=0` để đo đăng nhập lạnh) | Tải |
| Trôi hiệu năng dài hạn | `soak.js` (mặc định 60 phút, so sánh p95 đầu/cuối) | Tải |
| Ghi cường độ cao | `booking-high-load.js` (arrival rate, pool bệnh nhân, kiểm tra kích thước fixture) | Tải |
| 0 overbooking khi tranh chấp | `contention.js` (N bệnh nhân khác nhau, 1 slot, đúng 1 người thắng) | Tải + invariant |
| Workflow bất đồng bộ §2.4 | `workflow-slo.js` | Synthetic probe |
| Rate limit theo tầng & route §5.3 trên stack thật | `qos-policy.js`; `GatewayQoSPolicyTests`, `QoSFailurePolicyTests` | Tải chức năng + integration |
| Deadline/retry/circuit breaker §5.2 | `GatewayQoSPolicyTests` (retry GET, không retry POST, 504 theo deadline, 503 fail-fast + phục hồi, lỗi phụ thuộc không mở breaker), `SchedulingDependencyQoSTests` | Integration / unit |
| Rate limit theo client thật tại Identity §5.3 | `IdentityAuthRateLimitTests` | Integration (SQL Server) |
| Năng lực đăng nhập & load shedding §2.3, §5.3 | `login-capacity.js` (ramp lượt đăng nhập, 0 × 504, shed nhanh có `Retry-After`); `PasswordHashingTests`; `IdentityLoadShedding_IsPassedThrough_AndDoesNotOpenTheBreaker` | Tải + unit + integration |
| Phân quyền hàng đợi nhất quán giữa seed và importer | `QueuePermissionCatalogTests` | Unit |
| Backup / RPO / RTO §5.6 | `scripts/backup.ps1 drill` (restore + CHECKDB, bản chính và off-site), `scripts/backup.ps1 status` / healthcheck `db-backup`, mục backup trong `check-slo-invariants.ps1` | Diễn tập khôi phục |
| Degraded modes §5.4 | `scripts/run-fault-isolation.ps1` + `fault-isolation.js`; `scripts/run-degradation-test.ps1` (broker) | Fault injection |
| Invariant §2.5 | `scripts/check-slo-invariants.ps1` (chạy sau mọi test ghi; exit code ≠ 0 khi vi phạm) | Đối soát DB |
| Alert lifecycle | `scripts/test-alert-lifecycle.ps1` | Vận hành |
| Contract & bảo mật API | `ApiContractDriftTests` | Integration |

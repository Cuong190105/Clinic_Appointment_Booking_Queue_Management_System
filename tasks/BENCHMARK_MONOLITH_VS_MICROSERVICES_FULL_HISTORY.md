# Báo Cáo Lưu Trữ Chi Tiết Toàn Bộ Lịch Sử Đo Kiểm & Tối Ưu Hiệu Năng: Monolith vs Microservices
**Ngày thực hiện:** 11/11/2026  
**Phương pháp kiểm thử:** Chạy tuần tự độc lập (mỗi thời điểm chỉ bật duy nhất 1 hệ thống, sau khi test xong tắt hoàn toàn `docker compose down`, chờ máy chủ hạ nhiệt ổn định rồi mới bật hệ thống kia).  
**Công cụ đo kiểm:**
1. **`tests/load/benchmark-runner.mjs` (`SCENARIO=all`):** Đo 4 tác vụ nối tiếp: Business Read (`GET /specialties`, 50 reqs, concurrency 10), Booking API & E2E SAGA (`POST /appointments` + poll tới `Confirmed` + kiểm tra Idempotency, 20 bookings, concurrency 2), Check-in & Duplicate Guard (`POST /appointments/{id}/check-in`, 4 reqs tuần tự), và Cleanup Verification.
2. **Grafana `k6` (`tests/load/booking.js`):** Đo tải đồng thời cao với **20 Virtual Users (20 VUs)** cùng lúc kiểm tra slot trống, gửi `POST /appointments`, poll tới khi đạt `Confirmed`, gửi lại request cùng `Idempotency-Key`, và hủy lịch hẹn sau khi hoàn tất. Ngưỡng PASS/FAIL tự động:
   - `booking_api_duration`: **`p(95) < 500ms`**, **`p(99) < 1500ms`**
   - `booking_e2e_duration`: **`p(95) < 5000ms`**
   - `booking_success_rate`, `booking_confirmed_rate`, `booking_idempotency_verified_rate`, `booking_cleanup_verified_rate`: **`rate > 0.98`**

---

# MỤC LỤC CÁC GIAI ĐOẠN TRONG TÀI LIỆU
- **PHẦN I:** Cấu hình & Kết quả Đo Lường Gốc (**Giai đoạn 1 — Baseline Benchmark**)
- **PHẦN II:** Phân Tích Điểm Nghẽn Tài Nguyên & Cấu Hình Tái Phân Bổ + Polling 100ms (**Giai đoạn 2 — Hotspot Rebalancing + 100ms Polling**)
- **PHẦN III:** Rà Soát Độ Trễ Đuôi `p95 / p99` Dưới Tải 20 VUs `k6` — Các Lượt **FAIL (`exit code: 99`)** & Nguyên Nhân Gốc Rễ (**Giai đoạn 3**)
- **PHẦN IV:** Chi Tiết Cách Cấu Hình & Tối Ưu Mã Nguồn Toàn Diện Trên Cả 2 Hệ Thống — Đạt **PASS (`k6 exit code: 0`)** (**Giai đoạn 4 — Final Optimized**)
- **PHẦN V:** Bảng Tổng Hợp Đối Chiếu Tất Cả Các Lượt Chạy (Cũ & Mới, FAIL & PASS, Đầy Đủ `min / p50 / p90 / p95 / p99 / max`)

---

# PHẦN I: GIAI ĐOẠN 1 — ĐO LƯỜNG GỐC (BASELINE BENCHMARK)

## 1. Cách Cấu Hình Từng Hệ Thống Ở Giai Đoạn 1 (Baseline)

Để tránh bị ảnh hưởng bởi các tiến trình nền trên máy tính (Host: 12 cores / 16 threads, 16 GB RAM), cả hai hệ thống được áp giới hạn `limits` và `reservations` trong Docker Compose:

### 1.1. Cấu hình Monolith — Giai đoạn 1 ([`docker-compose.monolith.yml`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/docker-compose.monolith.yml))
- **Kiến trúc:** 1 Backend API (`clinic_mono_backend`, in-process MediatR) + 1 SQL Server (`clinic_mono_sql_server`) + 1 Frontend (`clinic_mono_frontend`).
- **Connection String ban đầu:** Chưa cấu hình `Min Pool Size` (mặc định `Min Pool Size=0, Max Pool Size=100`).
- **Phân bổ CPU / RAM:**

| Container | CPU Limits | CPU Reservations | Memory Limits | Memory Reservations |
| :--- | :---: | :---: | :---: | :---: |
| `clinic_mono_sql_server` (`mono-db`) | `4.00 CPU` | `2.00 CPU` | `2048 MB` | `1024 MB` |
| `clinic_mono_backend` (`mono-backend`) | `4.00 CPU` | `2.00 CPU` | `1536 MB` | `512 MB` |
| `clinic_mono_frontend` (`mono-frontend`) | `1.00 CPU` | `0.25 CPU` | `256 MB` | `64 MB` |
| **Tổng cộng Monolith** | **`9.00 CPU`** | **`4.25 CPU`** | **`3840 MB (3.75 GiB)`** | **`1600 MB`** |

### 1.2. Cấu hình Microservices — Giai đoạn 1 (Balanced Baseline - [`docker-compose.yml`](file:///D:/Project/KHDV/microservices/docker-compose.yml))
- **Kiến trúc:** 1 API Gateway (`clinic_backend`) + 6 Microservices (`identity`, `scheduling`, `appointment`, `queue`, `medical-records`, `notification`) + 1 RabbitMQ + 1 SQL Server + 3 Telemetry/Frontend containers.
- **Chu kỳ Worker / Outbox ban đầu:**
  - `AppointmentConfirmationWorker`: Polling mặc định **`1000 ms`**.
  - `AppointmentOutboxPublisher` & `SchedulingOutboxPublisher`: Polling mặc định **`1000 ms`**.
- **Connection String ban đầu:** Mặc định (`Min Pool Size=0`).
- **Phân bổ CPU / RAM (Cào bằng giữa các core services):**

| Container / Service | CPU Limits (GĐ 1) | CPU Reservations (GĐ 1) | Memory Limits (GĐ 1) | Memory Reservations (GĐ 1) |
| :--- | :---: | :---: | :---: | :---: |
| `clinic_sql_server` (`db`) | `4.00 CPU` | `2.00 CPU` | `2048 MB` | `1024 MB` |
| `rabbitmq` | `1.50 CPU` | `0.50 CPU` | `512 MB` | `256 MB` |
| `clinic_backend` (Gateway) | `1.00 CPU` | `0.50 CPU` | `384 MB` | `128 MB` |
| `identity-service` | `0.75 CPU` | `0.25 CPU` | `256 MB` | `128 MB` |
| `clinic_scheduling_service` | `0.75 CPU` | `0.25 CPU` | `256 MB` | `128 MB` |
| `clinic_appointment_service` | `0.75 CPU` | `0.25 CPU` | `256 MB` | `128 MB` |
| `clinic_queue_service` | `0.75 CPU` | `0.25 CPU` | `256 MB` | `128 MB` |
| `clinic_medical_records_service` | `0.50 CPU` | `0.15 CPU` | `256 MB` | `128 MB` |
| `clinic_notification_service` | `0.50 CPU` | `0.15 CPU` | `256 MB` | `128 MB` |
| `clinic_frontend` | `1.00 CPU` | `0.25 CPU` | `256 MB` | `64 MB` |
| `clinic_prometheus` | `0.50 CPU` | `0.10 CPU` | `256 MB` | `64 MB` |
| `clinic_jaeger` | `0.50 CPU` | `0.10 CPU` | `256 MB` | `64 MB` |
| `mssql-exporter` | `0.25 CPU` | — | `128 MB` | — |
| **Tổng cộng Microservices (GĐ 1)** | **`12.75 CPU`** | **`4.75 CPU`** | **`5376 MB (5.25 GiB)`** | **`2368 MB`** |

---

## 2. Kết Quả Chi Tiết Giai Đoạn 1 (Baseline) — Đầy Đủ `min / p50 / p90 / p95 / p99 / max`

- **File bằng chứng Monolith (GĐ 1):** [`benchmark-monolith-2026-11-11T05-58-59-764Z.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/benchmark-monolith-2026-11-11T05-58-59-764Z.json)
- **File bằng chứng Microservices (GĐ 1):** [`benchmark-microservices-2026-11-11T06-15-17-036Z.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/benchmark-microservices-2026-11-11T06-15-17-036Z.json)

| Tác vụ & Chỉ số (`benchmark-runner.mjs`) | Monolith (Baseline GĐ 1) | Microservices (Baseline GĐ 1 - Poll 1000ms) | Nhận xét Giai đoạn 1 |
| :--- | :---: | :---: | :--- |
| **1. Business Read (`GET /specialties`, 50 reqs, c=10)** | | | |
| • Thành công / Lỗi | 50 / 0 (100% HTTP 200) | 50 / 0 (100% HTTP 200) | Cả hai không có lỗi. |
| • Throughput (RPS) | **324.7 req/s** | **231.5 req/s** | Monolith nhanh hơn nhờ gọi DB nội bộ. |
| • `min` / `avg` | 8.33 ms / 29.37 ms | 9.81 ms / 41.67 ms | |
| • **`p50` / `p90` / `p95` / `p99`** | **11.35 / 103.46 / 114.92 / 118.83 ms** | **45.21 / 73.87 / 80.91 / 96.57 ms** | p50 Monolith thấp hơn, p95/p99 Microservices ổn định. |
| **2. Booking API (`POST /appointments`, 20 reqs, c=2)** | | | |
| • Thành công / Idempotency | 20/20 Created, 20/20 Idempotent | 20/20 Created, 20/20 Idempotent | Đúng nghiệp vụ 100%. |
| • `min` / `avg` | 23.33 ms / 41.75 ms | 31.86 ms / 49.62 ms | |
| • **`p50` / `p90` / `p95` / `p99`** | **27.11 / 38.80 / 318.02 / 318.02 ms** | **45.18 / 87.83 / 99.18 / 99.18 ms** | Ở concurrency=2, API trả về nhanh nhưng Monolith có 1 spike 318ms. |
| **3. Booking E2E (`Confirmed`, 20 reqs, c=2)** | | | |
| • `min` / `avg` | 31.79 ms / 58.52 ms | 259.19 ms / 982.00 ms | |
| • **`p50` / `p90` / `p95` / `p99`** | **42.63 / 49.73 / 416.34 / 416.34 ms** | **1019.91 / 1068.40 / 1073.46 / 1073.46 ms** | **Điểm nghẽn 1:** Microservices mất ~1 giây mới `Confirmed` do worker sleep 1000ms. |
| • Độ trễ SAGA (Async Lag `p50 / p95`) | 15.52 ms / 98.32 ms | **974.73 ms / 974.27 ms** | |
| **4. Check-in (`POST /check-in`, 4 reqs tuần tự)** | | | |
| • Lần đầu / Chặn retry trùng | 1 (`204`) / 3 (`400`) | 1 (`204`) / 3 (`409`) | Bảo toàn đúng 1 vé khám duy nhất. |
| • `min` / `avg` | 15.11 ms / 35.13 ms | 8.10 ms / 28.39 ms | |
| • **`p50` / `p90` / `p95` / `p99`** | **15.91 / 94.07 / 94.07 / 94.07 ms** | **40.47 / 43.76 / 43.76 / 43.76 ms** | |

---

# PHẦN II: GIAI ĐOẠN 2 — TÁI PHÂN BỔ TÀI NGUYÊN (HOTSPOT) & HẠ POLLING 100MS

## 1. Cách Cấu Hình Thay Đổi Ở Giai Đoạn 2

Từ số liệu `docker stats` ở Giai đoạn 1 (`RabbitMQ` chỉ dùng `0.52% CPU` dù được cấp `1.50 CPU`, trong khi `appointment-service` dùng `51.97%` và `queue-service` dùng `64.18%` trên giới hạn `0.75 CPU`), hệ thống Microservices được thử nghiệm phương án:
1. **Tái phân bổ CPU/RAM (Profile Hotspot):**
   - Giảm `rabbitmq`: `1.50 CPU / 512 MB` -> `0.50 CPU / 256 MB`
   - Giảm `identity-service`: `0.75 CPU` -> `0.25 CPU`
   - Giảm `medical-records-service`: `0.50 CPU / 256 MB` -> `0.25 CPU / 192 MB`
   - Giảm `prometheus` & `jaeger`: mỗi container `0.50 CPU / 256 MB` -> `0.25 CPU / 192 MB`
   - Tăng `appointment-service`: `0.75 CPU / 256 MB` -> **`1.50 CPU / 384 MB`**
   - Tăng `scheduling-service`: `0.75 CPU / 256 MB` -> **`1.25 CPU / 384 MB`**
   - Tăng `queue-service`: `0.75 CPU` -> **`1.25 CPU`**
2. **Hạ chu kỳ Polling (Busy-Polling 100ms):**
   - Hạ `Outbox:PollingIntervalMs` và `Sagas:ConfirmationIntervalMs` từ `1000ms` xuống **`100ms`**.

## 2. Kết Quả Chi Tiết Giai Đoạn 2 (`benchmark-microservices-2026-11-11T08-10-54-929Z.json`)

- **File bằng chứng:** [`benchmark-microservices-2026-11-11T08-10-54-929Z.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/benchmark-microservices-2026-11-11T08-10-54-929Z.json)
- **Ưu điểm đạt được (ở `p50`):**
  - Read `p50` giảm từ `45.21 ms` xuống **`24.05 ms`**.
  - Booking E2E `p50` giảm từ `1019.91 ms` xuống **`221.40 ms`** (Async Lag `p50` giảm từ `974.73 ms` xuống **`147.73 ms`**).
  - Check-in `p50` giảm từ `40.47 ms` xuống **`33.05 ms`**.
- **Nhược điểm nghiêm trọng phát sinh (ở `p95 / p99`):**
  - Read `p90 / p95 / p99`: tăng từ `73.87 / 80.91 / 96.57 ms` lên **`265.00 / 266.87 / 270.58 ms`** (Throughput giảm từ `231.5 RPS` xuống `138.1 RPS`).
  - Booking API (`POST /appointments`) `p95 / p99`: vọt từ `99.18 ms` lên **`1219.36 ms`**!
  - Booking E2E (`Confirmed`) `p95 / p99`: vọt từ `1073.46 ms` lên **`1435.31 ms`**!
  - Check-in `p90 / p95 / p99`: tăng từ `43.76 ms` lên **`82.84 ms`**.

---

# PHẦN III: GIAI ĐOẠN 3 — RÀ SOÁT ĐỘ TRỄ ĐUÔI P95/P99 DƯỚI TẢI 20 VUs (`k6`) & CÁC LƯỢT FAIL (`EXIT CODE: 99`)

Khi chạy kịch bản kiểm thử chịu tải đồng thời cao `tests/load/booking.js` bằng **Grafana k6 (20 VUs cùng lúc đặt lịch)** trên Monolith và 3 cấu hình Microservices trước tối ưu mã nguồn, **cả 4 lượt đều bị FAIL ngưỡng `booking_api_duration p(95) < 500ms` (`k6 exit code: 99`)**:

## 1. Bảng Ghi Nhận Đầy Đủ 4 Lượt Chạy K6 Bị FAIL (`exit code: 99`)

| Cấu hình Hệ thống (Trước khi tối ưu Code & DB Pool) | File Bằng Chứng (`tests/load/evidence/`) | `POST /appointments` (`booking_api_duration`)<br>`min` / `p50` / `p90` / **`p95`** / **`max (p99)`** | Hoàn tất `Confirmed` (`booking_e2e_duration`)<br>`min` / `p50` / `p90` / **`p95`** / **`max (p99)`** | Toàn bộ HTTP (`http_req_duration`)<br>`p50` / `p90` / **`p95`** / `max` | Trạng thái K6 & Lý do |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **1. Monolith (Cũ)** | [`review-mono-booking-k6.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/review-mono-booking-k6.json) | 315.2 / 522.8 / 690.4 / **`706.7`** / **726.4 ms** | 487.0 / 617.5 / 765.7 / **`790.8`** / **805.0 ms** | 25.3 / 500.4 / **598.5** / 948.4 ms | ❌ **FAIL (`exit 99`)**<br>(`p95 = 706.7ms > 500ms`) |
| **2. Microservices Balanced (Poll 1000ms)** | [`review-balanced1000-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-balanced1000-booking-k6.json) | 1769.7 / 1823.0 / 1893.7 / **`1902.5`** / **1935.1 ms** | 2621.0 / 3178.0 / 3401.0 / **`3437.1`** / **3438.0 ms** | 69.6 / 302.7 / **1804.1** / 1935.1 ms | ❌ **FAIL (`exit 99`)**<br>(`p95 = 1902.5ms > 500ms`, `p99 > 1500ms`) |
| **3. Microservices Balanced (Poll 100ms)** | [`review-balanced100-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-balanced100-booking-k6.json) | 2008.2 / 2191.0 / 2350.5 / **`2351.7`** / **2372.0 ms** | 2889.0 / 3895.5 / 4360.1 / **`4383.8`** / **4474.0 ms** | 71.1 / 215.5 / **2119.5** / 2372.0 ms | ❌ **FAIL (`exit 99`)**<br>(`p95 = 2351.7ms > 500ms`, tệ hơn cả 1000ms!) |
| **4. Microservices Hotspot (Poll 100ms)** | [`review-hotspot100-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-hotspot100-booking-k6.json) | 989.9 / 1071.9 / 1165.3 / **`1171.9`** / **1224.4 ms** | 1451.0 / 1939.5 / 2252.2 / **`2259.3`** / **2360.0 ms** | 33.5 / 395.0 / **1058.8** / 1909.2 ms | ❌ **FAIL (`exit 99`)**<br>(`p95 = 1171.9ms > 500ms`) |

---

# PHẦN IV: GIAI ĐOẠN 4 — CÁCH TỐI ƯU HÓA TOÀN DIỆN HỆ THỐNG & ĐẠT PASS 100% (`K6 EXIT CODE: 0`)

## 1. Năm Nguyên Nhân Gốc Rễ & Cách Tối Ưu Mã Nguồn Chi Tiết

### 1.1. Tối ưu 1 (Microservices): Thay thế Busy-Polling 100ms bằng Tín hiệu Đánh thức Tức thì (`IAppointmentWorkSignal`)
- **Vấn đề cũ:** Hạ chu kỳ quét DB xuống `100ms` khiến 3 background workers liên tục bắn `30 queries/giây` vào SQL Server ngay cả khi rảnh và gây tranh chấp Connection Pool khi đang có burst 20 VUs.
- **Cách khắc phục:**
  - Tạo mới [`AppointmentWorkSignal.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentWorkSignal.cs) sử dụng `SemaphoreSlim(0, 1)` không khóa luồng (`WaitForConfirmationAsync`, `WaitForOutboxAsync`, `SignalConfirmation`, `SignalOutbox`) và đăng ký Singleton trong [`Program.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Program.cs).
  - Ngay sau khi [`AppointmentManager.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentManager.cs) lưu DB (`SaveChangesAsync`), hàm gọi `_workSignal?.SignalConfirmation()` và `_workSignal?.SignalOutbox()` để đánh thức worker ngay lập tức trong **`< 1ms`**.
  - Trả chu kỳ polling mặc định khi rảnh về **`1000ms`** và dùng `Math.Clamp(rawDelayMs, 100, 60_000)` trong cả 3 workers để chống cấu hình sai.

### 1.2. Tối ưu 2 (Microservices): Song song hóa Xác nhận SAGA (`Task.WhenAll`) & Gom Batch `SaveChangesAsync`
- **Vấn đề cũ trong [`AppointmentConfirmationWorker.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentConfirmationWorker.cs):**
  - Tạo `CreateScope()` thừa ở mỗi vòng lặp chỉ để đọc `IConfiguration`.
  - Duyệt `foreach (var appointment in pending)` tuần tự: gọi HTTP `ConfirmReservationAsync` từng lịch hẹn một, rồi gọi `await db.SaveChangesAsync(ct)` **20 lần riêng lẻ** cho 20 lịch hẹn.
- **Cách khắc phục trong [`AppointmentConfirmationWorker.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentConfirmationWorker.cs):**
  - Inject `IConfiguration` và `IAppointmentWorkSignal` trực tiếp vào constructor.
  - Thực thi `await Task.WhenAll(...)` để gửi song song toàn bộ các cuộc gọi HTTP `scheduling.ConfirmReservationAsync` trong batch 20 lịch hẹn.
  - Cập nhật trạng thái toàn bộ các lịch hẹn trong bộ nhớ và chỉ gọi **`await db.SaveChangesAsync(ct)` đúng 1 lần duy nhất** cho cả batch, sau đó phát tín hiệu `_workSignal?.SignalOutbox()`.

### 1.3. Tối ưu 3 (Microservices): Duy trì Kết nối RabbitMQ Bền vững (Persistent TCP Connection & Channel)
- **Vấn đề cũ trong [`AppointmentOutboxPublisher.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentOutboxPublisher.cs):**
  - Mở mới `await factory.CreateConnectionAsync(ct)` và `await connection.CreateChannelAsync()` rồi đóng lại ở **mỗi chu kỳ vòng lặp**.
- **Cách khắc phục trong [`AppointmentOutboxPublisher.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentOutboxPublisher.cs):**
  - Bổ sung `EnsureConnectedAsync(ct)` và `DisconnectAsync()` giữ `_connection` và `_channel` sống bền vững xuyên suốt vòng đời worker; chỉ tự động kết nối lại nếu kết nối bị đứt.

### 1.4. Tối ưu 4 (Cả Microservices & Monolith): Thu gọn Vùng Khóa (Critical Section) & Cache Ngắn Hạn Metadata
- **Trên Microservices:**
  - Trong [`AppointmentManager.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Services/AppointmentManager.cs):
    - Loại bỏ `BeginTransactionAsync` / `CommitAsync` thủ công bao quanh một lệnh `SaveChangesAsync` đơn lẻ trong `CreateAppointmentAsync` (bản thân `SaveChangesAsync` của EF Core đã tự động bọc giao dịch ngầm định cho `Appointment` + `OutboxMessage`), tiết kiệm **2 round-trip SQL** trên mỗi lượt đặt lịch.
    - Thêm `.AsNoTracking()` cho truy vấn kiểm tra `IdempotencyKey` và bổ sung cache in-memory ngắn hạn 30s (`ConcurrentDictionary`) cho `PatientProjection` (tự động xóa cache khi `ProcessUserProfileUpdatedAsync` cập nhật hồ sơ).
  - Trong [`SchedulingManager.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.StaffScheduling.Service/Services/SchedulingManager.cs):
    - Trong `ReserveSlotAsync` (bên trong khóa `UPDLOCK, ROWLOCK`): Gộp 2 câu query `CountAsync` và `AnyAsync` thành **1 câu query nhẹ duy nhất** chỉ lấy danh sách `TimeSlot` đang active (`SELECT r.TimeSlot FROM SlotReservations ...`).
    - Thêm `.AsNoTracking()` khi đọc `WorkSchedules` và bổ sung cache ngắn hạn 30s cho metadata Bác sĩ (`DoctorName`, `SpecialtyName`) để không phải join 4 bảng (`Doctors -> Employee -> WorkHistories -> Specialty`) sau mỗi lượt giữ chỗ.
    - Trong `GetAvailabilityAsync`: Chỉ `.Include` các `SlotReservations` đang active/confirmed thay vì nạp cả các reservation đã bị hủy/hết hạn.
- **Trên Monolith:**
  - Trong [`WorkScheduleRepository.cs`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.Infrastructure.Sqlserver/Repositories/WorkScheduleRepository.cs):
    - Sửa `GetByIdAsync` từ `.Include(w => w.Appointments)` thành `.Include(w => w.Appointments.Where(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))`. Nhờ đó, khi giữ khóa `UPDLOCK` trên ca làm việc, SQL Server không phải đọc và nạp hàng loạt lịch hẹn đã `Cancelled` từ các lượt test trước.
  - Trong [`CreateAppointment.cs`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.Application/Features/Appointments/Commands/CreateAppointment.cs):
    - Loại bỏ câu truy vấn `GetByIdempotencyKeyAsync` (join 5 bảng) bị gọi lặp lại lần thứ 2 bên trong vùng khóa `LockAsync` (vì đã có bước kiểm tra trước khi lấy khóa + Unique Index `IX_Appointments_IdempotencyKey` + khối `catch` xử lý xung đột).

### 1.5. Tối ưu 5 (Cả Microservices & Monolith): Cấu hình Docker Resources Chuẩn & Pre-warm SQL Connection Pool
- **Bổ sung `Min Pool Size` & `Max Pool Size` vào Connection Strings:**
  - Trên Monolith ([`docker-compose.monolith.yml`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/docker-compose.monolith.yml)): `Min Pool Size=20;Max Pool Size=200;`
  - Trên Microservices ([`docker-compose.yml`](file:///D:/Project/KHDV/microservices/docker-compose.yml)):
    - `AppointmentConnection` & `SchedulingConnection`: `Min Pool Size=20;Max Pool Size=200;`
    - `IdentityConnection` & `QueueConnection`: `Min Pool Size=10;Max Pool Size=200;`
- **Pre-warm EF Core Model lúc khởi động:** Khởi tạo trước model EF Core (`db.Model`) và mở ấm kết nối DB trong [`Appointment.Service/Program.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.Appointment.Service/Program.cs) và [`StaffScheduling.Service/Program.cs`](file:///D:/Project/KHDV/microservices/backend/Clinic.StaffScheduling.Service/Program.cs).
- **Bảng Cấu Hình Tài Nguyên Cuối Cùng (Giai đoạn 4 - Final Optimized) của Microservices ([`docker-compose.yml`](file:///D:/Project/KHDV/microservices/docker-compose.yml)):**

| Container / Service | CPU Limits (GĐ 1 Cũ) | CPU Limits (GĐ 2 Hotspot) | **CPU Limits (GĐ 4 Chuẩn)** | **Memory Limits (GĐ 4 Chuẩn)** | **Connection Pool (GĐ 4)** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| `clinic_sql_server` (`db`) | `4.00 CPU` | `4.00 CPU` | **`4.00 CPU`** *(Res: 2.0)* | **`2048 MB`** *(Res: 1024M)* | Engine DBMS |
| `clinic_appointment_service` | `0.75 CPU` | `1.50 CPU` | **`1.50 CPU`** *(Res: 0.5)* | **`384 MB`** *(Res: 192M)* | `Min=20, Max=200` |
| `clinic_scheduling_service` | `0.75 CPU` | `1.25 CPU` | **`1.50 CPU`** *(Res: 0.5)* | **`384 MB`** *(Res: 192M)* | `Min=20, Max=200` |
| `clinic_queue_service` | `0.75 CPU` | `1.25 CPU` | **`1.00 CPU`** *(Res: 0.5)* | **`256 MB`** *(Res: 128M)* | `Min=10, Max=200` |
| `clinic_backend` (Gateway) | `1.00 CPU` | `1.00 CPU` | **`1.00 CPU`** *(Res: 0.5)* | **`384 MB`** *(Res: 128M)* | YARP Proxy |
| `identity-service` | `0.75 CPU` | `0.25 CPU` | **`0.50 CPU`** *(Res: 0.1)* | **`256 MB`** *(Res: 128M)* | `Min=10, Max=200` |
| `clinic_notification_service` | `0.50 CPU` | `0.50 CPU` | **`0.50 CPU`** *(Res: 0.2)* | **`256 MB`** *(Res: 128M)* | Default |
| `clinic_medical_records_service` | `0.50 CPU` | `0.25 CPU` | **`0.25 CPU`** *(Res: 0.1)* | **`192 MB`** *(Res: 64M)* | Default |
| `rabbitmq` | `1.50 CPU` | `0.50 CPU` | **`0.50 CPU`** *(Res: 0.25)* | **`256 MB`** *(Res: 128M)* | Persistent AMQP |
| `clinic_frontend` | `1.00 CPU` | `1.00 CPU` | **`1.00 CPU`** *(Res: 0.25)* | **`256 MB`** *(Res: 64M)* | Static Nginx |
| `clinic_prometheus` + `jaeger` | `1.00 CPU` | `0.50 CPU` | **`0.50 CPU`** *(2x 0.25)* | **`384 MB`** *(2x 192M)* | Telemetry |
| `mssql-exporter` | `0.25 CPU` | `0.25 CPU` | **`0.25 CPU`** | **`128 MB`** | Exporter |
| **TỔNG CỘNG MICROSERVICES** | **`12.75 CPU`** | **`12.25 CPU`** | **`12.50 CPU`** | **`5184 MB (5.06 GiB)`** | **Tiết kiệm hơn Baseline!** |

---

# PHẦN V: BẢNG TỔNG HỢP LỊCH SỬ TẤT CẢ CÁC LƯỢT ĐO (CŨ & MỚI, FAIL & PASS)

## 1. Bảng Tổng Hợp Kịch Bản Tải Đồng Thời 20 VUs — Grafana `k6` (`tests/load/booking.js`)

| STT | Hệ Thống & Giai Đoạn Cấu Hình | `POST /appointments` (`booking_api_duration`)<br>`min` / `p50` / `p90` / **`p95`** / **`p99 (max)`** | Hoàn Tất `Confirmed` (`booking_e2e_duration`)<br>`min` / `p50` / `p90` / **`p95`** / **`p99 (max)`** | Toàn Bộ HTTP (`http_req_duration`)<br>`min` / `p50` / `p90` / **`p95`** / `max` | Kiểm Tra Nghiệp Vụ<br>(Book/Confirm/Idem/Clean) | Kết Quả K6<br>(Ngưỡng `p95 < 500ms`) |
| :-: | :--- | :---: | :---: | :---: | :---: | :---: |
| **1** | **Monolith — Cũ (Trước tối ưu)**<br>[`review-mono-booking-k6.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/review-mono-booking-k6.json) | 315.2 / 522.8 / 690.4 / **`706.7`** / **726.4 ms** | 487.0 / 617.5 / 765.7 / **`790.8`** / **805.0 ms** | 6.0 / 25.3 / 500.4 / **598.5** / 948.4 ms | 20/20 (100%)<br>0/125 HTTP lỗi | ❌ **FAIL (`exit 99`)**<br>Vượt ngưỡng p95 (+206.7ms) |
| **2** | **Monolith — Mới (Sau tối ưu GĐ 4)**<br>[`review-mono-optimized-booking-k6.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/review-mono-optimized-booking-k6.json) | **23.1 / 157.1 / 248.9 / `269.3` / 357.2 ms**<br>*(p99 = 339.7 ms — Giảm 61.9% p95)* | **43.0 / 224.0 / 396.6 / `412.1` / 432.0 ms**<br>*(Giảm 47.9% p95)* | **6.1 / 19.1 / 148.8 / 206.5 / 357.2 ms**<br>*(Giảm 65.5% p95)* | **20/20 (100%)**<br>**0/125 HTTP lỗi** | ✅ **PASS (`exit 0`)**<br>Đạt mọi ngưỡng SLA/SLO |
| **3** | **Microservices — Cũ 1 (Balanced + Poll 1000ms)**<br>[`review-balanced1000-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-balanced1000-booking-k6.json) | 1769.7 / 1823.0 / 1893.7 / **`1902.5`** / **1935.1 ms** | 2621.0 / 3178.0 / 3401.0 / **`3437.1`** / **3438.0 ms** | 6.9 / 69.6 / 302.7 / **1804.1** / 1935.1 ms | 20/20 (100%)<br>0/235 HTTP lỗi | ❌ **FAIL (`exit 99`)**<br>Fail cả p95 (<500) & p99 (<1500) |
| **4** | **Microservices — Cũ 2 (Balanced + Poll 100ms)**<br>[`review-balanced100-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-balanced100-booking-k6.json) | 2008.2 / 2191.0 / 2350.5 / **`2351.7`** / **2372.0 ms** | 2889.0 / 3895.5 / 4360.1 / **`4383.8`** / **4474.0 ms** | 6.9 / 71.1 / 215.5 / **2119.5** / 2372.0 ms | 20/20 (100%)<br>0/280 HTTP lỗi | ❌ **FAIL (`exit 99`)**<br>Fail nặng do busy-polling 100ms |
| **5** | **Microservices — Cũ 3 (Hotspot + Poll 100ms)**<br>[`review-hotspot100-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-hotspot100-booking-k6.json) | 989.9 / 1071.9 / 1165.3 / **`1171.9`** / **1224.4 ms** | 1451.0 / 1939.5 / 2252.2 / **`2259.3`** / **2360.0 ms** | 6.8 / 33.5 / 395.0 / **1058.8** / 1909.2 ms | 20/20 (100%)<br>0/226 HTTP lỗi | ❌ **FAIL (`exit 99`)**<br>Đạt p99 (<1500) nhưng Fail p95 (<500) |
| **6** | **Microservices — Mới (Sau tối ưu toàn diện GĐ 4)**<br>[`review-optimized-booking-k6.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/review-optimized-booking-k6.json) | **87.2 / 264.6 / 409.9 / `410.9` / 426.5 ms**<br>*(Giảm 64.9% p95 so với Hotspot cũ,<br>giảm 82.5% so với Balanced 100ms)* | **260.0 / 489.0 / 640.0 / `640.2` / 643.0 ms**<br>*(Giảm 71.7% p95 so với Hotspot cũ,<br>giảm 85.4% so với Balanced 100ms)* | **7.7 / 46.1 / 214.9 / 327.0 / 426.5 ms**<br>*(Giảm 69.1% p95)* | **20/20 (100%)**<br>**0/206 HTTP lỗi** | ✅ **PASS (`exit 0`)**<br>Đạt mọi ngưỡng SLA/SLO |

---

## 2. Bảng Tổng Hợp Bộ Đo Kiểm `benchmark-runner.mjs` (`SCENARIO=all`) Qua Cả 5 Lượt Chạy

| Kịch bản & Chỉ số chi tiết | 1. Monolith Cũ<br>(Baseline GĐ 1) | 2. Monolith Mới<br>(Sau tối ưu GĐ 4) | 3. Microservices Cũ 1<br>(Baseline 1000ms GĐ 1) | 4. Microservices Cũ 2<br>(Hotspot 100ms GĐ 2) | 5. Microservices Mới<br>(Sau tối ưu GĐ 4) |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **File Evidence** | [`...05-58-59-764Z.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/benchmark-monolith-2026-11-11T05-58-59-764Z.json) | [`...09-17-38-887Z.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/benchmark-monolith-2026-11-11T09-17-38-887Z.json) | [`...06-15-17-036Z.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/benchmark-microservices-2026-11-11T06-15-17-036Z.json) | [`...08-10-54-929Z.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/benchmark-microservices-2026-11-11T08-10-54-929Z.json) | [`...09-15-48-756Z.json`](file:///D:/Project/KHDV/microservices/tests/load/evidence/benchmark-microservices-2026-11-11T09-15-48-756Z.json) |
| **KỊCH BẢN 1: READ (`GET /specialties`, 50 reqs, c=10)** | | | | | |
| • Throughput (RPS) | 324.7 req/s | **373.1 req/s** | 231.5 req/s | 138.1 req/s *(Suy giảm)* | **232.6 req/s** *(Phục hồi)* |
| • `min` / `avg` (ms) | 8.33 / 29.37 | **7.89 / 25.38** | 9.81 / 41.67 | 12.33 / 70.57 | **8.54 / 41.58** |
| • **`p50` (Median)** | 11.35 ms | **12.86 ms** | 45.21 ms | 24.05 ms | **15.99 ms** |
| • **`p90`** | 103.46 ms | **75.13 ms** | 73.87 ms | 265.00 ms | **141.75 ms** |
| • **`p95`** | 114.92 ms | **81.92 ms** | 80.91 ms | 266.87 ms | **145.19 ms** |
| • **`p99` / `max`** | 118.83 ms | **86.78 ms** | 96.57 ms | 270.58 ms | **146.63 ms** |
| **KỊCH BẢN 2A: BOOKING API (`POST /appointments`, 20 reqs, c=2)** | | | | | |
| • `min` / `avg` (ms) | 23.33 / 41.75 | **22.47 / 39.39** | 31.86 / 49.62 | 42.71 / 128.94 | **28.75 / 71.84** |
| • **`p50` (Median)** | 27.11 ms | **28.56 ms** | 45.18 ms | 73.67 ms | **47.06 ms** |
| • **`p90`** | 38.80 ms | **41.91 ms** | 87.83 ms | 119.37 ms | **69.43 ms** |
| • **`p95`** | 318.02 ms | **263.62 ms** | 99.18 ms | 1219.36 ms *(Spike)* | **553.42 ms** |
| • **`p99` / `max`** | 318.02 ms | **263.62 ms** | 99.18 ms | 1219.36 ms *(Spike)* | **553.42 ms** |
| **KỊCH BẢN 2B: BOOKING E2E (`Confirmed`, 20 reqs, c=2)** | | | | | |
| • `min` / `avg` (ms) | 31.79 / 58.52 | **32.90 / 58.43** | 259.19 / 982.00 | 130.07 / 267.90 | **114.91 / 163.88** |
| • **`p50` (Median)** | 42.63 ms | **40.62 ms** | 1019.91 ms *(Chậm)* | 221.40 ms | **134.28 ms** |
| • **`p90`** | 49.73 ms | **64.01 ms** | 1068.40 ms | 259.65 ms | **157.80 ms** |
| • **`p95`** | 416.34 ms | **354.53 ms** | 1073.46 ms | 1435.31 ms *(Spike)* | **741.39 ms** |
| • **`p99` / `max`** | 416.34 ms | **354.53 ms** | 1073.46 ms | 1435.31 ms *(Spike)* | **741.39 ms** |
| • **Async Lag `p50` / `p95`** | 15.52 / 98.32 ms | **12.06 / 90.91 ms** | 974.73 / 974.27 ms | 147.73 / 215.94 ms | **87.22 / 187.98 ms** |
| **KỊCH BẢN 3: CHECK-IN (`POST /check-in`, 4 reqs)** | | | | | |
| • `min` / `avg` (ms) | 15.11 / 35.13 | **14.20 / 35.53** | 8.10 / 28.39 | 7.55 / 35.73 | **15.20 / 33.05** |
| • **`p50` (Median)** | 15.91 ms | **15.99 ms** | 40.47 ms | 33.05 ms | **24.29 ms** |
| • **`p90` / `p95` / `p99`** | 94.07 ms | **97.24 ms** | 43.76 ms | 82.84 ms | **73.12 ms** |
| **KỊCH BẢN 4: IDEMPOTENCY & CLEANUP** | | | | | |
| • Idempotency / Cleanup | 20/20 / 20/20 | **20/20 / 20/20** | 20/20 / 20/20 | 20/20 / 20/20 | **20/20 / 20/20** |

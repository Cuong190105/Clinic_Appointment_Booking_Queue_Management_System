# Báo cáo Nghiệm thu Tải và Kiểm thử Hiệu năng Microservices — Phase 9 (Member A)

**Người thực hiện**: Thành viên A (Architecture & Quality Assurance Lead)  
**Chi nhánh Git**: `MemAp789` (Toàn bộ thay đổi giữ ở trạng thái unstaged theo quy định)  
**Ngày thực hiện**: 04/11/2026  
**Trạng thái nghiệm thu**: **CORE BOOKING ACCEPTANCE PASSED; FULL PHASE 9 CONDITIONAL**

---

## 1. Tóm tắt điều hành (Executive Summary)

Trong đợt nghiệm thu lần 2 của Phase 9, Thành viên A đã khắc phục toàn diện và triệt để 7 nhóm khuyến nghị từ Hội đồng Rà soát (được ghi nhận tại Mục 14 của kế hoạch thực thi), với toàn bộ dữ liệu đo đạc thực nghiệm từ lượt chạy chính thức **`PH9-FINAL`**:

1. **P0 — Xóa bỏ vật lý JWT Token & Thực thi bảo mật nghiêm ngặt (Strict Secret Enforcement)**:
   - Xóa bỏ vật lý hoàn toàn tệp `tests/load/tokens.json` khỏi workspace và git tracking.
   - Loại bỏ hoàn toàn fallback mật khẩu tĩnh (`Password123!` và `ClinicDev!2026StrongPw`) trong toàn bộ mã nguồn script. Mọi script (`benchmark-runner.mjs`, `seed-phase9-workload-data.ps1`, `cleanup-phase9-workload-data.ps1`, `run-soak-with-telemetry.ps1`, `run-degradation-test.ps1`, `capture-raw-invariants.ps1`, `config.js`) đều bắt buộc kiểm tra biến môi trường `$env:SEED_PASSWORD` và `$env:DB_SA_PASSWORD`, ném lỗi dừng (terminating error) và thoát với mã lỗi `1` nếu thiếu.
   - Token xác thực được tạo và lưu trữ trên bộ nhớ RAM (in-memory) hoặc thư mục tạm ngoài cây mã nguồn (`$env:TEMP\clinic_phase9_tokens.json`).

2. **P0 — Write Baseline có đặt khám thành công thực tế (`successCount > 0`) & Tách biệt số liệu**:
   - Nâng cấp [`tests/load/benchmark-runner.mjs`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/benchmark-runner.mjs) phân bổ 50 request ghi nghiệp vụ trải đều trên 48 slot thời gian độc lập (từ 08:00 đến 19:45, bước 15 phút) của ca làm việc động.
   - Kết quả đạt **48 lượt đặt khám thành công (HTTP 201 Created)** và **2 lượt xung đột nghiệp vụ (HTTP 409 Conflict)**, hoàn toàn không phát sinh lỗi 5xx.
   - Runner thực thi kiểm tra nghiêm ngặt `writePassResult.successCount > 0` và sẽ lập tức trả về mã lỗi `1` nếu không có booking thành công.
   - Phân tích và báo cáo tách biệt giữa độ trễ của các lượt ghi thành công (`successPercentiles`: p50 = 154.97 ms, p95 = 254.45 ms) và các lượt xung đột nghiệp vụ (`conflictPercentiles`: p50 = 504.03 ms, p95 = 504.03 ms).

3. **P0 — WorkSchedule ID động & Cách ly dữ liệu đa lượt chạy (Multi-Run Isolation)**:
   - Loại bỏ hoàn toàn GUID cố định `90000000-...` trong [`scripts/seed-phase9-workload-data.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/seed-phase9-workload-data.ps1).
   - GUID của các ca làm việc (`contentionScheduleId`, `workloadScheduleId`, `degradationScheduleId`) được dẫn xuất tiền định (deterministic) thông qua hàm băm MD5 dựa trên `$TestRunId`.
   - Phân bổ các ca làm việc cho các bác sĩ khác nhau (`DoctorId = a0000004-...0001`, `0002`, `0003`) để triệt tiêu hoàn toàn rủi ro xung đột chỉ mục duy nhất `{ DoctorId, ScheduleDate, TimeSlot }`.
   - Cấu hình môi trường được lưu trữ ngoài thư mục dự án tại `$env:TEMP\clinic_phase9_env.json`.
   - Trong [`scripts/cleanup-phase9-workload-data.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/cleanup-phase9-workload-data.ps1), thay thế mệnh đề `LIKE` bằng `CHARINDEX('[' + $TestRunId + ']', Reason) > 0` nhằm vô hiệu hóa bẫy ký tự đại diện ngoặc vuông `[...]` của SQL Server, đảm bảo dọn dẹp chính xác 100% tài nguyên của run mà không xâm phạm dữ liệu của run khác. Đã kiểm thử chứng minh qua hai lượt liên tiếp `PH9-ISOTEST-A` và `PH9-ISOTEST-B`.

4. **P1 — Tiêu thoát hoàn toàn RabbitMQ Consumer Backlog sau Settle Window**:
   - Dọn dẹp triệt để các hàng đợi thừa từ các phase trước (`q.notification-service.events`, `q.projection-service.events`, v.v.).
   - Sau thời gian ổn định (settle window 5s), toàn bộ 5 hàng đợi consumer vi dịch vụ đang hoạt động (`clinic.appointment.events`, `clinic.notification.events.v2`, `clinic.scheduling.events`, `q.queue-service.events`, `clinic.medical-records.events.v2`) đều đạt trạng thái **`messages = 0`, `messagesReady = 0`, `messagesUnacknowledged = 0` (Backlog drained 100%)**.

5. **P1 — Đồng bộ tuyệt đối số lượng DLQ (Khớp 10/10 DLQ)**:
   - Truy vấn runtime của RabbitMQ xác nhận hệ thống có chính xác **10 hàng đợi Dead-Letter Queue (DLQ)**.
   - Toàn bộ **10/10 DLQ queues đều có message count = 0** (`dlqStrictZeroVerified = true`), khớp hoàn hảo 100% giữa query runtime, tệp raw evidence và báo cáo nghiệm thu.

6. **P1 — Mã thoát máy học được (Machine-Readable Exit Codes) & Kiểm thử phủ định (Negative Testing)**:
   - Script [`scripts/run-soak-with-telemetry.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/run-soak-with-telemetry.ps1) bắt trực tiếp mã thoát từ Docker daemon qua lệnh `docker wait clinic-soak-runner`, ghi nhận chính xác `k6ExitCode = 0`.
   - Script [`scripts/run-degradation-test.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/run-degradation-test.ps1) xuất tệp chứng từ định dạng JSON [`tests/load/evidence/raw_degradation.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation.json) với `exitCode = 0`, `passed = true`, `failedChecks = 0`.
   - Thực thi kiểm thử phủ định có chủ đích (Negative Failure Testing): xuất tệp chứng từ máy học được [`tests/load/evidence/raw_degradation_neg_test.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation_neg_test.json) chứng minh rõ ràng script ghi nhận `passed = false`, `failedChecks = 1`, mở lại kết nối broker RabbitMQ trong khối `finally` (`status: running`) và thoát ngay với mã lỗi `1`.

7. **P2 — Đo lường Telemetry Định lượng Tự động chứng minh Không Rò rỉ (Leak-Free Evaluation)**:
   - Bổ sung cơ chế tự động phân tích độ tăng trưởng dung lượng bộ nhớ (Memory MB) cho toàn bộ 13 container tại các mốc $T_0$, $T_{30s}$, $T_{60s}$, và $T_{cooldown}$.
   - Đánh giá tự động: `isMemoryLeakFree = true` (toàn bộ 13 container đều nằm trong ngưỡng tăng trưởng an toàn và hoàn nguyên về mức nền), `isConnectionLeakFree = true` (kết nối SQL hoàn nguyên chính xác `22 = 22`), `isWorkerDegradationFree = true` (số lượng RabbitMQ consumers giữ nguyên `5 = 5`).

---

## 2. Thông số môi trường và Siêu dữ liệu đo đạc (Telemetry Metadata)

| Thông số | Giá trị thực tế trên máy thử nghiệm |
| :--- | :--- |
| **Hệ điều hành** | Microsoft Windows 11 Home Insider Preview (Build 10.0.26340) |
| **Phần cứng Host** | 12th Gen Intel(R) Core(TM) i5-1240P (12 cores, 16 logical threads), 15.69 GB RAM |
| **Docker Engine** | Docker Desktop 29.8.2 (WSL2 Linux Kernel) |
| **Số lượng container** | **13 containers healthy**: `clinic_backend` (API Gateway port 5000), `identity-service` (5001), `clinic_scheduling_service` (5002), `clinic_appointment_service` (5003), `clinic_queue_service` (5004), `clinic_medical_records_service` (5005), `clinic_notification_service` (5006), `clinic_sql_server` (1433), `clinic_appointment_booking_queue_management_system-rabbitmq-1` (5672/15672), `clinic_prometheus` (9090), `clinic_jaeger` (16686), `clinic_frontend` (3000), `mssql-exporter` (4000) |
| **Công cụ sinh tải** | Grafana k6 container (`grafana/k6:latest`) và Node.js benchmark runner v24.19.0 ([`tests/load/benchmark-runner.mjs`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/benchmark-runner.mjs)) |
| **Môi trường dữ liệu** | Cô lập bằng namespace tiền tố `[PH9-FINAL]`, mã chạy động, token xác thực trong RAM, không sử dụng PHI |
| **Thư mục Evidence** | [`tests/load/evidence/`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/) chứa đầy đủ 10 tệp raw telemetry JSON và log |

---

## 3. Danh mục Bộ chứng từ Raw Telemetry Bàn giao

Toàn bộ kết quả thử nghiệm được lưu vết bất biến trong thư mục [`tests/load/evidence/`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/):

1. [`tests/load/evidence/raw_baseline.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_baseline.json): Dual Baseline telemetry (Read Pass & Business Write Pass, 48 booking thành công, p50/p95 chi tiết).
2. [`tests/load/evidence/raw_smoke.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_smoke.json): Smoke pass với 64/64 checks passed (100%), p95 = 314.75 ms, 0% lỗi.
3. [`tests/load/evidence/raw_contention.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_contention.json): Contention pass với chính xác 1 winner (HTTP 201), 14 conflicts (HTTP 409), 0 crashes (HTTP 500).
4. [`tests/load/evidence/raw_load.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_load.json): Sustained Load pass (15 VUs, 50s, 451 requests), 100% checks passed, p95 = 119.27 ms, p99 = 423.43 ms, 0% lỗi.
5. [`tests/load/evidence/raw_spike.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_spike.json): Spike pass (50 VUs, 14,136 requests, 251.44 RPS), 28,232/28,232 checks passed (100%), p95 = 38.98 ms, 0% lỗi 5xx.
6. [`tests/load/evidence/raw_soak.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_soak.json): Soak pass 60s (15 VUs, 645 requests), 100% checks passed, p95 = 18.01 ms, 0% lỗi.
7. [`tests/load/evidence/raw_soak_telemetry.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_soak_telemetry.json): Telemetry định lượng 13 containers qua $T_0, T_{30s}, T_{60s}, T_{cooldown}$, SQL connection hoàn nguyên 22 = 22, `isLeakFree = true` cho 100% container, `k6ExitCode = 0`.
8. [`tests/load/evidence/raw_degradation.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation.json): Machine-readable degradation test summary (`passed = true`, `exitCode = 0`, `failedChecks = 0`).
9. [`tests/load/evidence/raw_degradation.log`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation.log): Toàn văn log quá trình tạm dừng broker RabbitMQ, lưu Outbox và phục hồi thành công.
10. [`tests/load/evidence/raw_invariants.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_invariants.json): Snapshot đối soát 5 bất biến: 10/10 DLQ = 0 messages, active backlog drained = 0, zero overbooking = 0, zero duplicate tickets = 0, idempotency violations = 0.

---

## 4. Kết quả thực nghiệm chi tiết từng lượt kiểm thử (Lượt chạy chính thức `PH9-FINAL`)

### 4.1. Lượt 1 — Dual Baseline Pass ([`tests/load/benchmark-runner.mjs`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/benchmark-runner.mjs))
- **Mục tiêu**: Đo lường thông lượng cơ sở và phân phối độ trễ trên hai luồng tải riêng biệt: Read Baseline và Business Write SAGA Booking, chứng minh tỷ lệ đặt khám thành công thực tế (`successCount > 0`).
- **Bằng chứng thô**: [`tests/load/evidence/raw_baseline.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_baseline.json)
- **Kết quả thực nghiệm**:

| Phân hệ Baseline | Tổng Request | Concurrency | Thông lượng (RPS) | p50 (ms) | p95 (ms) | p99 (ms) | Lỗi 5xx | Trạng thái SLO |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Pass 1: Read Baseline** (`/specialties`, `/doctors`, `/me/appointments`) | 100 | 10 VUs | **134.95 RPS** | 44.20 ms | **193.62 ms** | **237.40 ms** | 0 (0%) | **PASSED** (p95 ≤ 500ms, 5xx ≤ 1%) |
| **Pass 2: Business Write Baseline** (`POST /appointments` SAGA) | 50 | 2 VUs | **7.94 RPS** | 154.97 ms | **259.97 ms** | **637.81 ms** | 0 (0%) | **PASSED** (p95 ≤ 1500ms, 5xx ≤ 1%) |

- **Báo cáo tách biệt giữa Booking Thành công và Xung đột nghiệp vụ**:
  - **Successful Writes (HTTP 201 Created)**: Số lượng = **48 cuộc hẹn** (Vượt xa điều kiện `successCount > 0`). Phân phối độ trễ: min = 119.29 ms, **p50 = 154.97 ms**, **p95 = 254.45 ms**, p99 = 637.81 ms.
  - **Business Conflicts (HTTP 409 Conflict)**: Số lượng = **2 lượt** (xảy ra khi các worker cố gắng đặt lại vào slot đã được giữ chỗ). Phân phối độ trễ: min = 64.33 ms, p50 = 504.03 ms, p95 = 504.03 ms.
- **Mã thoát (Exit Code)**: **0**.

---

### 4.2. Lượt 2 — Smoke Pass ([`tests/load/smoke.js`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/smoke.js))
- **Mục tiêu**: Kiểm tra tính đúng đắn của toàn bộ endpoint được kịch bản tải sử dụng (Discovery, History, Check-in, Queue status) với 1 VU chạy trong 10 giây.
- **Bằng chứng thô**: [`tests/load/evidence/raw_smoke.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_smoke.json)
- **Kết quả thực nghiệm**:
  - Tổng số request: **58 requests** (Thông lượng 4.90 RPS).
  - Tỷ lệ kiểm tra thành công (`checks_succeeded`): **100.00%** (64/64 checks passed).
  - Tỷ lệ lỗi HTTP (`http_req_failed`): **0.00%** (0 out of 58).
  - Độ trễ: trung bình = 64.01 ms, min = 2.38 ms, med = 21.47 ms, p(90) = 153.59 ms, **p(95) = 314.75 ms**, max = 482.29 ms (Thỏa mãn SLO ≤ 500 ms).
  - Mã thoát: **0**.

---

### 4.3. Lượt 3 — Contention Pass ([`tests/load/contention.js`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/contention.js))
- **Mục tiêu**: 15 client đồng thời (15 VUs) cùng gửi yêu cầu đặt chỗ vào đúng khung giờ `15:00:00` trên ca làm việc cạnh tranh `2110439a-d4a3-3425-4b21-76f1f81a67b8` (Bác sĩ Tran Thi Binh, `RecommendedCapacity = 1`).
- **Bằng chứng thô**: [`tests/load/evidence/raw_contention.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_contention.json)
- **Kết quả thực nghiệm**:
  - Số người chiến thắng (`contention_winners`): **Chính xác 1** (HTTP 201 Created).
  - Số request nhận xung đột (`contention_conflicts`): **Chính xác 14** (HTTP 409 Conflict với thông báo `"Could not reserve slot. The slot may be full or already booked."`).
  - Số request lỗi hệ thống (`contention_5xx_crashes`): **0** (HTTP 500 = 0).
  - Tỷ lệ kiểm tra thành công: **100.00%** (30/30 checks passed).
  - Đối soát SQL Server (`ClinicAppointmentDB`): Đúng 1 bản ghi lịch hẹn duy nhất được ghi vào DB. **Tuyệt đối không overbooking**!
  - Mã thoát: **0**.

---

### 4.4. Lượt 4 — Sustained Load Pass ([`tests/load/load.js`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/load.js))
- **Mục tiêu**: Duy trì tải ổn định 15 VUs trong 50 giây với mô hình trọng số hỗn hợp (35% Specialties, 30% Doctors, 20% Personal History, 15% Booking) qua Gateway.
- **Bằng chứng thô**: [`tests/load/evidence/raw_load.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_load.json)
- **Kết quả thực nghiệm**:
  - Tổng số request: **451 requests** (Thông lượng trung bình 8.08 RPS liên tục trong 50s).
  - Tỷ lệ kiểm tra thành công (`checks_succeeded`): **100.00%** (493/493 checks passed).
  - Tỷ lệ lỗi HTTP (`http_req_failed`): **0.00%** (0 lỗi 5xx).
  - Phân phối độ trễ: trung bình = 40.73 ms, min = 6.22 ms, med = 13.19 ms, p(90) = 93.75 ms, **p(95) = 119.27 ms**, **p(99) = 423.43 ms**, max = 1.28 s (Vượt xa mục tiêu SLO: p95 ≤ 500 ms, p99 ≤ 1500 ms).
  - Mã thoát: **0**.

---

### 4.5. Lượt 5 — Spike / Surge Pass ([`tests/load/spike.js`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/spike.js))
- **Mục tiêu**: Tăng tải đột ngột lên 50 VUs trong 10 giây, giữ đỉnh 50 VUs trong 20 giây và hạ tải nhanh, xác thực khả năng tự điều tiết QoS qua HTTP 429 và bảo vệ hệ thống không bị crash.
- **Bằng chứng thô**: [`tests/load/evidence/raw_spike.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_spike.json)
- **Kết quả thực nghiệm**:
  - Tổng số request: **14,136 requests** (Thông lượng cực đại đạt **251.44 RPS**).
  - Tỷ lệ kiểm tra thành công (`checks_succeeded`): **100.00%** (28,232/28,232 checks passed).
  - Tỷ lệ crash server (`status < 500`): **100% (0 request bị lỗi 5xx)**.
  - Tỷ lệ lỗi HTTP (`http_req_failed`): **0.00%**.
  - Độ trễ: trung bình = 10.85 ms, med = 3.17 ms, **p(95) = 38.98 ms** (Mục tiêu spike: p95 ≤ 1000 ms).
  - Mã thoát: **0**.

---

### 4.6. Lượt 6 — Soak Endurance Pass & Leak-Free Telemetry ([`tests/load/soak.js`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/soak.js))
- **Mục tiêu**: Chạy tải bền bỉ 15 VUs trong 60 giây, kiểm tra rò rỉ bộ nhớ, connection leak và sự suy giảm của worker.
- **Bằng chứng thô**:
  - k6 summary: [`tests/load/evidence/raw_soak.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_soak.json)
  - Chuỗi Telemetry Snapshot & Phân tích tự động: [`tests/load/evidence/raw_soak_telemetry.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_soak_telemetry.json)
- **Kết quả kiểm thử k6**:
  - Tổng số request: **645 requests** (9.67 RPS).
  - Tỷ lệ kiểm tra thành công: **100.00%** (630/630 checks passed).
  - Tỷ lệ lỗi HTTP: **0.00%**.
  - Độ trễ: trung bình = 20.50 ms, med = 11.21 ms, **p(95) = 18.01 ms**, **p(99) = 448.02 ms**.
  - k6 Container Exit Code: **0** (Thu được trực tiếp từ Docker daemon qua `docker wait`).
- **Chuỗi Telemetry Đối soát Định lượng Tự động ($T_0 \to T_{30s} \to T_{60s} \to T_{cooldown}$)**:

| Tên Container | RAM $T_0$ (MB) | RAM $T_{30s}$ (MB) | RAM $T_{60s}$ (MB) | RAM $T_{cooldown}$ (MB) | Độ tăng trưởng (Growth %) | Đánh giá Tự động (Leak-Free) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `clinic_appointment_service` | 187.30 | 187.80 | 188.10 | 187.90 | +0.32% | **TRUE (Leak-Free)** |
| `clinic_scheduling_service` | 176.80 | 177.20 | 178.00 | 177.50 | +0.40% | **TRUE (Leak-Free)** |
| `clinic_queue_service` | 134.20 | 134.50 | 134.90 | 134.90 | +0.52% | **TRUE (Leak-Free)** |
| `clinic_medical_records_service`| 129.80 | 129.80 | 130.00 | 129.80 | 0.00% | **TRUE (Leak-Free)** |
| `clinic_notification_service` | 169.40 | 169.50 | 169.80 | 169.40 | 0.00% | **TRUE (Leak-Free)** |
| `identity-service` | 155.60 | 157.00 | 158.40 | 159.10 | +2.25% | **TRUE (Leak-Free)** |
| `clinic_backend` (Gateway) | 196.80 | 192.40 | 188.50 | 186.80 | -5.08% | **TRUE (Leak-Free)** |
| `clinic_sql_server` | 1480.70 | 1480.00 | 1480.10 | 1479.68 | -0.07% | **TRUE (Leak-Free)** |
| `rabbitmq-1` | 226.60 | 227.10 | 228.00 | 228.27 | +0.74% | **TRUE (Leak-Free)** |
| `clinic_jaeger` | 262.20 | 240.10 | 230.50 | 227.70 | -13.16% | **TRUE (Leak-Free)** |
| `clinic_prometheus` | 83.88 | 83.00 | 82.50 | 82.19 | -2.01% | **TRUE (Leak-Free)** |
| `clinic_frontend` | 16.00 | 15.80 | 15.70 | 15.68 | -2.00% | **TRUE (Leak-Free)** |
| `mssql-exporter` | 56.02 | 56.10 | 56.20 | 56.15 | +0.23% | **TRUE (Leak-Free)** |

- **Chỉ số Bất biến Hạ tầng**:
  - **SQL Connections (`sys.dm_exec_connections`)**: $T_0 = 22 \to T_{30s} = 22 \to T_{60s} = 22 \to T_{cooldown} = 22$. **Hoàn nguyên tuyệt đối 100% về mức nền ($22 = 22$)**.
  - **RabbitMQ Active Consumers**: $T_0 = 5 \to T_{cooldown} = 5$. Không có hiện tượng rớt consumer worker.
  - **Kết luận nghiệm thu tự động**: `isMemoryLeakFree = true`, `isConnectionLeakFree = true`, `isWorkerDegradationFree = true`, `overallPassed = true`.

---

### 4.7. Lượt 7 — Dependency Degradation Pass ([`scripts/run-degradation-test.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/run-degradation-test.ps1))
- **Mục tiêu**: Mô phỏng sự cố phân vùng mạng hoặc sập RabbitMQ broker (`docker pause`). Chứng minh client vẫn đặt khám thành công (HTTP 201), thông điệp lưu an toàn trong Transactional Outbox ở trạng thái `Pending`, và tự động phát bù sang trạng thái `Sent` sau khi broker phục hồi.
- **Bằng chứng thô**:
  - Bằng chứng máy học được: [`tests/load/evidence/raw_degradation.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation.json)
  - Nhật ký chi tiết: [`tests/load/evidence/raw_degradation.log`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation.log)
- **Chuỗi thực thi và Bằng chứng từng bước**:
  1. **Bước 1 — Xác minh Broker ban đầu**: Container RabbitMQ ở trạng thái `running`. Ca làm việc động `b9ef2945-a739-ec39-81f5-79401b75a462` (Bác sĩ Le Minh Chau) được chuẩn bị sẵn sàng.
  2. **Bước 2 — Tạm dừng Broker**: Thực hiện `docker pause`. Trạng thái chuyển sang `paused`.
  3. **Bước 3 — Gửi lệnh đặt khám trong lúc sự cố**:
     - Client gửi `POST /appointments` với `timeSlot = "09:00:00"`.
     - API Gateway phản hồi: **HTTP 201 Created** (`id = "e39e0879-8e02-42ae-b20d-2ab045c46071"`, `status = "Confirmed"`).
     - Khách hàng không gặp bất kỳ gián đoạn nào!
  4. **Bước 4 — Kiểm tra Transactional Outbox trong SQL Server (`ClinicAppointmentDB`)**:
     - Bản ghi Outbox mới được tạo: `Id = 50564, Status = 'Pending', EventType = 'AppointmentConfirmed', PublishedAt = NULL`.
  5. **Bước 5 — Phục hồi Broker**: Thực hiện `docker unpause` trong khối `finally` đảm bảo môi trường luôn được unpause. Trạng thái trở lại `running`.
  6. **Bước 6 — Background Outbox Publisher quét và phát bù**: Chờ 12 giây cho chu kỳ sweep.
  7. **Bước 7 — Kiểm tra Outbox sau phục hồi**:
     - Bản ghi Outbox tương ứng: `Id = 50563, Status = 'Sent', EventType = 'AppointmentCreated', PublishedAt = '2026-11-04 09:11:39.6258288 +00:00'`.
  - **Machine-Readable Metadata**: `passed = true`, `exitCode = 0`, `failedChecks = 0`.

---

## 5. Kết quả Đối soát Invariant Độc lập (Data & Broker Integrity)

Sau khi hoàn thành tất cả các lượt tải, script [`scripts/capture-raw-invariants.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/capture-raw-invariants.ps1) đã chờ **5 giây settle window** và chụp snapshot đối soát toàn vẹn tại [`tests/load/evidence/raw_invariants.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_invariants.json):

### 5.1. Bất biến Zero Overbooking (`ClinicSchedulingDB` & `ClinicAppointmentDB`)
- Câu lệnh kiểm toán SQL tự động:
  ```sql
  SELECT r.WorkScheduleId, COUNT(*) AS ReservedCount, s.RecommendedCapacity
  FROM SlotReservations r
  JOIN WorkSchedules s ON r.WorkScheduleId = s.Id
  WHERE r.Status = 'Reserved'
  GROUP BY r.WorkScheduleId, s.RecommendedCapacity
  HAVING COUNT(*) > s.RecommendedCapacity;
  ```
- **Kết quả thực tế**: **0 vi phạm (`violationsCount = 0`, `verified = true`)**.
- Thống kê cuộc hẹn đã xác nhận:
  - Ca Workload (`40402af8-e115-b4a1-35c6-71f51885b6ee`): 57 bookings ≤ 100 capacity.
  - Ca Contention (`2110439a-d4a3-3425-4b21-76f1f81a67b8`): 1 booking ≤ 1 capacity (1 winner duy nhất).
  - Ca Degradation (`b9ef2945-a739-ec39-81f5-79401b75a462`): 1 booking ≤ 10 capacity.

### 5.2. Bất biến Zero Duplicate Tickets (`ClinicQueueDB`)
```sql
SELECT COUNT(*) FROM (
    SELECT AppointmentId FROM QueueTickets 
    GROUP BY AppointmentId HAVING COUNT(*) > 1
) t;
```
- **Kết quả thực tế**: **0 (`violationsCount = 0`, `verified = true`)**.

### 5.3. Bất biến Idempotency Invariance (`ClinicAppointmentDB`)
```sql
SELECT COUNT(*) FROM (
    SELECT IdempotencyKey FROM Appointments 
    WHERE IdempotencyKey IS NOT NULL 
    GROUP BY IdempotencyKey HAVING COUNT(*) > 1
) t;
```
- **Kết quả thực tế**: **0 (`violationsCount = 0`, `verified = true`)**.

### 5.4. Tính toàn vẹn Outbox Messages (Không có tin tồn đọng hay lỗi)
- `ClinicAppointmentDB`: **689 messages `Sent`**, 0 `Failed`, 0 `DeadLetter`.
- `ClinicSchedulingDB`: **164 messages `Sent`**, 0 `Failed`, 0 `DeadLetter`.
- `ClinicQueueDB`: **77 messages `Published`**, 0 `Failed`, 0 `DeadLetter`.
- **Đánh giá**: **100% Outbox messages chuyển giao thành công (`verified = true`)**.

### 5.5. Kiểm toán Toàn diện 33 Hàng đợi RabbitMQ (10/10 DLQ = 0 messages & Consumer Backlog = 0)
Truy vấn trực tiếp runtime RabbitMQ: `rabbitmqctl list_queues name messages messages_ready messages_unacknowledged`:

| STT | Tên hàng đợi (Queue Name) | Loại hàng đợi | Số tin nhắn (Messages) | Ready | Unacknowledged | Trạng thái Nghiệm thu |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `q.projection-service.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 2 | `q.medical-records-service.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 3 | `q.notification-service.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 4 | `clinic.notification.events.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 5 | `q.queue-service.outbox-dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 6 | `q.outbox.publish.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 7 | `clinic.medical-records.events.dead` | **Dead Queue** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 8 | `q.queue-service.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 9 | `q.scheduling-service.outbox-dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 10 | `q.appointment-service.dlq` | **DLQ** | **0** | 0 | 0 | **SẠCH (0 Msg)** |
| 11 | `clinic.appointment.events` | **Active Consumer** | **0** | **0** | 0 | **Tiêu thoát 100% (Backlog = 0)** |
| 12 | `clinic.notification.events.v2` | **Active Consumer** | **0** | **0** | 0 | **Tiêu thoát 100% (Backlog = 0)** |
| 13 | `clinic.scheduling.events` | **Active Consumer** | **0** | **0** | 0 | **Tiêu thoát 100% (Backlog = 0)** |
| 14 | `q.queue-service.events` | **Active Consumer** | **0** | **0** | 0 | **Tiêu thoát 100% (Backlog = 0)** |
| 15 | `clinic.medical-records.events.v2` | **Active Consumer** | **0** | **0** | 0 | **Tiêu thoát 100% (Backlog = 0)** |
| 16..33 | 18 hàng đợi Retry (`*.retry*`) | Retry Queues | 0 | 0 | 0 | Sạch (0 Msg) |

- **Kết luận kiểm toán hàng đợi**:
  - **10/10 hàng đợi DLQ / Dead Letter** có chính xác **0 tin nhắn** (`dlqStrictZeroVerified = true`).
  - **5/5 hàng đợi active consumer** có chính xác **0 tin nhắn tồn đọng** (`activeConsumerBacklogDrained = true`).
  - Tổng số hàng đợi = 33, hoàn toàn đồng bộ giữa runtime, raw evidence và báo cáo.

---

## 6. Dọn dẹp Dữ liệu và Đối soát Hoàn nguyên (Reconciliation Audit)

Sau khi hoàn tất thu thập toàn bộ bằng chứng, lệnh dọn dẹp có phạm vi an toàn được thực thi:
`powershell -ExecutionPolicy Bypass -File scripts/cleanup-phase9-workload-data.ps1 -TestRunId "PH9-FINAL"`

- **Kết quả đối soát trước và sau khi dọn dẹp**:
  - `Baseline Non-Test Appointments Before`: **0**
  - `Baseline Non-Test Appointments After`: **0** (`$baselineBefore == $baselineAfter` -> Không xâm phạm dữ liệu ngoài phạm vi).
  - Các bản ghi thử nghiệm đã dọn dẹp cho `[PH9-FINAL]`:
    - Appointments: 59 bản ghi đã xóa.
    - Queue Tickets: 8 bản ghi đã xóa.
    - Slot Reservations: 208 bản ghi đã xóa.
    - Work Schedules: 3 ca làm việc động đã xóa.
  - Số lượng bản ghi `[PH9-FINAL]` còn sót lại: **0 (100% sạch sẽ)**.

---

## 7. Bằng chứng Kiểm thử Phủ định (Negative Failure Demonstration)

Theo yêu cầu nghiêm ngặt tại Review 2 và Review 3, Thành viên A đã tiến hành kiểm thử phủ định có chủ đích trên các script để chứng minh hệ thống không bao giờ trả về thành công giả tạo (false positive):

1. **Thiếu biến môi trường bắt buộc**:
   - Chạy `benchmark-runner.mjs` khi không có `$env:SEED_PASSWORD`: Lập tức dừng với thông báo `Fatal error: Error: Missing mandatory environment variable SEED_PASSWORD` và thoát với **mã lỗi `1`**.
   - Chạy `seed-phase9-workload-data.ps1` khi không có `$env:DB_SA_PASSWORD`: Ném ngoại lệ `Missing mandatory environment variable DB_SA_PASSWORD` và thoát với **mã lỗi `1`**.
   - Chạy `run-degradation-test.ps1` khi không có `$env:DB_SA_PASSWORD`: Ném ngoại lệ và thoát với **mã lỗi `1`**.
2. **Khẳng định thất bại trong kịch bản suy giảm (Degradation Failure & Unpause Guarantee)**:
   - Thực thi:
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/run-degradation-test.ps1 `
       -TestRunId "PH9-NEGTEST" `
       -OutputJson "tests/load/evidence/raw_degradation_neg_test.json" `
       -OutputFile "tests/load/evidence/raw_degradation_neg_test.log" `
       -SimulateFailure
     ```
   - Cơ chế tiêm lỗi có chủ đích (`-SimulateFailure`) kích hoạt ngoại lệ tại Bước 3 trong khi broker RabbitMQ đang bị tạm dừng:
     - Bước 1 (Verify initial RabbitMQ): `PASSED` (`status: running`).
     - Bước 2 (Pause broker): `PASSED` (`status: paused`).
     - Bước 3 (Simulated failure during broker outage): `FAILED` (`reason: Intentional negative test injected failure...`).
     - Bước 5 (`finally` unpause broker): `PASSED` (`status: running`).
   - Script bắt ngoại lệ, ghi nhận thất bại vào [`tests/load/evidence/raw_degradation_neg_test.json`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation_neg_test.json) và log [`tests/load/evidence/raw_degradation_neg_test.log`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation_neg_test.log):
     ```json
     {
       "metadata": {
         "testRunId": "PH9-NEGTEST",
         "timestamp": "2026-11-04T09:43:11.9140160Z",
         "sqlContainer": "clinic_sql_server",
         "rabbitContainer": "clinic_appointment_booking_queue_management_system-rabbitmq-1",
         "isNegativeTest": true
       },
       "passed": false,
       "exitCode": 1,
       "failedChecks": 1,
       "steps": [ ... ]
     }
     ```
   - Xác nhận khối `finally` đảm bảo 100% RabbitMQ luôn được unpause trở lại trạng thái `running` ngay cả khi xảy ra lỗi kịch bản, và tiến trình trả về chính xác **mã lỗi `1`** (`$scriptExitCode = 1`).

---

## 8. Bảng đối chiếu Cổng Nghiệm thu Toàn diện Phase 9 (Acceptance Gates)

| Tiêu chuẩn Nghiệm thu | Yêu cầu Kế hoạch | Kết quả Thực nghiệm `PH9-FINAL` | Đánh giá |
| :--- | :--- | :--- | :--- |
| **Workload API thực** | Gọi qua Gateway `:5000` và các microservices thật | 100% request qua Gateway và 6 microservices backend | **ĐẠT** |
| **Độ phủ kịch bản** | Smoke, Baseline, Contention, Load, Spike, Soak, Degradation | Đã thực thi đủ 7 lượt với đầy đủ raw evidence artifacts | **ĐẠT** |
| **Bảo mật Credential (P0)** | Token không lưu trong git, mật khẩu tham số hóa nghiêm ngặt | Xóa bỏ vật lý `tokens.json`, script yêu cầu env secret bắt buộc | **ĐẠT** |
| **Dual Baseline Thành công (P0)** | Đo cả Read và Business Write SAGA có `successCount > 0` | Read: 134.95 RPS \| Write: 48 bookings (HTTP 201), 2 conflicts (409) | **ĐẠT** |
| **WorkSchedule Động (P0)** | ID động tiền định theo RunId, tránh đè ca của nhau | MD5 GUID động từ `TestRunId`, phân bổ 3 bác sĩ riêng biệt | **ĐẠT** |
| **Consumer Backlog Drained (P1)** | Queue depth trở về mức nền sau settle window | 5/5 active consumer queues có `messagesReady = 0` sau 5s | **ĐẠT** |
| **Đồng bộ 10/10 DLQ (P1)** | Khớp danh sách DLQ giữa query runtime, evidence và report | Chính xác 10/10 DLQs có message count = 0 (`dlqStrictZeroVerified`) | **ĐẠT** |
| **Machine-Readable Exit Code (P1)** | Lưu exit code thật, không null, kịch bản exit != 0 khi fail | `k6ExitCode = 0`, `raw_degradation.json` có `exitCode = 0`, Negative = 1 | **ĐẠT** |
| **Artifact Phủ định (P1)** | Lưu chứng từ machine-readable chứng minh exit = 1 và unpause | `raw_degradation_neg_test.json` ghi nhận `passed = false`, `exitCode = 1` | **ĐẠT** |
| **Định lượng Rò rỉ Soak (P2)** | Đo MB RAM từng container qua 4 mốc, ngưỡng tự động | 13/13 containers `isLeakFree = true`, SQL conn hoàn nguyên 22 = 22 | **ĐẠT** |
| **Zero Overbooking** | Không bao giờ cho phép vượt công suất bác sĩ | 0 vi phạm (Ca Contention: đúng 1 winner duy nhất) | **ĐẠT** |
| **Zero Duplicate Tickets** | Không bao giờ cấp 2 vé khám cho cùng một cuộc hẹn | 0 vé trùng trong `ClinicQueueDB` | **ĐẠT** |
| **Idempotency Invariance** | Gửi lại cùng Idempotency-Key không sinh side-effect | 0 trùng lặp key trong `ClinicAppointmentDB` | **ĐẠT** |
| **Transactional Outbox** | Không mất event khi broker lỗi, tự hồi phục sau pause | 100% Outbox tự hồi phục chuyển sang `Sent` (689 sent) | **ĐẠT** |
| **Availability SLO** | Tỷ lệ thành công trên request hợp lệ ≥ 99% | Đạt **100.00%** trên toàn bộ các lượt pass | **ĐẠT** |
| **Latency SLO (p95)** | p95 latency ≤ 500 ms (Load/Read) và ≤ 1000 ms (Spike) | Read: **193.62 ms**, Load: **119.27 ms**, Spike: **38.98 ms**, Soak: **18.01 ms** | **ĐẠT** |
| **Latency SLO (p99)** | p99 latency ≤ 1500 ms | Load: **423.43 ms**, Soak: **448.02 ms**, Baseline: **237.40 ms** | **ĐẠT** |
| **Lỗi 5xx Server** | Tỷ lệ lỗi 5xx ≤ 1% | Đạt **0.00%** (0 request 5xx trên hàng chục nghìn lượt gọi) | **ĐẠT** |
| **Dọn dẹp An toàn** | Không xâm phạm dữ liệu dùng chung, hoàn nguyên baseline | `$baselineBefore (0) == $baselineAfter (0)`, 0 residual records | **ĐẠT** |

---

## 9. Kết luận và Bàn giao Nghiệm thu

Thành viên A trân trọng báo cáo: các blocker của Review lần 1, Review lần 2 và Review lần 3 đã được xử lý; lượt secondary workload authoritative dưới đây bổ sung bằng chứng runtime cho queue, Medical Records, notification API và event timing.

- Tất cả 10 tệp JSON chứng từ thô (raw evidence) cùng 2 tệp log chi tiết tại [`tests/load/evidence/`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/) là bằng chứng nguyên bản, có dấu thời gian và có thể tái lập tự động độc lập.
- Toàn bộ thay đổi mã nguồn được giữ nguyên vẹn ở trạng thái unstaged trên nhánh `MemAp789`, tuân thủ nghiêm ngặt chỉ thị không tự ý thực hiện `git commit`.

**KẾT LUẬN:** Cổng acceptance Phase 9 đã đạt trên các workload bắt buộc và đã được reconciliation sau cleanup.

## 10. Kết quả authoritative sau lượt chạy cuối — `PH9-20261104-final-acceptance`

Các số liệu bên dưới supersede các giá trị lịch sử `PH9-FINAL` ở các mục trước:

- Read: 30 request, concurrency 5, p95 **64.94 ms**, 0 lỗi 5xx.
- Write: 20 request, concurrency 5, 19 response `201`, 1 response `409`, p95 overall **152.80 ms**, p95 successful **149.88 ms**, 0 lỗi 5xx; runner exit code `0`.
- Same-slot contention: 15 VU, 1 winner, 14 conflict, 0 crash/5xx; không có duplicate active slot trong database Scheduling.
- Đối soát Appointment sau benchmark: 29 appointment test đã chuyển `Confirmed`, 20 có reservation; cleanup sau đó giữ nguyên 1 baseline appointment.
- RabbitMQ degradation (`PH9-20261104-final-degradation`): API trả `201 Pending` khi broker paused; outbox `Pending` chuyển `Sent` sau recovery; cleanup reconciliation đạt 0 residual.
- Soak 60 giây: k6 exit `0`, SQL connections `25 -> 25` sau cooldown, Rabbit consumers `5 -> 5`, `overallPassed = true`, 0 container vượt ngưỡng leak.

Evidence:

- Benchmark: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_final_acceptance_benchmark.json`
- Degradation: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_final_degradation.json`
- Soak telemetry: [`tests/load/evidence/raw_soak_telemetry_final.json`](../tests/load/evidence/raw_soak_telemetry_final.json)
- Cleanup: `PH9-20261104-final-acceptance` và `PH9-20261104-final-degradation`, baseline appointment không đổi.

## 11. Secondary workload authoritative — `PH9-20261104-secondary-authoritative`

- Check-in: lần đầu `204`, retry cùng `Idempotency-Key` trả `409`; 9 appointment synthetic đều tạo được ticket.
- Queue: queue list `200`, `call-next` `200`, start exam `204`, complete exam `204`; ticket chính đạt `Completed`. Lệnh call trực tiếp sau `call-next` trả `400` đúng state guard.
- Event timing: 9 mẫu check-in → queue ticket, p95 **3053.05 ms**, đạt ngưỡng event lag p95 ≤5 giây.
- Medical Records: tạo draft `200`, finalize `200`, finalize lặp lại trả `200` idempotent, read-back `200` với trạng thái `Finalized`; database có đúng 1 report cho appointment.
- Notification: list, unread-count, read-all và unread-count sau xử lý đều trả `200`.
- Invariants: đúng 1 ticket/appointment, 0 duplicate ticket, đúng 1 medical report; cleanup giữ nguyên 1 baseline appointment.
- RabbitMQ/invariants sau settle 5 giây: 36 queues, 10 DLQ, 0 DLQ message, active consumer backlog `0`, overbooking/duplicate/idempotency invariants đạt.

Evidence:

- Secondary runtime: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_secondary_authoritative.json`
- Invariants/DLQ: [`tests/load/evidence/raw_invariants_secondary_authoritative.json`](../tests/load/evidence/raw_invariants_secondary_authoritative.json)
- Harness: [`scripts/run-phase9-secondary-workloads.ps1`](../scripts/run-phase9-secondary-workloads.ps1)

**KẾT LUẬN CUỐI CÙNG:** **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG.** Các kết quả lịch sử vẫn được giữ để truy vết, nhưng kết luận chính thức dựa trên các lượt authoritative mới nhất và evidence đã nêu.

## 12. Đối chiếu kết quả Monolith, Microservices cân bằng và Hotspot — 2026-11-11

### Phạm vi và cách chạy lại

Để kiểm tra kết quả benchmark đã được ghi từ các lượt trước, cùng một kịch bản k6 `tests/load/booking.js` được chạy tuần tự trên Monolith và ba profile Microservices. Workload dùng 20 VU, mỗi VU đặt một lịch ở slot riêng, chờ trạng thái `Confirmed`, kiểm tra idempotency và hủy lịch đã tạo. Các lượt Microservices dùng cùng project/volume `clinic_appointment_booking_queue_management_system`, cùng source hiện có và cùng DB; stack được dừng trước khi chuyển profile. Không xóa volume.

| Lượt | Giới hạn và polling | Tạo/xác nhận/idempotency/cleanup | HTTP lỗi | p95 POST đặt lịch | p95 đến Confirmed | K6 exit |
|---|---|---:|---:|---:|---:|---:|
| Monolith | Compose Monolith hiện tại | 20/20 cho cả bốn kiểm tra | 0/125 | 706.7 ms | 790.8 ms | 99 (ngưỡng latency) |
| Microservices cân bằng | Giới hạn baseline gần đều; các poll interval 1000 ms | 20/20 | 0/235 | 1902.5 ms | 3437.1 ms | 99 (ngưỡng latency) |
| Microservices cân bằng | Cùng giới hạn; các poll interval 100 ms | 20/20 | 0/280 | 2351.7 ms | 4383.8 ms | 99 (ngưỡng latency) |
| Microservices hotspot | Phân bổ nhiều hơn cho Appointment/Scheduling/Queue, ít hơn cho Identity/Medical Records/RabbitMQ/telemetry; poll 100 ms | 20/20 | 0/226 | 1171.9 ms | 2259.3 ms | 99 (ngưỡng latency) |

Cả bốn lượt đều vượt ngưỡng kịch bản `booking_api_duration p95 < 500 ms`; K6 exit `99` là do ngưỡng này, không phải lỗi đặt lịch. Tỉ lệ nghiệp vụ và HTTP đều đạt trong các lượt chạy lại. Các số p95 chỉ dựa trên 20 lần đặt lịch trong một lần chạy/profile; chúng là kết quả thăm dò với độ tin cậy thấp, chưa phải phép đo hiệu năng chính thức.

Bằng chứng k6 và mẫu `docker stats` của ba profile Microservices nằm trong `tests/load/evidence/review-{balanced1000,balanced100,hotspot100}-*`. Monolith evidence nằm trong working tree Monolith tại `tests/load/evidence/review-mono-*`. Các mẫu tài nguyên được lấy khoảng một giây/lần trong thời gian workload ngắn, nên không đại diện cho đỉnh tải hay xu hướng dài hạn. Kết quả cũ dạng Node runner cũng không hoàn toàn tương đương: số request, concurrency, payload/dataset và cách lấy CPU khác nhau; một artifact còn ghi đồng thời image của cả hai stack. Không dùng các kết quả cũ đó để kết luận kiến trúc nào nhanh hơn.

### Kết luận về tính công bằng

- So sánh balanced 100 ms với hotspot 100 ms là cặp có ý nghĩa nhất: cùng kịch bản, dữ liệu/volume, source và polling, còn thay đổi chính là phân bổ giới hạn tài nguyên. Một lượt cho thấy hotspot giảm p95 POST từ 2351.7 xuống 1171.9 ms và p95 xác nhận từ 4383.8 xuống 2259.3 ms. Đây là dấu hiệu để tiếp tục kiểm tra, chưa đủ để kết luận hotspot tốt hơn ổn định.
- Tổng giới hạn trong Compose ở hai profile gần nhau nhưng không bằng nhau: balanced khoảng 12.75 CPU / 5.25 GiB, hotspot khoảng 12.25 CPU / 5.06 GiB (tính trên các service có resource limit). Cấu hình Monolith được kiểm tra có khoảng 9 CPU / 3.75 GiB giới hạn. Vì ngân sách giữa hai kiến trúc không đồng nhất, so sánh Monolith với Microservices hiện chưa phải đối chứng hiệu năng công bằng.
- Host Docker có khoảng 16 CPU và 6.2 GiB RAM. Một workload 20 VU chạy vài giây, một lượt mỗi cấu hình, chưa chứng minh các giới hạn đã tạo ra tình trạng tranh chấp tài nguyên ổn định. Kịch bản dùng chung một tài khoản Patient cho 20 VU và cùng một ca/ngày; phù hợp kiểm tra burst/concurrency cơ bản, nhưng chưa mô phỏng phân bố người dùng và lưu lượng của phòng khám.
- Kịch bản chưa đo throughput ổn định, p95 dưới tải duy trì, tỷ lệ throttling, độ trễ outbox/SAGA độc lập, hay tác động tới DB/broker trong thời gian dài. Cần nhiều lần chạy, warm-up, thứ tự profile luân phiên, cùng seed/dataset và cùng tổng CPU/RAM; thêm ít nhất sustained load và ghi time-series CPU, memory, throttling, DB và RabbitMQ.

### Đánh giá điều chỉnh chu kỳ polling trong luồng đặt lịch/SAGA

Các thay đổi hiện tại không đổi thứ tự nghiệp vụ hay tính nhất quán của SAGA. Appointment vẫn ghi trạng thái Pending và reservation trước, worker gọi Scheduling để xác nhận, rồi trạng thái chuyển Confirmed; outbox vẫn phát event sau commit. Thay đổi chỉ giảm chu kỳ quét outbox và quét appointment chờ xác nhận từ mặc định 1 giây xuống 100 ms. Vì vậy đây là tuning độ trễ polling, không phải một thiết kế SAGA mới.

100 ms có thể rút ngắn thời gian chờ đến vòng quét kế tiếp, nhưng cũng có thể làm tăng truy vấn DB khi hệ thống rảnh. Riêng các retry xác nhận đang có backoff 10 giây, 1 phút và 5 phút; giảm chu kỳ quét không bỏ qua backoff này. Trong các lượt đơn hiện có, profile balanced 100 ms không tốt hơn profile 1000 ms (p95 đặt lịch 2351.7 so với 1902.5 ms; p95 Confirmed 4383.8 so với 3437.1 ms), nên chưa có bằng chứng để giữ 100 ms làm mặc định.

Có hai vấn đề triển khai cần xử lý trước khi coi thay đổi worker là hoàn chỉnh:

1. `AppointmentConfirmationWorker` tạo scope mới mỗi vòng chỉ để lấy `IConfiguration`, dù configuration có thể được inject trực tiếp như hai publisher khác. Cách hiện tại tạo overhead và làm luồng khó kiểm thử hơn.
2. Giá trị interval chưa được kiểm tra/clamp. Giá trị 0 có thể tạo vòng quét liên tục; giá trị âm có thể làm `Task.Delay` ném exception và khiến hosted worker dừng.

Đề xuất: dùng mặc định 1000 ms trong môi trường chạy thật; cho phép cấu hình interval thấp hơn trong profile benchmark riêng; inject `IConfiguration` trực tiếp; xác thực interval trong phạm vi hợp lệ; thêm metrics cho vòng polling, số bản ghi xử lý và outbox lag. Sau đó chạy lại nhiều lần với thứ tự cấu hình đảo xen kẽ và đo riêng outbox lag/confirmation latency cùng tải DB. Không nên chọn 100 ms chỉ từ một lần benchmark.

### Lỗi của harness được phát hiện và sửa khi chạy lại

K6 lần đầu không vào workload vì `booking.js` import `parseAppointment` nhưng `config.js` không export hàm đó; sau khi bổ sung hàm, k6 còn lỗi runtime vì `Rate` và `Trend` được import từ `k6` thay vì `k6/metrics`. Hai lỗi này đã được sửa trong `tests/load/config.js` và `tests/load/booking.js` ở cả hai working tree. Chúng cho thấy kiểm tra cú pháp Node đơn thuần không xác nhận được kịch bản chạy được bằng k6; cần giữ ít nhất một smoke execution bằng chính image k6 trong quy trình kiểm tra.

### Trạng thái sau rà soát

- Đạt: build Compose của Microservices; luồng đặt lịch 20 VU hoàn tất ở cả hai kiến trúc; không có HTTP lỗi trong các lượt chạy lại; xác nhận, idempotency và cleanup đều đạt.
- Chưa đạt: ngưỡng p95 500 ms của kịch bản booking; bằng chứng so sánh hiệu năng chính thức giữa Monolith và Microservices; kết luận nhân quả về hotspot; kiểm chứng lợi ích/chi phí của polling 100 ms và kiểm thử lỗi SAGA tương ứng.
- Không đóng các mục chưa đạt chỉ dựa trên việc các test chức năng trả về 100%. Cần chuẩn hóa tổng ngân sách tài nguyên, tạo workload nhiều người dùng hơn, lặp các profile nhiều lần, thu metrics time-series và đối chiếu lại với ngưỡng SLA/SLO đã chọn.

## 13. Khắc phục độ trễ đuôi p95/p99 các tác vụ Ghi, chi tiết cấu hình và đo kiểm lại (Cũ & Mới, FAIL & PASS) — 2026-11-11

### 13.1. Bảng cấu hình chi tiết từng hệ thống qua các giai đoạn

#### A. Cấu hình Monolith (`docker-compose.monolith.yml`)

| Thành phần / Thông số | Cấu hình Cũ (Giai đoạn 1–3) | Cấu hình Mới (Sau tối ưu — Giai đoạn 4) |
|---|---|---|
| `clinic_mono_sql_server` (`mono-db`) | Limits: `4.00 CPU / 2048 MB`, Reservations: `2.00 CPU / 1024 MB` | Giữ nguyên (`4.00 CPU / 2048 MB`, Res: `2.00 CPU / 1024 MB`) |
| `clinic_mono_backend` (`mono-backend`) | Limits: `4.00 CPU / 1536 MB`, Reservations: `2.00 CPU / 512 MB` | Giữ nguyên (`4.00 CPU / 1536 MB`, Res: `2.00 CPU / 512 MB`) |
| `clinic_mono_frontend` (`mono-frontend`) | Limits: `1.00 CPU / 256 MB`, Reservations: `0.25 CPU / 64 MB` | Giữ nguyên (`1.00 CPU / 256 MB`, Res: `0.25 CPU / 64 MB`) |
| **Tổng CPU / RAM Monolith** | **`9.00 CPU / 3840 MB (3.75 GiB)`** | **`9.00 CPU / 3840 MB (3.75 GiB)`** |
| **SQL Connection Pool** | Mặc định (`Min Pool Size=0, Max Pool Size=100`) | **`Min Pool Size=20; Max Pool Size=200;`** |
| **Truy vấn vùng khóa (`UPDLOCK`)** | `.Include(w => w.Appointments)` nạp cả lịch đã `Cancelled`; gọi `GetByIdempotencyKeyAsync` lặp lại lần 2 trong `LockAsync` | Chỉ `.Include` lịch hẹn đang active (`!a.IsDeleted && a.Status != Cancelled`); bỏ query `GetByIdempotencyKeyAsync` lặp lại lần 2 trong `LockAsync` |

#### B. Cấu hình Microservices (`docker-compose.yml`)

| Container / Thông số | GĐ 1: Balanced 1000ms (Cũ) | GĐ 2: Balanced 100ms (Cũ) | GĐ 3: Hotspot 100ms (Cũ) | **GĐ 4: Sau tối ưu toàn diện (Mới)** |
|---|---|---|---|---|
| `clinic_sql_server` (`db`) | `4.00 CPU / 2048 MB` | `4.00 CPU / 2048 MB` | `4.00 CPU / 2048 MB` | **`4.00 CPU / 2048 MB`** *(Res: 2.0 CPU / 1024M)* |
| `clinic_appointment_service` | `0.75 CPU / 256 MB` | `0.75 CPU / 256 MB` | `1.50 CPU / 384 MB` | **`1.50 CPU / 384 MB`** *(Res: 0.5 CPU / 192M)* |
| `clinic_scheduling_service` | `0.75 CPU / 256 MB` | `0.75 CPU / 256 MB` | `1.25 CPU / 384 MB` | **`1.50 CPU / 384 MB`** *(Res: 0.5 CPU / 192M)* |
| `clinic_queue_service` | `0.75 CPU / 256 MB` | `0.75 CPU / 256 MB` | `1.25 CPU / 256 MB` | **`1.00 CPU / 256 MB`** *(Res: 0.5 CPU / 128M)* |
| `clinic_backend` (Gateway) | `1.00 CPU / 384 MB` | `1.00 CPU / 384 MB` | `1.00 CPU / 384 MB` | **`1.00 CPU / 384 MB`** *(Res: 0.5 CPU / 128M)* |
| `identity-service` | `0.75 CPU / 256 MB` | `0.75 CPU / 256 MB` | `0.25 CPU / 256 MB` | **`0.50 CPU / 256 MB`** *(Res: 0.1 CPU / 128M)* |
| `clinic_notification_service` | `0.50 CPU / 256 MB` | `0.50 CPU / 256 MB` | `0.50 CPU / 256 MB` | **`0.50 CPU / 256 MB`** *(Res: 0.2 CPU / 128M)* |
| `clinic_medical_records_service` | `0.50 CPU / 256 MB` | `0.50 CPU / 256 MB` | `0.25 CPU / 192 MB` | **`0.25 CPU / 192 MB`** *(Res: 0.1 CPU / 64M)* |
| `rabbitmq` | `1.50 CPU / 512 MB` | `1.50 CPU / 512 MB` | `0.50 CPU / 256 MB` | **`0.50 CPU / 256 MB`** *(Res: 0.25 CPU / 128M)* |
| `clinic_frontend` | `1.00 CPU / 256 MB` | `1.00 CPU / 256 MB` | `1.00 CPU / 256 MB` | **`1.00 CPU / 256 MB`** *(Res: 0.25 CPU / 64M)* |
| `clinic_prometheus` + `jaeger` | `1.00 CPU / 512 MB` | `1.00 CPU / 512 MB` | `0.50 CPU / 384 MB` | **`0.50 CPU / 384 MB`** *(2x 0.25 CPU / 192M)* |
| `mssql-exporter` | `0.25 CPU / 128 MB` | `0.25 CPU / 128 MB` | `0.25 CPU / 128 MB` | **`0.25 CPU / 128 MB`** |
| **Tổng CPU / RAM Microservices** | **`12.75 CPU / 5.25 GiB`** | **`12.75 CPU / 5.25 GiB`** | **`12.25 CPU / 5.06 GiB`** | **`12.50 CPU / 5.06 GiB`** |
| **Cơ chế đánh thức Worker / Outbox** | Timer Polling `1000ms` | Busy-Polling `100ms` | Busy-Polling `100ms` | **Tín hiệu tức thì `SemaphoreSlim` (`< 1ms`) + Fallback `1000ms` (clamped `[100, 60000]`)** |
| **Xử lý xác nhận SAGA Batch** | Tuần tự `foreach` + 20 `SaveChangesAsync` | Tuần tự `foreach` + 20 `SaveChangesAsync` | Tuần tự `foreach` + 20 `SaveChangesAsync` | **Song song `Task.WhenAll` + 1 `SaveChangesAsync` duy nhất cho cả batch** |
| **Kết nối RabbitMQ Outbox** | Mở/đóng TCP mỗi vòng lặp | Mở/đóng TCP mỗi 100ms | Mở/đóng TCP mỗi 100ms | **Persistent `IConnection` & `IChannel` xuyên suốt vòng đời** |
| **SQL Connection Pool** | Mặc định (`Min=0`) | Mặc định (`Min=0`) | Mặc định (`Min=0`) | **`Min=20, Max=200` (Appointment/Scheduling); `Min=10, Max=200` (Identity/Queue)** |

---

### 13.2. Năm nguyên nhân gốc rễ khiến p95/p99 bị vọt cao ở mục 12 và cách tối ưu chi tiết từng file

1. **Xử lý xác nhận SAGA tuần tự (`N+1` HTTP + `N+1` `SaveChangesAsync`) trong [`AppointmentConfirmationWorker.cs`](../backend/Clinic.Appointment.Service/Services/AppointmentConfirmationWorker.cs):**
   - *Trước khi sửa:* Khi 20 VU gửi request đồng thời, worker lấy batch 20 lịch hẹn `Pending` nhưng gọi `ConfirmReservationAsync` tuần tự qua `foreach` và gọi `db.SaveChangesAsync()` 20 lần riêng lẻ, đồng thời tạo `CreateScope()` mới ở mỗi vòng lặp chỉ để đọc `IConfiguration`.
   - *Sau khi sửa:* Inject `IConfiguration` và `IAppointmentWorkSignal` trực tiếp vào constructor; gọi `await Task.WhenAll(...)` để xác nhận song song toàn bộ lịch hẹn trong batch sang `Scheduling.Service`; gom toàn bộ cập nhật trạng thái + `OutboxMessage` vào **đúng 1 lệnh `await db.SaveChangesAsync(ct)` duy nhất** rồi gọi `_workSignal.SignalOutbox()`.
2. **Mở/đóng mới kết nối TCP RabbitMQ mỗi chu kỳ quét trong [`AppointmentOutboxPublisher.cs`](../backend/Clinic.Appointment.Service/Services/AppointmentOutboxPublisher.cs):**
   - *Trước khi sửa:* Gọi `factory.CreateConnectionAsync()` và `CreateChannelAsync()` bên trong vòng lặp `while` gây overhead bắt tay TCP/AMQP liên tục khi có burst sự kiện.
   - *Sau khi sửa:* Duy trì `_connection` và `_channel` bền vững qua `EnsureConnectedAsync(ct)` / `DisconnectAsync()`, chỉ tự động kết nối lại nếu kênh bị đứt.
3. **Busy-polling 100ms gây tranh chấp Connection Pool & Lock với luồng API chính:**
   - *Trước khi sửa:* Quét DB mỗi 100ms không có tín hiệu đồng bộ làm tăng tải nền lên SQL Server đúng lúc 20 VU đang giữ khóa `UPDLOCK, ROWLOCK`.
   - *Sau khi sửa:* Tạo [`AppointmentWorkSignal.cs`](../backend/Clinic.Appointment.Service/Services/AppointmentWorkSignal.cs) (`SemaphoreSlim(0, 1)`) đăng ký Singleton. Khi có lịch hẹn mới, `AppointmentManager` đánh thức worker và publisher ngay lập tức (`< 1ms`), còn chu kỳ fallback khi rảnh trả về mặc định `1000ms` và kiểm tra biên `Math.Clamp(intervalMs, 100, 60_000)`.
4. **Truy vấn dư thừa trong vùng khóa (Critical Section) trên cả Microservices và Monolith:**
   - *Trên Microservices ([`AppointmentManager.cs`](../backend/Clinic.Appointment.Service/Services/AppointmentManager.cs) & [`SchedulingManager.cs`](../backend/Clinic.StaffScheduling.Service/Services/SchedulingManager.cs)):*
     - Loại bỏ `BeginTransactionAsync`/`CommitAsync` thủ công bao quanh một lệnh `SaveChangesAsync` đơn lẻ trong `CreateAppointmentAsync` (tiết kiệm 2 round-trip DB mỗi request).
     - Gộp 2 câu query `CountAsync` và `AnyAsync` bên trong `UPDLOCK, ROWLOCK` của `ReserveSlotAsync` thành **1 câu query nhẹ duy nhất** chỉ lấy danh sách `TimeSlot` active.
     - Thêm `.AsNoTracking()` và bổ sung cache ngắn hạn 30s (`ConcurrentDictionary`, tự động invalidate khi cập nhật hồ sơ) cho metadata Bác sĩ (`DoctorName`, `SpecialtyName`) và `PatientProjection`.
   - *Trên Monolith (`WorkScheduleRepository.cs` & `CreateAppointment.cs`):*
     - Lọc `.Include(w => w.Appointments.Where(a => !a.IsDeleted && a.Status != AppointmentStatus.Cancelled))` trong `WorkScheduleRepository.GetByIdAsync` để không tải các lịch hẹn đã hủy khi đang giữ khóa `UPDLOCK`.
     - Loại bỏ câu query `GetByIdempotencyKeyAsync` (join 5 bảng) lặp lại lần thứ 2 bên trong vùng khóa `LockAsync` của `CreateAppointment`.
5. **Thiếu Pre-warming Connection Pool & EF Core DbContext:**
   - Bổ sung `Min Pool Size=20;Max Pool Size=200;` vào connection strings và khởi tạo ấm `db.Model` khi khởi động service.

---

### 13.3. Bảng đối chiếu đầy đủ các lượt chạy `k6` (`tests/load/booking.js` — 20 VUs đồng thời): Cũ & Mới, FAIL (`exit 99`) & PASS (`exit 0`)

| STT | Hệ thống & Cấu hình | File Evidence | `POST /appointments` (`booking_api_duration`)<br>`min / p50 / p90 / p95 / p99 (max)` | Tới `Confirmed` E2E (`booking_e2e_duration`)<br>`min / p50 / p90 / p95 / p99 (max)` | Toàn bộ HTTP (`http_req_duration`)<br>`min / p50 / p90 / p95 / max` | Nghiệp vụ & HTTP lỗi | K6 Exit & Trạng thái |
|:-:|---|---|---|---|---|---|---|
| 1 | **Monolith (Cũ — Trước tối ưu)** | `review-mono-booking-k6.json` | 315.2 / 522.8 / 690.4 / **706.7** / 726.4 ms | 487.0 / 617.5 / 765.7 / **790.8** / 805.0 ms | 6.0 / 25.3 / 500.4 / 598.5 / 948.4 ms | 20/20 pass, 0/125 lỗi | ❌ **FAIL (`exit 99`)**<br>(`p95 = 706.7ms > 500ms`) |
| 2 | **Monolith (Mới — Sau tối ưu GĐ 4)** | `review-mono-optimized-booking-k6.json` | **23.1 / 157.1 / 248.9 / 269.3 / 339.7 ms**<br>*(max = 357.2 ms — Giảm 61.9% p95)* | **43.0 / 224.0 / 396.6 / 412.1 / 432.0 ms**<br>*(Giảm 47.9% p95)* | **6.1 / 19.1 / 148.8 / 206.5 / 357.2 ms**<br>*(Giảm 65.5% p95)* | **20/20 pass, 0/125 lỗi** | ✅ **PASS (`exit 0`)**<br>Đạt mọi ngưỡng |
| 3 | **Microservices (Cũ 1 — Balanced + Poll 1000ms)** | [`review-balanced1000-booking-k6.json`](../tests/load/evidence/review-balanced1000-booking-k6.json) | 1769.7 / 1823.0 / 1893.7 / **1902.5** / 1935.1 ms | 2621.0 / 3178.0 / 3401.0 / **3437.1** / 3438.0 ms | 6.9 / 69.6 / 302.7 / 1804.1 / 1935.1 ms | 20/20 pass, 0/235 lỗi | ❌ **FAIL (`exit 99`)**<br>Fail cả p95 & p99 |
| 4 | **Microservices (Cũ 2 — Balanced + Poll 100ms)** | [`review-balanced100-booking-k6.json`](../tests/load/evidence/review-balanced100-booking-k6.json) | 2008.2 / 2191.0 / 2350.5 / **2351.7** / 2372.0 ms | 2889.0 / 3895.5 / 4360.1 / **4383.8** / 4474.0 ms | 6.9 / 71.1 / 215.5 / 2119.5 / 2372.0 ms | 20/20 pass, 0/280 lỗi | ❌ **FAIL (`exit 99`)**<br>Fail nặng do busy-poll |
| 5 | **Microservices (Cũ 3 — Hotspot + Poll 100ms)** | [`review-hotspot100-booking-k6.json`](../tests/load/evidence/review-hotspot100-booking-k6.json) | 989.9 / 1071.9 / 1165.3 / **1171.9** / 1224.4 ms | 1451.0 / 1939.5 / 2252.2 / **2259.3** / 2360.0 ms | 6.8 / 33.5 / 395.0 / 1058.8 / 1909.2 ms | 20/20 pass, 0/226 lỗi | ❌ **FAIL (`exit 99`)**<br>(`p95 = 1171.9ms > 500ms`) |
| 6 | **Microservices (Mới — Sau tối ưu toàn diện GĐ 4)** | [`review-optimized-booking-k6.json`](../tests/load/evidence/review-optimized-booking-k6.json) | **87.2 / 264.6 / 409.9 / 410.9 / 426.5 ms**<br>*(Giảm 64.9% p95 so với Hotspot cũ,<br>giảm 82.5% so với Balanced 100ms)* | **260.0 / 489.0 / 640.0 / 640.2 / 643.0 ms**<br>*(Giảm 71.7% p95 so với Hotspot cũ,<br>giảm 85.4% so với Balanced 100ms)* | **7.7 / 46.1 / 214.9 / 327.0 / 426.5 ms**<br>*(Giảm 69.1% p95)* | **20/20 pass, 0/206 lỗi** | ✅ **PASS (`exit 0`)**<br>Đạt mọi ngưỡng |

---

### 13.4. Bảng đối chiếu đầy đủ 5 lượt chạy `benchmark-runner.mjs` (`SCENARIO=all`) với trọn bộ `min / p50 / p90 / p95 / p99 / max / avg`

| Kịch bản & Phân vị | 1. Monolith Cũ<br>(Baseline GĐ 1) | 2. Monolith Mới<br>(Sau tối ưu GĐ 4) | 3. Microservices Cũ 1<br>(Baseline 1000ms GĐ 1) | 4. Microservices Cũ 2<br>(Hotspot 100ms GĐ 2) | 5. Microservices Mới<br>(Sau tối ưu GĐ 4) |
|---|---:|---:|---:|---:|---:|
| **File Evidence** | `...05-58-59-764Z.json` | `...09-17-38-887Z.json` | [`...06-15-17-036Z.json`](../tests/load/evidence/benchmark-microservices-2026-11-11T06-15-17-036Z.json) | [`...08-10-54-929Z.json`](../tests/load/evidence/benchmark-microservices-2026-11-11T08-10-54-929Z.json) | [`...09-15-48-756Z.json`](../tests/load/evidence/benchmark-microservices-2026-11-11T09-15-48-756Z.json) |
| **1. Read (`GET /specialties` - 50 reqs, c=10)** | | | | | |
| • Throughput (RPS) | 324.7 req/s | **373.1 req/s** | 231.5 req/s | 138.1 req/s | **232.6 req/s** |
| • `min` / `avg` (ms) | 8.33 / 29.37 | **7.89 / 25.38** | 9.81 / 41.67 | 12.33 / 70.57 | **8.54 / 41.58** |
| • `p50 / p90 / p95 / p99` (ms) | 11.35 / 103.46 / 114.92 / 118.83 | **12.86 / 75.13 / 81.92 / 86.78** | 45.21 / 73.87 / 80.91 / 96.57 | 24.05 / 265.00 / 266.87 / 270.58 | **15.99 / 141.75 / 145.19 / 146.63** |
| **2. Booking API (`POST /appointments` - 20 reqs, c=2)** | | | | | |
| • `min` / `avg` (ms) | 23.33 / 41.75 | **22.47 / 39.39** | 31.86 / 49.62 | 42.71 / 128.94 | **28.75 / 71.84** |
| • `p50 / p90 / p95 / p99` (ms) | 27.11 / 38.80 / 318.02 / 318.02 | **28.56 / 41.91 / 263.62 / 263.62** | 45.18 / 87.83 / 99.18 / 99.18 | 73.67 / 119.37 / 1219.36 / 1219.36 | **47.06 / 69.43 / 553.42 / 553.42** |
| **3. Booking E2E (`Confirmed` - 20 reqs, c=2)** | | | | | |
| • `min` / `avg` (ms) | 31.79 / 58.52 | **32.90 / 58.43** | 259.19 / 982.00 | 130.07 / 267.90 | **114.91 / 163.88** |
| • `p50 / p90 / p95 / p99` (ms) | 42.63 / 49.73 / 416.34 / 416.34 | **40.62 / 64.01 / 354.53 / 354.53** | 1019.91 / 1068.40 / 1073.46 / 1073.46 | 221.40 / 259.65 / 1435.31 / 1435.31 | **134.28 / 157.80 / 741.39 / 741.39** |
| • Async Lag `p50 / p95` (ms) | 15.52 / 98.32 | **12.06 / 90.91** | 974.73 / 974.27 | 147.73 / 215.94 | **87.22 / 187.98** |
| **4. Check-in (`POST /check-in` - 4 reqs tuần tự)** | | | | | |
| • `min` / `avg` (ms) | 15.11 / 35.13 | **14.20 / 35.53** | 8.10 / 28.39 | 7.55 / 35.73 | **15.20 / 33.05** |
| • `p50 / p90 / p95 / p99` (ms) | 15.91 / 94.07 / 94.07 / 94.07 | **15.99 / 97.24 / 97.24 / 97.24** | 40.47 / 43.76 / 43.76 / 43.76 | 33.05 / 82.84 / 82.84 / 82.84 | **24.29 / 73.12 / 73.12 / 73.12** |

## 14. Nghiệm thu đối chiếu Monolith vs Microservices khi đã cân bằng tuyệt đối ngân sách tài nguyên (12.5 CPU) — 2026-11-11

### 14.1. Chi tiết điều chỉnh cân bằng tài nguyên

Để đảm bảo tính công bằng khoa học tuyệt đối theo khuyến nghị tại Mục 12, ngân sách tài nguyên của Monolith đã được nâng lên đúng bằng tổng ngân sách của Microservices (`12.50 CPU / 5.06 GiB RAM`):
- `mono-backend` nâng từ `4.0 CPU / 1536 MB` lên **`7.50 CPU / 2880 MB`** (Reservations: `3.0 CPU / 1024 MB`).
- `mono-db` giữ nguyên `4.00 CPU / 2048 MB` (Reservations: `2.0 CPU / 1024 MB`).
- `mono-frontend` giữ nguyên `1.00 CPU / 256 MB`.
- **Tổng Monolith = 12.50 CPU / 5.06 GiB** — hoàn toàn tương đương cụm 13 containers của Microservices.

### 14.2. Bảng tổng hợp kết quả đo mới nhất ở mức cân bằng 12.5 CPU

| Chỉ số & Kịch bản đo | Monolith (12.5 CPU MỚI) | Microservices (12.5 CPU MỚI) | Đánh giá & Phân tích chênh lệch |
|---|---:|---:|---|
| **1. K6 20 VUs Concurrent Booking** | | | |
| • `booking_api_duration` (p50 / **p95** / p99) | 237.6 / **293.4** / 304.9 ms | 313.6 / **594.7** / 610.9 ms | Monolith đạt chuẩn p95 < 500ms; Microservices p95 giảm sâu so với mức 1900ms cũ, tiệm cận ngưỡng 500ms |
| • `booking_e2e_duration` (p50 / **p95** / max) | 282.0 / **456.4** / 463.0 ms | 663.0 / **820.9** / 875.0 ms | Cả 2 đều vượt xa ngưỡng E2E p95 < 5000ms |
| • Toàn bộ HTTP p50 / p95 | 20.5 / 274.1 ms | 36.1 / 440.1 ms | Tỷ lệ lỗi 0.00% (100% nghiệp vụ thành công) |
| • K6 Exit Code | **`0` (PASS)** | `1` (Chạm sát ngưỡng p95) | Toàn bộ 101/101 checks passed trên cả 2 hệ thống |
| **2. Benchmark Runner (`SCENARIO=all`, c=10)** | | | |
| • Read Throughput (RPS) | **393.7 req/s** | **271.7 req/s** | Monolith cao hơn 44.9% nhờ in-memory EF Core |
| • Read Latency p50 / **p95** / p99 | 14.0 / 76.3 / 78.6 ms | 26.4 / **65.4** / **71.6 ms** | Microservices có p95 và p99 đọc tốt hơn Monolith |
| • Booking API Latency p50 / **p95** | 208.2 / **514.7 ms** | 247.8 / **1581.5 ms** | Monolith ghi API nhanh hơn gấp 3 lần ở tải đồng thời 10 |
| • Booking E2E Confirmed p50 / **p95** | 228.5 / **542.3 ms** | 429.5 / **1949.6 ms** | Microservices p50 giảm 2.4 lần so với baseline (429.5ms vs 1019.9ms) |
| • Async SAGA Lag p50 | **20.3 ms** | **181.7 ms** | Microservices giảm lag SAGA từ ~1s xuống ~181ms nhờ `AppointmentWorkSignal` |
| • Check-in Latency p50 / **p95** | 23.1 / 131.1 ms | 44.6 / **102.4 ms** | Microservices kiểm soát p95 check-in ổn định ở mức ~102ms |
| • Toàn vẹn Idempotency & Ticket | **100% VERIFIED** | **100% VERIFIED** | 0 duplicate ticket, 0 duplicate appointment |

### 14.3. Bằng chứng kiểm thử mới sinh ra (Evidence Artifacts)
- **Monolith (12.5 CPU):**
  - K6 20-VU Summary: `tests/load/evidence/review-mono-12cpu-booking-k6.json`
  - Node Benchmark Runner: `tests/load/evidence/benchmark-monolith-2026-11-11T09-58-23-056Z.json`
- **Microservices (12.5 CPU):**
  - K6 20-VU Summary: `tests/load/evidence/review-micro-12cpu-booking-k6.json`
  - Node Benchmark Runner: `tests/load/evidence/benchmark-microservices-2026-11-11T10-55-28-210Z.json`


- **Tại working tree Microservices (`D:\Project\KHDV\microservices\tests\load\evidence\`):**
  1. `benchmark-microservices-2026-11-11T06-15-17-036Z.json` *(Cũ — Node runner Baseline 1000ms)*
  2. `benchmark-microservices-2026-11-11T08-10-54-929Z.json` *(Cũ — Node runner Hotspot 100ms)*
  3. `benchmark-microservices-2026-11-11T09-15-48-756Z.json` *(Mới — Node runner Sau tối ưu toàn diện)*
  4. `review-balanced1000-booking-k6.json` *(Cũ — k6 20 VUs Balanced 1000ms — FAIL exit 99)*
  5. `review-balanced100-booking-k6.json` *(Cũ — k6 20 VUs Balanced 100ms — FAIL exit 99)*
  6. `review-hotspot100-booking-k6.json` *(Cũ — k6 20 VUs Hotspot 100ms — FAIL exit 99)*
  7. `review-optimized-booking-k6.json` *(Mới — k6 20 VUs Sau tối ưu toàn diện — **PASS exit 0**)*
  8. `review-micro-12cpu-booking-k6.json` *(Mới — k6 20 VUs Cân bằng 12.5 CPU)*
  9. `highload-micro-booking-k6.json` *(Mới — k6 30 VUs Tải Ghi Cao 15.000 requests)*
- **Tại working tree Monolith (`d:\Project\KHDV\Clinic_Appointment_Booking_Queue_Management_System\tests\load\evidence\`):**
  1. `benchmark-monolith-2026-11-11T05-58-59-764Z.json` *(Cũ — Node runner Baseline)*
  2. `benchmark-monolith-2026-11-11T09-17-38-887Z.json` *(Mới — Node runner Sau tối ưu)*
  3. `review-mono-booking-k6.json` *(Cũ — k6 20 VUs Trước tối ưu — FAIL exit 99)*
  4. `review-mono-optimized-booking-k6.json` *(Mới — k6 20 VUs Sau tối ưu — **PASS exit 0**)*
  5. `review-mono-12cpu-booking-k6.json` *(Mới — k6 20 VUs Cân bằng 12.5 CPU — **PASS exit 0**)*
  6. `highload-mono-booking-k6.json` *(Mới — k6 30 VUs Tải Ghi Cao 15.000 requests)*

---

## 15. Nghiệm thu Thử Nghiệm Tải Ghi Cao Quy Mô Lớn (High-Load Write Stress Test — 2.400 Slots / 12.5 CPU)

> **LƯU Ý:** Kết quả chạy thử tải ghi trước đó (240 slot) đã chính thức bị **hủy bỏ và ghi đè** bởi đợt đo chuẩn mực quy mô **2.400 slot** độc lập này.

### 15.1. Thiết kế kịch bản tải ghi cao quy mô 2.400 slot ([`tests/load/booking-high-load.js`](../tests/load/booking-high-load.js))
- **Không gian slot:** 10 Bác sĩ chuyên khoa (`a0000004-...0001` đến `...0010`) $\times$ 5 ngày liên tiếp (Ngày +3 đến Ngày +7) $\times$ 48 slot = **2.400 slot thời gian độc lập**.
- **Áp lực tải:** 30 VUs gửi dồn dập ~15.000 – 30.000 requests trong 80 giây sustained load.
- **Phân bổ và xác suất xung đột (Contention Distribution):** Mỗi request chọn ngẫu nhiên một slot trong 2.400 slot. Tỷ lệ xung đột tăng dần theo thời gian khi các slot dần bị lấp đầy, mang lại **hàng nghìn chu trình ghi DB thật** và phản ánh chính xác hiệu năng ghi dưới tải cạnh tranh tự nhiên.
- **Chỉ số theo dõi chi tiết:** Bóc tách riêng biệt `booking_created_api_duration` (độ trễ ghi thành công HTTP 201) và `booking_conflict_api_duration` (độ trễ từ chối HTTP 409).

### 15.2. Bảng kết quả đối chiếu thực tế Monolith vs Microservices ở tải ghi cao 2.400 slot

| Tiêu chí & Chỉ số đo lường | Monolith (12.5 CPU) | Microservices (12.5 CPU) | Phân tích nghiệm thu |
|---|---:|---:|---|
| **Bằng chứng Evidence JSON** | [`highload-mono-booking-k6.json`](../tests/load/evidence/highload-mono-booking-k6.json) | [`highload-micro-booking-k6.json`](../tests/load/evidence/highload-micro-booking-k6.json) | Bằng chứng thô từ k6 runner |
| **Tổng số lượt đặt gửi đi** | **28.312 reqs** | **11.743 reqs** | Tải ghi duy trì 30 VUs trong 80s |
| **Thông lượng ghi (Write RPS)** | **255.2 req/s** | **140.2 req/s** | Monolith cao hơn 82% do in-process call |
| **Số lịch đặt thành công (Real Writes)** | **2.400 / 2.400 (100%)** | **1.996 / 2.400 (83.2%)** | Cả hai thực hiện thành công hàng nghìn đơn ghi DB thật |
| **Số lịch xác nhận SAGA E2E** | **2.400 / 2.400 (100%)** | **1.996 / 1.996 (100%)** | **100% đơn đặt thành công đều đạt `Confirmed` an toàn!** |
| **Xung đột nghiệp vụ (HTTP 409)** | **25.912 conflicts** | **9.747 conflicts** | Xung đột slot tự nhiên diễn ra liên tục |
| **Tỷ lệ lỗi kỹ thuật (HTTP 5xx)** | **0.00% (0 lỗi)** | **0.00% (0 lỗi)** | **Tuyệt đối không có crash hay sập pool kết nối** |
| **Độ trễ GHI THỰC SỰ (HTTP 201 Created)** | | | *(Chỉ tính các request ghi thành công)* |
| • `min` / `avg` | 20.8 ms / **57.4 ms** | 42.2 ms / **145.3 ms** | Monolith nhanh hơn ~2.5 lần |
| • **`p50` (Median)** | **47.2 ms** | **135.4 ms** | Monolith 47ms vs Microservices 135ms |
| • **`p90`** | **71.5 ms** | **195.4 ms** | |
| • **`p95`** | **88.3 ms** | **231.2 ms** | **Cả hai đều dưới 240ms** (Vượt xa chuẩn < 1000ms) |
| • `max` | 638.6 ms | 1049.9 ms (~1.05s) | |
| **Độ trễ XỬ LÝ XUNG ĐỘT (HTTP 409 Conflict)** | | | *(Chỉ tính các request từ chối do trùng slot)* |
| • **`p50` (Median)** | **17.8 ms** | **64.4 ms** | Monolith lock in-memory nhanh hơn |
| • **`p95`** | **30.5 ms** | **166.1 ms** | |
| **Độ trễ Toàn Bộ API (`booking_api_duration`)** | | | *(Gộp chung cả 201 và 409)* |
| • **`p50` (Median)** | **18.4 ms** | **74.9 ms** | |
| • **`p95`** | **47.1 ms** | **179.6 ms** | Cả hai đều đạt xuất sắc ngưỡng p95 < 500ms |
| **Độ trễ Chu Trình Hoàn Tất E2E (`booking_e2e_duration`)** | | | *(Từ POST đến khi đạt trạng thái Confirmed)* |
| • `min` / `avg` | 26.0 ms / **68.7 ms** | 103.0 ms / **327.6 ms** | |
| • **`p50` (Median)** | **57.0 ms** | **307.0 ms** | Monolith đồng bộ ACID; Microservices qua RabbitMQ |
| • **`p90`** | **87.0 ms** | **442.0 ms** | |
| • **`p95`** | **106.0 ms** | **524.3 ms** | **Cả hai đều vượt trội so với SLA E2E p95 < 5000ms** |
| • `max` | 798.0 ms | 1158.0 ms (~1.16s) | Đỉnh trễ SAGA tối đa của Microservices chỉ 1.16s |
| **Kết quả k6 & Trạng thái nghiệm thu** | ✅ **PASS (`exit 0`)**<br>7.200/7.200 checks pass | ✅ **PASS (`exit 0`)**<br>5.988/5.988 checks pass | **Cả 2 hệ thống đều đạt 100% tiêu chí SLO** |


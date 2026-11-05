# Đặc tả Workload Kiểm thử Hiệu năng (Phase 9) — Thành viên A

**Tài liệu tham chiếu:** [`tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md)  
**Phiên bản:** 1.0.0  
**Tác giả:** Thành viên A  
**Ngày lập:** 04/11/2026  

---

## 1. Tổng quan mục đích và nguyên tắc

Đặc tả này xác lập chi tiết kỹ thuật cho 7 hành trình nghiệp vụ (Business Workloads) thực tế chạy qua API Gateway (`http://localhost:5000`), tương tác với 6 microservices backend (`Identity`, `Scheduling`, `Appointment`, `Queue`, `MedicalRecords`, `Notification`), cơ sở dữ liệu SQL Server (`Clinic*DB`) và message broker RabbitMQ.

### Nguyên tắc cốt lõi:
1. **Không giả lập (No Dummy/Catalog-only):** Không được lấy `GET /api/catalog` làm đại diện cho toàn bộ hệ thống. Mọi workload phải kích hoạt luồng xử lý nghiệp vụ thực thụ của từng domain service.
2. **Xác thực và Phân quyền đầy đủ:** Mọi request được gắn JWT Token hợp lệ từ Identity Service (`/auth/login`) với đúng vai trò (`Patient`, `Doctor`, `Receptionist`, `Admin`).
3. **Idempotency & Tracing Headers:** Mọi mutation command (POST/PUT/PATCH) bắt buộc truyền `Idempotency-Key: <unique-uuid>` và `X-Correlation-ID: <trace-uuid>`.
4. **Phân định rõ Error Contract:** 
   - HTTP 200/201/204: Thành công.
   - HTTP 409 Conflict: Hợp lệ trong kịch bản tranh chấp (Contention winner vs loser), không tính vào tỷ lệ lỗi 5xx hệ thống.
   - HTTP 429 Too Many Requests: Hợp lệ trong kịch bản Spike vượt rate limit cấu hình (Fixed Window 100 req/min/IP hoặc subject).
   - HTTP 5xx / Timeout (Gateway 504): Vi phạm SLO nếu vượt quá 1%.

---

## 2. Đặc tả chi tiết 7 Workloads Nghiệp vụ

### 2.1. Workload 1: Browse và Đọc dữ liệu (Read-Heavy Browsing)
- **Mục tiêu:** Kiểm thử năng lực xử lý truy vấn dữ liệu đồng thời của Gateway và các downstream service mà không làm suy giảm hiệu năng hoặc lộ dữ liệu giữa các người dùng.
- **Vai trò thực hiện:** Khách vãng lai (Anonymous) hoặc Bệnh nhân (`Patient`).
- **Danh sách Route & Protocol:**
  1. `GET /api/catalog` (Anonymous)
     - Headers: `Accept: application/json`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Danh sách service contracts, URLs và QoS metadata.
  2. `GET /specialties` (Anonymous)
     - Headers: `Accept: application/json`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Paged list các chuyên khoa y tế.
  3. `GET /doctors` (Anonymous)
     - Headers: `Accept: application/json`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Paged list bác sĩ và chuyên khoa tương ứng.
  4. `GET /me/appointments` (Patient JWT)
     - Headers: `Authorization: Bearer <patient_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Mảng lịch hẹn cá nhân của bệnh nhân đăng nhập.
  5. `GET /me/queue-status` (Patient JWT)
     - Headers: `Authorization: Bearer <patient_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Trạng thái hàng đợi hiện tại của bệnh nhân trong ngày.
  6. `GET /notifications` (Patient JWT)
     - Headers: `Authorization: Bearer <patient_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK`
     - Response Schema: Lịch sử thông báo in-app của tài khoản.
- **SLO chỉ định:** Latency P95 ≤ 300 ms, Availability ≥ 99.9%.

---

### 2.2. Workload 2: Đặt lịch khám thành công (End-to-End Successful Booking)
- **Mục tiêu:** Đo lường luồng Saga điều phối (Orchestration) giữa Gateway -> Appointment Service -> Staff & Scheduling Service (Reserve Slot) -> Outbox Event `appointment.scheduled` -> Notification Service.
- **Vai trò thực hiện:** Bệnh nhân (`Patient`) hoặc Lễ tân (`Receptionist`).
- **Luồng tuần tự:**
  1. `POST /auth/login`
     - Request: `{"username": "patient1", "password": "Password123!"}`
     - Expected Status: `200 OK` -> Trích xuất `result.accessToken`.
  2. `POST /appointments`
     - Headers: 
       - `Authorization: Bearer <token>`
       - `Idempotency-Key: book-<testRunId>-<vu>-<iter>`
       - `X-Correlation-ID: corr-book-<testRunId>-<vu>-<iter>`
       - `Content-Type: application/json`
     - Request Body:
       ```json
       {
         "workScheduleId": "<valid-work-schedule-uuid>",
         "timeSlot": "09:00:00",
         "reason": "Khám định kỳ tổng quát - Phase 9 Workload",
         "patientId": "<patient-uuid>",
         "isWalkIn": false
       }
       ```
     - Expected Status: `201 Created`
     - Invariant: Tạo đúng 1 record trong bảng `Appointments` trạng thái `Scheduled` hoặc `Confirmed`.
  3. `POST /appointments/{appointmentId}/confirm` (Optional/Staff confirm)
     - Expected Status: `200 OK` hoặc `204 NoContent`.
  4. `GET /appointments/{appointmentId}`
     - Expected Status: `200 OK`
     - Response Schema: Trạng thái xác nhận của lịch hẹn đã tạo.
- **SLO chỉ định:** Latency P95 ≤ 500 ms, P99 ≤ 1500 ms, Error Rate ≤ 1%.

---

### 2.3. Workload 3: Booking Contention (Tranh chấp Slot giới hạn)
- **Mục tiêu:** Thử thách cơ chế khóa đồng thời (Distributed Concurrency Control / Optimistic & Pessimistic Concurrency) của hệ thống khi N client cùng gửi request đặt chung 1 slot khám có `Capacity = 1`.
- **Vai trò thực hiện:** N Virtual Users (`patient1` .. `patientN`).
- **Route thực thi:**
  - `POST /appointments`
  - Headers:
    - `Authorization: Bearer <token_of_patient_i>`
    - `Idempotency-Key: cont-<testRunId>-slot-<slotId>-vu-<vu>`
    - `X-Correlation-ID: corr-cont-<testRunId>-vu-<vu>`
    - `Content-Type: application/json`
  - Request Body:
    ```json
    {
      "workScheduleId": "<contention-schedule-uuid>",
      "timeSlot": "10:00:00",
      "reason": "Contention stress test slot capacity=1",
      "isWalkIn": false
    }
    ```
- **Kỳ vọng nghiệp vụ & Tiêu chuẩn nghiệm thu:**
  - **Winner (chính xác 1 request duy nhất):** Nhận HTTP `201 Created` kèm thông tin cuộc hẹn được tạo.
  - **Losers (tất cả N-1 requests còn lại):** Nhận HTTP `409 Conflict` với thông báo `"Could not reserve slot. The slot may be full or already booked."`.
  - **Bất biến bất di bất dịch:** Tuyệt đối KHÔNG có 2 winner (Zero Overbooking); Tuyệt đối KHÔNG crash 500 Internal Server Error.

---

### 2.4. Workload 4: Check-in và Cấp số thứ tự (Check-in & Ticket Issuance)
- **Mục tiêu:** Kiểm tra quy trình tiếp đón bệnh nhân tại quầy, đảm bảo cấp số thứ tự tự động (`QueueNumber`) liên tục, không bao giờ trùng lặp hay nhảy cóc sai quy tắc.
- **Vai trò thực hiện:** Lễ tân (`Receptionist`) hoặc Quản trị viên (`Admin`).
- **Route thực thi:**
  - `POST /appointments/{appointmentId}/check-in?isUrgent=false`
  - Headers:
    - `Authorization: Bearer <receptionist_token>`
    - `Idempotency-Key: checkin-<testRunId>-app-<appointmentId>`
    - `X-Correlation-ID: corr-checkin-<testRunId>-<appointmentId>`
  - Request Body: Trống (hoặc query string `isUrgent=false`).
- **Kỳ vọng nghiệp vụ:**
  - Lần gọi đầu tiên: HTTP `204 NoContent`.
  - Outbox message `appointment.checked-in` được ghi vào `ClinicAppointmentDB.OutboxMessages` và đẩy vào RabbitMQ queue `q.queue-service.events`.
  - Queue Service tiêu thụ event, tăng nguyên tử `QueueCounters.LastNumber` cho `(DoctorId, QueueDate)` và tạo bản ghi duy nhất trong `QueueTickets`.
  - **Kiểm thử Idempotency:** Gửi lặp lại request check-in cùng `appointmentId` và `Idempotency-Key` -> Nhận HTTP `409 Conflict` (Lịch hẹn đã check-in trước đó), bảng `QueueTickets` vẫn giữ nguyên chính xác 1 vé khám.

---

### 2.5. Workload 5: Tác vụ Hàng đợi Bác sĩ (Queue Operations)
- **Mục tiêu:** Đo lường hiệu năng của chuỗi tác vụ điều hành phòng khám trong giờ làm việc.
- **Vai trò thực hiện:** Bác sĩ (`Doctor`) hoặc Y tá quầy (`Receptionist`).
- **Danh sách Route & Protocol:**
  1. `POST /queue/call-next/{doctorId}`
     - Headers: `Authorization: Bearer <doctor_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK` (Trả về vé được gọi kế tiếp) hoặc `204 NoContent`/`404 NotFound` khi hết người đợi.
  2. `POST /queue/{queueTicketId}/start-exam`
     - Headers: `Authorization: Bearer <doctor_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `204 NoContent` -> Chuyển vé sang `InProgress`.
  3. `POST /queue/{queueTicketId}/complete-exam`
     - Headers: `Authorization: Bearer <doctor_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `204 NoContent` -> Chuyển vé sang `Completed`.
  4. `POST /queue/{queueTicketId}/skip`
     - Headers: `Authorization: Bearer <doctor_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK` -> Chuyển vé sang `Skipped`.
- **SLO chỉ định:** Latency P95 ≤ 300 ms, Error Rate ≤ 0.5%.

---

### 2.6. Workload 6: Hồ sơ Khám bệnh (Medical Records)
- **Mục tiêu:** Đảm bảo luồng tạo, cập nhật bản nháp và chốt (Finalize) hồ sơ bệnh án hoạt động chuẩn xác, ghi log audit và không rò rỉ dữ liệu nhạy cảm (PHI).
- **Vai trò thực hiện:** Bác sĩ điều trị (`Doctor`).
- **Danh sách Route & Protocol:**
  1. `POST /medical-reports` (Tạo Draft)
     - Headers: 
       - `Authorization: Bearer <doctor_token>`
       - `Idempotency-Key: med-draft-<testRunId>-<appointmentId>`
       - `X-Correlation-ID: corr-med-<testRunId>-<appointmentId>`
       - `Content-Type: application/json`
     - Request Body:
       ```json
       {
         "appointmentId": "<appointment-uuid>",
         "symptoms": "Trieu chung kho chiu do tai kiem tra - Synthetic Workload",
         "diagnosis": "Theo doi viem duong ho hap tren",
         "prescription": "Paracetamol 500mg, Vitamin C 500mg",
         "notes": "Nghi ngoi, uong nhieu nuoc, tai kham neu sot cao"
       }
       ```
     - Expected Status: `200 OK`
  2. `PUT /medical-reports/{reportId}` (Cập nhật Draft)
     - Headers: `Authorization: Bearer <doctor_token>`, `Content-Type: application/json`
     - Expected Status: `200 OK`
  3. `POST /medical-reports/{reportId}/finalize` (Chốt bệnh án)
     - Headers: `Authorization: Bearer <doctor_token>`, `X-Correlation-ID: <uuid>`
     - Expected Status: `200 OK` -> Chuyển trạng thái sang `Finalized`.
- **Invariant:** Bệnh án đã Finalized không cho phép ghi đè tự do; mọi bổ sung phải qua endpoint `/amendments`.

---

### 2.7. Workload 7: Xử lý Bất đồng bộ và Luồng Sự kiện (Notification & Event Flow)
- **Mục tiêu:** Xác minh message bus RabbitMQ không bị ứ đọng backlog và không làm rơi vãi thông báo.
- **Cơ chế kiểm thử:**
  - Hệ thống chạy ở chế độ Mock (`LoggingEmailSender`) -> Không bao giờ gửi SMS/email ra ngoài internet.
  - Sau mỗi đợt gửi command ghi (Booking, Check-in, Finalize), event được đẩy qua Outbox Pattern -> RabbitMQ -> Consumer xử lý.
- **Tiêu chuẩn nghiệm thu:**
  - `Event Lag P95` ≤ 5.0 giây.
  - Số lượng message tại toàn bộ các Dead-Letter Queue (`*.dlq`, `*.dead`) phải bằng 0.
  - Số lượng thông báo in-app tăng tương ứng với số sự kiện thành công (`ClinicNotificationDB.Notifications`).

---

## 3. Bảng Ma trận Workloads, Vai trò và Ngưỡng SLO

| ID | Tên Workload | Method & Route chính | Role yêu cầu | P95 SLO | P99 SLO | Max Error Rate |
|---|---|---|---|---|---|---|
| **W1** | Read Browsing | `GET /specialties`, `/doctors`, `/me/appointments` | Anonymous, Patient | ≤ 300 ms | ≤ 800 ms | ≤ 0.1% |
| **W2** | Successful Booking | `POST /appointments` | Patient, Receptionist | ≤ 500 ms | ≤ 1500 ms | ≤ 1.0% |
| **W3** | Booking Contention | `POST /appointments` (Slot cap=1) | Patient pool | ≤ 500 ms | ≤ 1500 ms | 0% (Losers get 409) |
| **W4** | Check-in & Queue | `POST /appointments/{id}/check-in` | Receptionist, Admin | ≤ 500 ms | ≤ 1200 ms | ≤ 0.5% |
| **W5** | Queue Operations | `POST /queue/call-next`, `/complete-exam` | Doctor | ≤ 300 ms | ≤ 800 ms | ≤ 0.5% |
| **W6** | Medical Records | `POST /medical-reports`, `/finalize` | Doctor | ≤ 500 ms | ≤ 1200 ms | ≤ 0.5% |
| **W7** | Event & Notifications| RabbitMQ Event Consumers | System Background | Lag ≤ 5s | Lag ≤ 10s | 0 DLQ |


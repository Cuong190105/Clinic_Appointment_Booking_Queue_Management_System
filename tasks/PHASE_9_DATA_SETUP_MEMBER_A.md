# Đặc tả Thiết lập và Cô lập Dữ liệu Kiểm thử (Phase 9) — Thành viên A

**Tài liệu tham chiếu:** [`tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md)  
**Phiên bản:** 1.0.0  
**Tác giả:** Thành viên A  
**Ngày lập:** 04/11/2026  

---

## 1. Mục tiêu và Chiến lược Cô lập (Data Isolation Strategy)

Để đảm bảo các đợt chạy kiểm thử hiệu năng (Smoke, Baseline, Load, Contention, Stress, Soak, Degradation) độc lập, có thể tái lập và không làm ô nhiễm hoặc xung đột với dữ liệu hiện có trong hệ thống, Thành viên A thiết lập chiến lược: **Data-Level Namespace Isolation** kết hợp Synthetic Test Dataset.

### Các nguyên tắc cách ly:
1. **Không can thiệp dữ liệu thật (No Real PHI / PII):** Tuyệt đối không sử dụng danh tính, số điện thoại, CMND/CCCD hoặc email của người dùng thật. Toàn bộ dữ liệu là tổng hợp (Synthetic Data).
2. **Namespace bằng `testRunId`:** Mỗi phiên chạy được gán một tiền tố duy nhất:
   - Định dạng: `PH9-<RUN_TIMESTAMP>` (Ví dụ: `PH9-20261104-120000`).
   - Mọi entity phát sinh (Lịch hẹn, Vé khám, Hồ sơ bệnh án, Idempotency-Key) đều mang tag hoặc correlation chứa namespace này.
3. **Dedicated Resource Slates:**
   - **Contention Slot:** Dành riêng 1 ca trực (WorkSchedule) có `RecommendedCapacity = 1` để đo đạc tranh chấp tuyệt đối.
   - **Throughput Slots:** Dành riêng các ca trực có `RecommendedCapacity = 100` để đo thông lượng ghi không bị nghẽn do business capacity.
4. **Mock Provider Enforced:**
   - Notification Service bắt buộc chạy cờ `LoggingEmailSender` và mock SMS; không kết nối tới SMTP hay Twilio/SMS gateway thật.

---

## 2. Danh mục Tài khoản & Phân quyền Thử nghiệm (Seed Accounts)

Hệ thống đã có sẵn tập tài khoản hợp lệ trong `ClinicIdentityDB` với mật khẩu thống nhất: `Password123!`.

### 2.1. Nhóm Bệnh nhân (Patients) — 50 tài khoản
- **Usernames:** `patient1`, `patient2`, ..., `patient50`
- **Mật khẩu:** `Password123!`
- **Role:** `Patient`
- **Mục đích:** Xoay vòng (Round-robin) giữa các Virtual Users (VUs) trong kịch bản k6 Load & Contention nhằm:
  - Phân tán quota Rate Limiter của Gateway (100 req/min/subject).
  - Đảm bảo tính xác thực người dùng thật (Không spam dưới dạng anonymous).

### 2.2. Nhóm Bác sĩ (Doctors) — 5 tài khoản
- **Usernames:** `doctor1` .. `doctor5`
- **Mật khẩu:** `Password123!`
- **Role:** `Doctor`, `Patient`
- **Doctor IDs tham chiếu:**
  - `doctor1`: `a0000004-0000-0000-0000-000000000001` (BS. Nguyễn Văn An - Tim mạch)
  - `doctor2`: `a0000004-0000-0000-0000-000000000002` (BS. Trần Thị Bích - Da liễu)
  - `doctor3`: `a0000004-0000-0000-0000-000000000003` (BS. Lê Minh Châu - Tim mạch)
- **Mục đích:** Thực thi tác vụ gọi số (`/queue/call-next`), khám (`/start-exam`), hoàn tất (`/complete-exam`) và lưu hồ sơ bệnh án (`/medical-reports`).

### 2.3. Nhóm Lễ tân (Receptionists) — 5 tài khoản
- **Usernames:** `receptionist1` .. `receptionist5`
- **Mật khẩu:** `Password123!`
- **Role:** `Receptionist`, `Patient`
- **Quyền hạn sở hữu:** `queue.check-in`, `appointment.confirm`, `appointment.create`, `appointment.view`.
- **Mục đích:** Tiếp nhận bệnh nhân tại quầy, thực hiện check-in và kích hoạt cấp vé khám số thứ tự.

### 2.4. Nhóm Quản trị viên (Admins) — 1 tài khoản
- **Username:** `admin1`
- **Mật khẩu:** `Password123!`
- **Role:** `Admin`, `Patient`
- **Mục đích:** Xem thống kê dashboard, kiểm tra dead-letter queue, replay delivery nếu cần.

---

## 3. Cấu trúc Lịch trực Thử nghiệm (Test WorkSchedules)

Tập script seeding tạo sẵn 2 lịch trực chuyên dụng cho kịch bản Phase 9 trong `ClinicSchedulingDB.WorkSchedules`:

```
+--------------------------------------+--------------------------------------+------------+------------+----------+----------+-------------------------------------+
| Id                                   | DoctorId                             | Date       | ShiftStart | ShiftEnd | Capacity | Mục đích thử nghiệm                 |
+--------------------------------------+--------------------------------------+------------+------------+----------+----------+-------------------------------------+
| 90000000-0000-0000-0000-000000000001 | a0000004-0000-0000-0000-000000000001 | 2026-11-20 | 08:00:00   | 12:00:00 | 1        | Contention Slot (Duy nhất 1 winner) |
| 90000000-0000-0000-0000-000000000002 | a0000004-0000-0000-0000-000000000001 | 2026-11-20 | 13:00:00   | 17:00:00 | 100      | High-Throughput Booking Slot        |
+--------------------------------------+--------------------------------------+------------+------------+----------+----------+-------------------------------------+
```

---

## 4. Kịch bản Dữ liệu Seeding Tự động (`seed-phase9-workload-data.ps1`)

Script PowerShell [`scripts/seed-phase9-workload-data.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/seed-phase9-workload-data.ps1) đảm nhận:
1. Kiểm tra kết nối tới SQL Server (`clinic_sql_server`).
2. Đồng bộ mật khẩu `Password123!` cho toàn bộ seed accounts (`admin1`, `receptionist1..5`, `doctor1..5`, `patient1..50`).
3. Khởi tạo 2 WorkSchedules test chuyên dụng nêu trên nếu chưa tồn tại.
4. Tạo sẵn 10 cuộc hẹn mẫu trạng thái `Confirmed` sẵn sàng cho kịch bản Check-in & Queue ticket generation.
5. In ra thông số chi tiết để k6 runner nạp cấu hình tự động.

---

## 5. Quy trình Thu dọn và Đối soát (`cleanup-phase9-workload-data.ps1`)

Script PowerShell [`scripts/cleanup-phase9-workload-data.ps1`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/cleanup-phase9-workload-data.ps1) thực hiện thu dọn an toàn:
1. Nhận tham số `-TestRunId <string>` (hoặc mặc định dọn dẹp các bản ghi mang tiền tố `PH9-` hoặc ID test `90000000-...`).
2. Xóa các bản ghi phát sinh trong `ClinicAppointmentDB.Appointments` có `Reason LIKE '%PH9%'`.
3. Xóa các bản ghi phát sinh trong `ClinicQueueDB.QueueTickets` theo danh sách cuộc hẹn test.
4. Xóa các bản ghi phát sinh trong `ClinicMedicalRecordsDB.MedicalReports` theo cuộc hẹn test.
5. Đặt lại trạng thái `SlotReservations` về trống cho các lịch trực test.
6. **Tuyệt đối không đụng chạm đến dữ liệu phát triển nền (Baseline data)** được tạo từ Phase 0 - Phase 8.

---

## 6. Quy trình Đối soát Dữ liệu (Data Reconciliation Protocol)

Sau khi hoàn tất mỗi lượt tải, thực hiện đối soát theo 4 phương trình cân bằng:
$$\text{Total HTTP 201 Created} = \Delta \text{Appointments} (\text{Status} \in \{\text{Scheduled, Confirmed}\})$$
$$\text{Total Check-in 204 NoContent} = \Delta \text{QueueTickets} (\text{Distinct QueueNumber})$$
$$\text{Overbooking Violations} = \sum \max(0, \text{BookedCount} - \text{RecommendedCapacity}) \equiv 0$$
$$\text{Total Unhandled DLQs} \equiv 0$$


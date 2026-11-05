# Đặc tả Truy vấn Kiểm tra Bất biến Nghiệp vụ (Phase 9) — Thành viên A

**Tài liệu tham chiếu:** [`tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md`](file:///d:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tasks/PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_A.md)  
**Phiên bản:** 1.0.0  
**Tác giả:** Thành viên A  
**Ngày lập:** 04/11/2026  

---

## 1. Mục đích và Yêu cầu Nghiệm thu Bất biến (Business Invariants)

Tài liệu này cung cấp toàn bộ các truy vấn SQL trực tiếp trên SQL Server (`clinic_sql_server`) và lệnh CLI trên RabbitMQ Management API (`clinic_appointment_booking_queue_management_system-rabbitmq-1`) nhằm kiểm tra độc lập các bất biến nghiệp vụ sau mỗi đợt chạy tải.

> [!CRITICAL]
> **Quy định nghiêm ngặt của Phase 9:** Invariant checker phải đọc nguồn sự thật từ API, Cơ sở dữ liệu và Broker thật. Tuyệt đối không sử dụng bộ nhớ process nội bộ (`Interlocked`, `ConcurrentDictionary`) để thay thế bằng chứng đối soát hệ thống phân tán.

---

## 2. Invariant 1: Không Overbooking (Zero Overbooking)

### 2.1. Bản chất nghiệp vụ:
Tổng số lượng cuộc hẹn hợp lệ (`Status IN ('Scheduled', 'Confirmed', 'CheckedIn', 'InProgress', 'Completed')`) cho một ca trực không bao giờ được vượt quá sức chứa khuyến nghị (`RecommendedCapacity`) của ca trực đó. Đặc biệt đối với ca trực Contention (`RecommendedCapacity = 1`), số lượng winner tối đa là 1.

### 2.2. Câu truy vấn kiểm tra trên SQL Server:
```sql
-- Kiểm tra xem có ca trực nào bị Overbooking trên toàn hệ thống hay không
-- Kết quả mong đợi: 0 ROWS (Tất cả ca trực đều thỏa OverbookingCount = 0)
USE ClinicAppointmentDB;

SELECT 
    a.WorkScheduleId,
    a.ScheduleDate,
    a.TimeSlot,
    COUNT(a.Id) AS TotalBooked,
    s.RecommendedCapacity,
    (COUNT(a.Id) - s.RecommendedCapacity) AS OverbookingViolationCount
FROM Appointments a
INNER JOIN ClinicSchedulingDB.dbo.WorkSchedules s ON a.WorkScheduleId = s.Id
WHERE a.IsDeleted = 0 
  AND a.Status NOT IN ('Cancelled', 'NoShow')
GROUP BY a.WorkScheduleId, a.ScheduleDate, a.TimeSlot, s.RecommendedCapacity
HAVING COUNT(a.Id) > s.RecommendedCapacity;
```

### 2.3. Lệnh thực thi nhanh qua Docker:
```powershell
docker exec clinic_sql_server /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "ClinicDev!2026StrongPw" -C -d ClinicAppointmentDB -Q "
SELECT a.WorkScheduleId, COUNT(a.Id) AS TotalBooked, s.RecommendedCapacity
FROM Appointments a
INNER JOIN ClinicSchedulingDB.dbo.WorkSchedules s ON a.WorkScheduleId = s.Id
WHERE a.IsDeleted = 0 AND a.Status NOT IN ('Cancelled', 'NoShow')
GROUP BY a.WorkScheduleId, s.RecommendedCapacity
HAVING COUNT(a.Id) > s.RecommendedCapacity;"
```
- **Tiêu chuẩn đạt:** Kết quả trả về `(0 rows affected)`.

---

## 3. Invariant 2: Không cấp trùng Vé khám hoặc Số thứ tự (Zero Duplicate Tickets)

### 3.1. Bản chất nghiệp vụ:
1. Mỗi cuộc hẹn (`AppointmentId`) chỉ được sinh tối đa duy nhất 1 vé khám trong ngày.
2. Với cùng một bác sĩ (`DoctorId`) và trong cùng một ngày (`QueueDate`), số thứ tự khám (`QueueNumber`) phải là duy nhất và tăng tuần tự.

### 3.2. Câu truy vấn kiểm tra trên SQL Server:
```sql
USE ClinicQueueDB;

-- Kiểm tra 1: Có AppointmentId nào có nhiều hơn 1 vé khám?
-- Kết quả mong đợi: 0 ROWS
SELECT 
    AppointmentId, 
    COUNT(Id) AS DuplicateTicketCount
FROM QueueTickets
WHERE IsDeleted = 0
GROUP BY AppointmentId
HAVING COUNT(Id) > 1;

-- Kiểm tra 2: Có số thứ tự (QueueNumber) nào bị cấp trùng cho cùng Bác sĩ trong ngày?
-- Kết quả mong đợi: 0 ROWS
SELECT 
    DoctorId, 
    QueueDate, 
    QueueNumber, 
    COUNT(Id) AS DuplicateNumberCount
FROM QueueTickets
WHERE IsDeleted = 0
GROUP BY DoctorId, QueueDate, QueueNumber
HAVING COUNT(Id) > 1;

-- Kiểm tra 3: Đối chiếu số lớn nhất trong QueueCounters với số vé thực tế trong QueueTickets
-- Kết quả mong đợi: MaxTicketNumber <= LastNumber
SELECT 
    qc.DoctorId, 
    qc.QueueDate, 
    qc.LastNumber AS CounterValue, 
    MAX(qt.QueueNumber) AS MaxTicketNumber
FROM QueueCounters qc
LEFT JOIN QueueTickets qt ON qc.DoctorId = qt.DoctorId AND qc.QueueDate = qt.QueueDate AND qt.IsDeleted = 0
GROUP BY qc.DoctorId, qc.QueueDate, qc.LastNumber
HAVING MAX(qt.QueueNumber) > qc.LastNumber;
```

### 3.3. Lệnh thực thi nhanh qua Docker:
```powershell
docker exec clinic_sql_server /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "ClinicDev!2026StrongPw" -C -d ClinicQueueDB -Q "
SELECT AppointmentId, COUNT(Id) AS DupTickets FROM QueueTickets WHERE IsDeleted = 0 GROUP BY AppointmentId HAVING COUNT(Id) > 1;
SELECT DoctorId, QueueDate, QueueNumber, COUNT(Id) AS DupNumbers FROM QueueTickets WHERE IsDeleted = 0 GROUP BY DoctorId, QueueDate, QueueNumber HAVING COUNT(Id) > 1;"
```
- **Tiêu chuẩn đạt:** Cả 2 truy vấn đều trả về `(0 rows affected)`.

---

## 4. Invariant 3: Tính Nhất quán của Idempotency (Idempotency Invariance)

### 4.1. Bản chất nghiệp vụ:
Khi một client gửi lặp lại (retry) một command có cùng `Idempotency-Key` (do rớt mạng client, gateway timeout ảo, hoặc retry tự động):
1. Hệ thống không tạo thêm tài nguyên mới (chính xác 1 cuộc hẹn duy nhất).
2. Trả về kết quả ban đầu một cách an toàn.

### 4.2. Câu truy vấn kiểm tra trên SQL Server:
```sql
USE ClinicAppointmentDB;

-- Kiểm tra: Có Idempotency-Key nào tạo ra > 1 bản ghi cuộc hẹn?
-- Kết quả mong đợi: 0 ROWS
SELECT 
    IdempotencyKey, 
    COUNT(Id) AS DuplicateRecordCount
FROM Appointments
WHERE IdempotencyKey IS NOT NULL AND IsDeleted = 0
GROUP BY IdempotencyKey
HAVING COUNT(Id) > 1;
```

---

## 5. Invariant 4: Tính Nhất quán Outbox / Inbox (Zero Stuck Outbox)

### 5.1. Bản chất nghiệp vụ:
Các bảng Outbox của các domain services (`ClinicAppointmentDB.OutboxMessages`, `ClinicSchedulingDB.OutboxMessages`, `ClinicQueueDB.OutboxMessages`) sau khi tải dừng phải được background publisher đẩy hết sang RabbitMQ, không để tồn đọng thông điệp kẹt (`Status = 'Pending'` hoặc `'Failed'` vĩnh viễn).

### 5.2. Câu truy vấn kiểm tra trên SQL Server:
```sql
-- Kiểm tra Outbox tồn đọng trong ClinicAppointmentDB
USE ClinicAppointmentDB;
SELECT Status, COUNT(*) AS MessageCount 
FROM OutboxMessages 
GROUP BY Status;

-- Kiểm tra Outbox tồn đọng trong ClinicSchedulingDB
USE ClinicSchedulingDB;
SELECT Status, COUNT(*) AS MessageCount 
FROM OutboxMessages 
GROUP BY Status;

-- Kiểm tra Outbox tồn đọng trong ClinicQueueDB
USE ClinicQueueDB;
SELECT Status, COUNT(*) AS MessageCount 
FROM OutboxMessages 
GROUP BY Status;
```
- **Tiêu chuẩn đạt:** Trạng thái `Published` chiếm tuyệt đại đa số; `Pending` hoặc `Failed` trở về 0 sau cửa sổ ổn định (settle window 10s - 30s).

---

## 6. Invariant 5: Không Thất thoát Sự kiện & Zero DLQ (Zero Dead-Letter Queues)

### 6.1. Bản chất nghiệp vụ:
Mọi Dead-Letter Queue của các dịch vụ trong hệ thống phải có số lượng message bằng 0 sau khi hoàn tất các lượt kiểm thử. Mọi event sinh ra phải được tiêu thụ thành công hoặc được retry có kiểm soát.

### 6.2. Lệnh kiểm tra trực tiếp qua RabbitMQ Management:
```powershell
docker exec clinic_appointment_booking_queue_management_system-rabbitmq-1 rabbitmqadmin -u clinic -p clinic-dev-password list queues name messages | Select-String -Pattern 'dlq|dead'
```
- **Danh sách các hàng đợi DLQ bắt buộc bằng 0:**
  1. `q.appointment-service.dlq` = 0
  2. `q.medical-records-service.dlq` = 0
  3. `q.notification-service.dlq` = 0
  4. `clinic.notification.events.dlq` = 0
  5. `q.projection-service.dlq` = 0
  6. `q.queue-service.dlq` = 0
  7. `q.queue-service.outbox-dlq` = 0
  8. `q.scheduling-service.outbox-dlq` = 0
  9. `q.outbox.publish.dlq` = 0
  10. `clinic.medical-records.events.dead` = 0

---

## 7. Tổng kết Tiêu chí Đạt (Invariant Acceptance Gate)

| Bất biến | Chỉ số đánh giá | Ngưỡng Pass bắt buộc |
|---|---|---|
| **Zero Overbooking** | Số ca trực có `Booked > RecommendedCapacity` | **= 0** |
| **Zero Duplicate Tickets** | Số vé trùng `AppointmentId` hoặc `(Doctor, Date, Number)` | **= 0** |
| **Idempotency** | Số bản ghi trùng trên cùng một `Idempotency-Key` | **= 0** |
| **Outbox Health** | Số message Outbox bị kẹt `Failed`/`Pending` sau settle window | **= 0** |
| **Zero DLQ** | Tổng số message nằm tại 10 hàng đợi Dead-Letter Queue | **= 0** |


# BÁO CÁO RÀ SOÁT & KIỂM TRA ĐỐI CHIẾU KIẾN TRÚC VÀ THIẾT KẾ
## CÁC GIAI ĐOẠN 2, 3, 4 (STAFF/SCHEDULING, APPOINTMENT, QUEUE)

> **Mục đích tài liệu:** Kiểm tra toàn diện chất lượng triển khai, tính toàn vẹn kiến trúc, cơ chế hướng sự kiện, database boundary, và các tiêu chí thiết kế đặt ra trong tài liệu phân công nhiệm vụ (`TASK_MEMBER_A.md`, `TASK_MEMBER_B.md`, và các execution plan liên quan).
> **Nguyên tắc rà soát:** Không thực hiện thay đổi mã nguồn trong quá trình kiểm tra; kiểm tra lần lượt từng giai đoạn và ghi nhận báo cáo độc lập.

---

## PHẦN 1: BÁO CÁO RÀ SOÁT GIAI ĐOẠN 2 — STAFF & SCHEDULING SERVICE

### 1. Phạm vi & Tiêu chí Thiết kế theo Yêu cầu
Theo tài liệu [`TASK_MEMBER_A.md`](../tasks/TASK_MEMBER_A.md) và [`PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md`](../tasks/PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md):
1. **Single Source of Truth (SSOT) & Database Boundary:**
   - `Clinic.Scheduling.Service` (`ClinicSchedulingDB`) sở hữu 100% dữ liệu và nghiệp vụ của: `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`, `SlotReservation`, `OutboxMessage`.
   - `ClinicDB` (Monolith) phải drop hoàn toàn 6 bảng trên; không còn Foreign Key hoặc navigation property từ `ClinicDB` sang Scheduling.
   - Các bảng `Appointments`, `MedicalReports`, `Notifications` chỉ lưu ID và các cột snapshot/denormalized (`WorkScheduleId`, `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate`).
2. **Nghiệp vụ Scheduling & Dual-Constraint Capacity Rule:**
   - Cung cấp API `GetAvailability` với Dual-Constraint capacity rule (`RecommendedCapacity` + unique 15-minute intervals), chặn slot quá khứ / cận giờ (< 30 phút).
   - Bộ API Reservation (`POST /schedules/{id}/reservations`, `/confirm`, `/release`) có timeout 10 phút, hỗ trợ `Idempotency-Key`, kiểm tra trùng lặp và background service dọn dẹp slot hết hạn (`ReservationExpirationWorker`).
   - Duyệt ca trực `ShiftRequest.Approve()` atomic trong local transaction kèm conflict detection trùng lịch.
3. **Cơ chế Event-Driven & Outbox:**
   - Phát các sự kiện `WorkScheduleCreated` (`schedule.created`), `WorkScheduleUpdated` (`schedule.updated`), `WorkScheduleCancelled` (`schedule.cancelled`) qua Transactional Outbox vào bảng `OutboxMessages`.
   - Phải có background worker `SchedulingOutboxPublisher` để quét và phát tin nhắn sang RabbitMQ exchange `clinic.events`.
   - Tiêu thụ sự kiện `AppointmentCancelled` và `UserProfileUpdated` để đồng bộ.

---

### 2. Hiện trạng triển khai chi tiết & Đối soát mã nguồn

#### 2.1. Phía Cơ sở Dữ liệu & Clean Architecture
- **`ClinicSchedulingDB`**:
  - Đã có đầy đủ các entity và DbContext độc lập tại `Clinic.Scheduling.Service`.
  - Toàn bộ dữ liệu nguồn từ `ClinicDB` đã được import và đối soát bằng các script idempotent (`import-from-clinicdb.sql`, `verify-scheduling-import.sql`) với `MismatchCount = 0`.
- **`ClinicDB` (Monolith)**:
  - Migration `20261024055019_RemoveLegacyStaffAndSchedulingSchema` đã drop toàn bộ 6 bảng legacy: `ShiftRequests`, `WorkSchedules`, `WorkHistories`, `Doctors`, `Employees`, `Specialties`.
  - Không còn bất kỳ navigation property nào sang Staff/Scheduling trong `Clinic.Domain` (`Person.cs` và `Appointment.cs` đã được dọn sạch).
  - Không còn bất kỳ repository hay MediatR handler nào của Staff/Scheduling trong `Clinic.Application` và `Clinic.Infrastructure.Sqlserver`.

#### 2.2. Phía API Endpoints & Gateway Orchestration
- Toàn bộ route `/employees/**`, `/doctors/**`, `/specialties/**`, `/shifts/**` được điều phối qua `SchedulingProxyMiddleware` và các Controller điều phối (`EmployeesController`, `DoctorsController`, `SlotsController`, `ReservationsController`).
- Quá trình tạo mới và cập nhật hồ sơ (`POST /employees`, `POST /doctors`, `PUT /employees/{id}`, `PUT /doctors/{id}`) thực hiện orchestration chuẩn xác giữa `Clinic.Identity.Service` (sở hữu account, credentials, roles, person gốc) và `Clinic.Scheduling.Service` (sở hữu hồ sơ nghề nghiệp và lịch trực).
- Endpoint `GET /admin/doctors/brief` đã chuyển sang gọi trực tiếp `Clinic.Scheduling.Service` (`GET /doctors/brief`), trả về `doctorId`, `employeeId`, `fullName`, `email` (từ Employee), loại bỏ hoàn toàn phụ thuộc vào Identity Service.
- Endpoint `GET /doctors/shifts` thực hiện query từ `db.Doctors` kết hợp left-join ca làm việc, đảm bảo bác sĩ chưa có ca trong tuần vẫn hiển thị trên bảng phân ca của Admin.

#### 2.3. Rà soát 8 điểm xem xét (Audit Issues) đã giải quyết
1. **Lỗi Crash & RecommendedCapacity trong DoctorScheduleView:** Đã bổ sung fallback an toàn `startTime`/`shiftStart` và chuẩn hóa hiển thị `RecommendedCapacity`.
2. **Lỗi HTTP 400 khi Admin cập nhật Doctor Profile & thiếu DTO fields:** Đã bổ sung `SpecialtyId`, `DateOfBirth`, `Address`, `HireDate` vào `DoctorDto` & `UpdateDoctorRequest`, cấu hình `FlexibleGenderConverter`.
3. **Lỗi HTTP 400 khi Toggle trạng thái Doctor / Specialty:** Đã hỗ trợ linh hoạt nhận `JsonElement` (number, boolean, string enum).
4. **Endpoint `GET /admin/doctors/brief` phụ thuộc Identity Service:** Đã chuyển sang `Clinic.Scheduling.Service`, thay thế trường `username` bằng `email` từ `Employee` đồng bộ xuyên suốt từ backend tới frontend.
5. **Lỗi cú pháp Null Coalescing `s.RecommendedCapacity` ở frontend:** Đã sửa thành `s.recommendedCapacity ?? s.RecommendedCapacity ?? 20`.
6. **Lỗi mất trường `userId` trong response `GET /employees`:** Đã loại bỏ lệnh `employee.Remove("userId")` trong middleware để giữ nguyên `userId` cho `users.api.ts`.
7. **Phân trang `GET /specialties`:** Đã hỗ trợ cả `page` lẫn `pageNumber` và giá trị mặc định an toàn.
8. **Thiếu Background Worker `SchedulingOutboxPublisher`:** Đã tạo worker `SchedulingOutboxPublisher.cs` kế thừa `BackgroundService`, khai báo topic exchange `clinic.events`, dead-letter exchange/queue, chạy thử thực tế và publish thành công toàn bộ 122 outbox message đang tồn đọng sang RabbitMQ.

---

### 3. Đánh giá Tiêu chuẩn Kiểm thử & Build của Giai đoạn 2
- **Unit & Integration Tests**:
  - `Clinic.Scheduling.Service.Tests`: **37/37 passed** (gồm test Availability, Concurrency, Capacity rule, Slot expiration, Outbox Publisher).
  - Toàn bộ solution: **273/273 tests passed (0 failed)**.
- **Frontend Type Check & Build**:
  - `npm run type-check` (`vue-tsc --build`): **0 errors**.
  - `npm run build`: **Build production thành công**.
- **Docker Stack Runtime**:
  - Container `clinic_scheduling_service` chạy ổn định, kết nối thông suốt với `clinic_sql_server`, `rabbitmq`, và `clinic_identity_service`.
  - Database `ClinicSchedulingDB` ghi nhận **0 pending message** trong bảng `OutboxMessages` (toàn bộ đã `Sent`).

---

### 4. Kết luận Giai đoạn 2
> [!NOTE]
> **ĐÁNH GIÁ GIAI ĐOẠN 2: ĐẠT 100% THEO THIẾT KẾ VÀ KIẾN TRÚC.**
> Không còn lỗi tồn đọng, không còn điểm vi phạm Single Source of Truth hay database boundary. Mã nguồn sẵn sàng chuyển sang rà soát Giai đoạn 3.

---



## PHẦN 2: BÁO CÁO RÀ SOÁT GIAI ĐOẠN 3 — APPOINTMENT SERVICE

### 1. Phạm vi & Tiêu chí Thiết kế theo Yêu cầu
Theo tài liệu [`TASK_MEMBER_A.md`](../tasks/TASK_MEMBER_A.md) và [`PHASE_3_APPOINTMENT_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](../tasks/PHASE_3_APPOINTMENT_SERVICE_EXECUTION_PLAN_MEMBER_A.md):
1. **Single Source of Truth (SSOT) & Database Boundary:**
   - `Clinic.Appointment.Service` sở hữu cơ sở dữ liệu riêng: **`ClinicAppointmentDB`**.
   - Sở hữu các thực thể: `Appointment` (Aggregate Root), `AppointmentSnapshot` (1-N Audit Changelog), `PatientProjection` (Bản chiếu bệnh nhân tối thiểu), và `OutboxMessage`.
   - Bảng `Appointments` lưu trữ mô hình "Self-Contained Denormalized Read Model" gồm đầy đủ thông tin Bác sĩ (`DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate`) và Bệnh nhân (`PatientName`, `PatientPhone`, `PatientGender`, `PatientDateOfBirth`), không cần join phân tán khi truy vấn.
   - Gỡ bỏ hoàn toàn khóa ngoại từ `QueueTickets` và `MedicalReports` sang `Appointments` trong `ClinicDB` (chuyển sang loose UUID reference).
2. **Saga Orchestration & Idempotency:**
   - **Booking Saga**: `ReserveSlot` (tại Scheduling) $\to$ `CreateAppointment` (tại Appointment) $\to$ `ConfirmReservation` (tại Scheduling). Hỗ trợ cơ chế bồi hoàn tự động (`ReleaseReservation`) nếu có bất kỳ bước nào thất bại.
   - **Idempotency**: Hỗ trợ header `Idempotency-Key` với Filtered Unique Index trên database để đảm bảo an toàn khi client retry.
   - **Reschedule Saga (2 pha)**: Giữ slot mới $\to$ Ghi snapshot & cập nhật lịch $\to$ Xác nhận slot mới $\to$ Giải phóng slot cũ và phát sự kiện `AppointmentCancelled`.
   - **Walk-in Flow**: Phân định rõ ràng giữa Online Booking (ràng buộc capacity) và Walk-in tại quầy (trực tiếp `Confirmed` hoặc `CheckedIn`, tự động phân bổ bác sĩ ít tải nhất).
3. **Cơ chế Hướng sự kiện (Event-Driven Messaging):**
   - **Check-in**: Endpoint `POST /appointments/{id}/check-in` cập nhật trạng thái `CheckedIn` và phát sự kiện `AppointmentCheckedIn` qua Outbox để Queue Service tiếp nhận cấp số.
   - **Đồng bộ Hoàn tất khám**: `AppointmentEventConsumer` lắng nghe sự kiện `ExamCompleted` (`queue.exam-completed`) từ RabbitMQ để tự động chuyển `Appointment.Status = Completed`.
   - **Đồng bộ Hồ sơ**: Lắng nghe `UserProfileUpdated` để cập nhật bảng `PatientProjections`.

---

### 2. Hiện trạng triển khai chi tiết & Đối soát mã nguồn

#### 2.1. Phía Cơ sở Dữ liệu & Clean Architecture
- **`ClinicAppointmentDB`**:
  - Sở hữu 4 bảng độc lập: `Appointments`, `AppointmentSnapshots`, `PatientProjections`, `OutboxMessages`.
  - Index chống Overbooking: Filtered Unique Index trên `(DoctorId, ScheduleDate, TimeSlot)` với điều kiện `[Status] <> 'Cancelled' AND [IsDeleted] = 0 AND [IsWalkIn] = 0`, ngăn chặn hoàn toàn việc đặt trùng ở tầng cơ sở dữ liệu.
  - Dữ liệu lịch sử từ `ClinicDB` đã được di chuyển qua script idempotent (`import-from-clinicdb.sql`, `verify-appointment-import.sql`) với `MismatchCount = 0` (750 Appointments, 21 Snapshots, 59 Projections khớp 100%).
- **`ClinicDB` (Monolith)**:
  - Migration `20261023013218_DecoupleAppointmentForeignKeys` đã gỡ bỏ thành công 2 foreign key: `FK_QueueTickets_Appointments_AppointmentId` và `FK_MedicalReports_Appointments_AppointmentId`.
  - Xác nhận trong SQL Server catalog: Không còn bất kỳ FK nào từ các bảng khác trỏ tới `Appointments`.

#### 2.2. Phía API Endpoints & Gateway Orchestration
- **Gateway Cutover**:
  - `AppointmentsController`, `MeAppointmentsController`, `ReceptionistController`, và `AdminController` trong `Clinic.API` chuyển tiếp 100% request nghiệp vụ lịch hẹn sang `Clinic.Appointment.Service` qua `AppointmentServiceClient`.
  - Bảo lưu chính sách xác thực và phân quyền (JWT Claims, `StaffOnly`, `DoctorOnly`, `AdminOnly`) tại cả hai tầng.
- **Workflow Nghiệp vụ Lễ tân & Bác sĩ**:
  - Đầy đủ API: `GET /receptionist/working-doctors`, `POST /receptionist/appointments/walk-in` (hỗ trợ phân bổ bác sĩ ít ca khám nhất, tự động check-in và gắn cờ ưu tiên `isUrgent`), `POST /appointments/{id}/confirm`, `POST /appointments/{id}/check-in`.
  - Phục vụ đầy đủ giao diện đặt khám 4 bước của Lễ tân và tra cứu bệnh án của Bác sĩ.

#### 2.3. Transactional Outbox & Background Workers
- **`AppointmentOutboxPublisher`**:
  - Chạy nền chu kỳ 1 giây, quét bảng `OutboxMessages`, phát các sự kiện `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn` sang RabbitMQ exchange `clinic.events`.
  - Kiểm tra thực tế trong database `ClinicAppointmentDB`: **49/49 messages đã `Sent` (0 pending)**.
- **`AppointmentEventConsumer`**:
  - Đăng ký hàng đợi `clinic.appointment.events`, bind với các routing key `queue.exam-completed` và `identity.user-profile-updated`.
  - Xử lý trơn tru cả envelope format lẫn raw event payload.

---

### 3. Đánh giá Tiêu chuẩn Kiểm thử & Build của Giai đoạn 3
- **Unit & Integration Tests**:
  - `Clinic.Appointment.Service.Tests`: **32/32 passed** (bao gồm Booking Saga, Reschedule Saga, Idempotency retry, Cancel compensation, Summary, Changelog).
  - Toàn bộ solution: **273/273 tests passed (0 failed)**.
- **Frontend Type Check & Build**:
  - `npm run type-check` (`vue-tsc --build`): **0 errors**.
  - `npm run build`: **Build production thành công**.
- **Docker Stack Runtime**:
  - Container `clinic_appointment_service` chạy ổn định tại cổng `5003`, health check `/readyz` và `/healthz` trả về HTTP `200`.

---

### 4. Kết luận Giai đoạn 3
> [!NOTE]
> **ĐÁNH GIÁ BAN ĐẦU GIAI ĐOẠN 3 (2026-10-24): ĐẠT THEO PHẠM VI CUTOVER LỊCH HẸN.**
> Toàn bộ luồng đặt lịch, đổi lịch, hủy lịch, check-in, bồi hoàn saga và đồng bộ sự kiện hoàn tất khám hoạt động chuẩn xác theo thiết kế. Mã nguồn sẵn sàng chuyển sang rà soát Giai đoạn 4.

---

### Cập nhật hoàn tất Phase 3 (2026-10-25)

Sau khi Queue Service được tách, rà soát lại phát hiện các API tra cứu lịch theo patient ID, Admin statistics/dashboard, dual-write ClinicDB và coupling của MedicalReports chưa được xử lý trong báo cáo ban đầu. Các điểm này đã được hoàn tất:

- Patient-specific appointments, Admin statistics và dashboard metrics hiện đọc từ Appointment/Queue/Scheduling owner services; Clinic API chỉ proxy/tổng hợp.
- MedicalReports vẫn là module Clinic, nhưng lưu doctor/date snapshot độc lập; xác thực appointment và kiểm tra/completion queue đi qua service API. Không còn EF navigation hoặc repository query sang Appointment/Queue legacy.
- Đã gỡ dual-write, Appointment/Queue legacy handlers, repositories, entities, configurations và DbSets khỏi Monolith; migration `20261025081432_CompleteAppointmentServiceCutover` backfill snapshot rồi xóa Clinic Appointment/Queue tables.
- Build solution pass; 9 test project pass; Docker build và service health/smoke pass. Chi tiết cùng giới hạn acceptance UI có xác thực nằm trong [Phase 3 completion plan](../tasks/PHASE_3_APPOINTMENT_SERVICE_EXECUTION_PLAN_MEMBER_A.md), mục 9.3–9.4.

**Trạng thái Phase 3 hiện tại: hoàn tất.** MedicalReports Service độc lập vẫn nằm ngoài phạm vi này và có thể được chuyển ở giai đoạn sau.

---


## PHẦN 3: BÁO CÁO RÀ SOÁT GIAI ĐOẠN 4 — QUEUE SERVICE

> **Cập nhật 2026-10-25:** Mục này thay thế các kết luận cũ về Queue trong Phần 4 và các số liệu đếm database có ngày 2026-10-24. ClinicDB/ClinicQueueDB là mock data; các chênh lệch trước đây không được dùng làm căn cứ tự sửa/xóa.

### 1. Phạm vi & Tiêu chí Thiết kế theo Yêu cầu
Theo tài liệu [`TASK_MEMBER_A.md`](../tasks/TASK_MEMBER_A.md), [`TASK_MEMBER_B.md`](../tasks/TASK_MEMBER_B.md) và [`PHASE_4_QUEUE_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](../tasks/PHASE_4_QUEUE_SERVICE_EXECUTION_PLAN_MEMBER_A.md):
1. **Single Source of Truth (SSOT) & Database Boundary:**
   - `Clinic.Queue.Service` sở hữu cơ sở dữ liệu riêng: **`ClinicQueueDB`**.
   - Sở hữu các thực thể: `QueueTicket`, `QueueCounter`, `InboxMessage`, `OutboxMessage`.
   - `QueueTicket` lưu trữ thông tin denormalized gồm `AppointmentId`, `PatientId`, `DoctorId`, `QueueDate`, `QueueNumber`, `PatientName`, `DoctorName`, `Priority`, `Status`, `CheckInTime`, `CalledAt`.
   - Không duy trì FK hoặc navigation runtime sang `ClinicDB`, `ClinicAppointmentDB`, `ClinicSchedulingDB` hay `ClinicIdentityDB`.
2. **Quy tắc Cấp số & Toàn vẹn Dữ liệu:**
   - **Số thứ tự duy nhất**: Ràng buộc Unique Constraint trên `(DoctorId, QueueDate, QueueNumber)`. Không tái sử dụng số đã cấp trong ngày kể cả ticket bị hủy/bỏ qua.
   - **Bộ đếm số**: `QueueCounter` có khóa `(DoctorId, QueueDate)`, tăng nguyên tử (atomic increment) trong local transaction của SQL Server.
   - **Xác định ngày (`QueueDate`)**: Tính toán chính xác theo IANA timezone cấu hình cho phòng khám (`Asia/Ho_Chi_Minh`), không dùng `.Date` trên UTC để tránh lệch ngày quanh ranh giới nửa đêm.
3. **Cơ chế Tiêu thụ & Phát Sự kiện (Event-Driven & Idempotency):**
   - **Tiêu thụ event Appointment**: Queue nghe `appointment.checked-in` để cấp vé và `appointment.cancelled` để hủy vé còn `Waiting`/`Called`.
     - Chống trùng lặp 2 lớp: Dedupe theo `(ConsumerName, EventId)` qua `InboxMessages` và Idempotency theo `AppointmentId`. Nếu cùng appointment đến lần hai (kể cả khác EventId), trả về ticket hiện có, tuyệt đối không tăng counter hay cấp số lần hai.
      - Cấp số, tạo ticket, ghi Inbox được commit nguyên tử trong cùng một local transaction. Event hủy dùng Inbox và tombstone theo AppointmentId; check-in đến trễ không tạo vé.
   - **Hạ tầng RabbitMQ Topology & Dead Letter Queue**:
      - Queue chính bind hai routing key Appointment. Hàng đợi retry tách theo routing key để redelivery không biến `appointment.cancelled` thành check-in; poison messages vào DLQ.
   - **Phát sự kiện trạng thái**:
     - Phát các sự kiện `PatientCalled`, `ExamStarted`, `ExamCompleted`, `PatientSkipped` qua Outbox pattern vào RabbitMQ để các service khác (như Appointment Service cập nhật `Completed`) tiếp nhận xử lý.

---

### 2. Hiện trạng triển khai chi tiết & Đối soát mã nguồn

#### 2.1. Phía Cơ sở Dữ liệu & Clean Architecture
- **`ClinicQueueDB`**:
  - Sở hữu 4 bảng độc lập: `QueueTickets`, `QueueCounters`, `InboxMessages`, `OutboxMessages`.
  - Toàn bộ ràng buộc Unique Index bảo vệ dữ liệu đã được cấu hình chặt chẽ:
    - Unique `(DoctorId, QueueDate, QueueNumber)` trên `QueueTickets`.
    - Unique `AppointmentId` trên `QueueTickets` (đảm bảo 1 appointment chỉ có tối đa 1 ticket).
    - Unique `(ConsumerName, EventId)` trên `InboxMessages`.
    - Khóa chính `(DoctorId, QueueDate)` trên `QueueCounters`.
   - ClinicDB không còn sở hữu Queue model/runtime DbSet; migration `CompleteAppointmentServiceCutover` drop `QueueTickets`/`QueueCounters`. Migration cũ còn định nghĩa bảng là lịch sử, không phải schema hiện hành.
   - Queue Service sở hữu `QueueTickets`, `QueueCounters`, `InboxMessages`, `OutboxMessages` và `CancelledAppointments` làm tombstone cho event hủy đến trước check-in.

#### 2.2. Phía API Endpoints & Gateway Routing
- **Gateway Cutover**:
  - `QueueController`, `DoctorsController` (các route `/doctors/{id}/queue`, `/doctors/{id}/queue/next`), và `MeController` (`/me/queue-status`) trong `Clinic.API` chuyển tiếp 100% sang `Clinic.Queue.Service` qua `QueueServiceClient`.
- **Ma trận Phân quyền & Bảo mật**:
  - Bác sĩ chỉ được xem và gọi số thuộc hàng đợi của chính mình (kiểm tra claim `doctorId` từ token).
  - Bệnh nhân chỉ xem được vé khám thuộc tài khoản của mình (`patientId` claim).
  - **Cập nhật quyền theo quyết định 2026-10-25:** Lễ tân chỉ được gọi số, bỏ qua ticket `Waiting`, đặt ưu tiên khi ticket `Waiting`, và retry ticket `Skipped`; không được bắt đầu/hoàn tất khám. Bác sĩ chỉ thao tác trên hàng đợi của mình, được gọi nhiều số lần lượt mà không cần hoàn tất lượt trước; chỉ bỏ qua ticket `Called` chưa bắt đầu khám. Admin giữ quyền theo policy hiện hành.
  - Appointment Service phát event hủy; Queue chỉ hủy vé `Waiting`/`Called`. Vé `InProgress`/`Completed` giữ nguyên và cần reconciliation nếu Appointment bị hủy đồng thời.

#### 2.3. Quy trình Trạng thái Hàng đợi
- Vòng đời trạng thái: `Waiting` $\to$ `Called` $\to$ `InProgress` $\to$ `Completed` (hoặc `Skipped`).
- Event hủy chuyển vé `Waiting`/`Called` sang `Cancelled`; event cũ giải phóng slot do reschedule không hủy vé/tombstone.
- Chuyển trạng thái phát sự kiện Outbox tức thời (`ExamStarted`, `ExamCompleted`, `PatientSkipped`).
- Ticket `Skipped` được giữ hiển thị ở giao diện lễ tân để retry thủ công về `Waiting`; retry không tự gọi số.
- Tích hợp telemetry OpenTelemetry: ghi nhận metrics `queue.consumer.processed`, `queue.consumer.lag`, `queue.ticket.processing_time`, `queue.outbox.published`.

---

### 3. Đánh giá Tiêu chuẩn Kiểm thử & Build của Giai đoạn 4
- **Unit & Integration Tests**:
  - `Clinic.Queue.Service.Tests`: **50/50 passed**, gồm test hủy ticket, event trùng, check-in đến sau cancellation, reschedule slot-release và giữ nguyên vé đã bắt đầu/hoàn tất.
- **Frontend Type Check & Build**:
  - `npm run type-check` (`vue-tsc --build`): **0 errors**.
  - `npm run build`: **Build production thành công**.
- **Docker Stack Runtime**:
  - Đã build và recreate backend, Appointment, Scheduling và Queue. Các container đều healthy; health/readiness của Queue và Appointment trả `200`, Gateway `/healthz` trả `200`, protected Queue API trả `401` nếu thiếu token.
  - Đã xác nhận migration `20261025091831_AddCancelledAppointmentTombstones` được áp dụng và bảng `CancelledAppointments` tồn tại trong ClinicQueueDB. Reconciliation script chạy với exit code `0` trên SQL Server hiện tại và chỉ đọc dữ liệu.

---

### 4. Kết luận Giai đoạn 4

Queue core đã tách khỏi Clinic và có API, DB riêng, cấp số/idempotency/outbox. Hủy sau check-in đã được xử lý trong source, migration đã áp dụng và RabbitMQ consumer khởi động mà không có lỗi topology trong logs. Kiểm thử manager bao phủ cancellation và event đến lệch thứ tự; chưa phát event hủy end-to-end bằng token/Appointment thật. Reconciliation read-only chạy thành công; kết quả mismatch cần diễn giải thủ công vì dữ liệu hiện là mock. Không tự replay hoặc chỉnh dữ liệu lệch.

**Bổ sung thiết kế và trạng thái sau nghiệm thu (2026-10-25):** ma trận quyền lễ tân/bác sĩ và thao tác retry `Skipped` được cập nhật theo mục 2.2–2.3 ở trên. Phần code tương ứng hiện được staged, gồm endpoint/domain/frontend và điều chỉnh QueueManager tests; chưa chạy lại test/build/smoke trong lượt tài liệu này, do đó đây là trạng thái implement chờ xác minh, chưa phải kết quả nghiệm thu mới.

Source diff cùng đợt còn bổ sung `GET /receptionist/queue-doctors` để lễ tân tải queue theo các bác sĩ có ca trong ngày; sửa màn bác sĩ cho phép nhiều ticket được gọi/đang khám song song và mở/tạo Draft report từ lượt `InProgress`; sửa hiển thị trạng thái queue. Các thay đổi này chưa được smoke test sau commit `c13a7ef`. `queue-doctors.activeAppointmentsCount` hiện là placeholder `0`, không phải thống kê đã tính từ Appointment Service.

---

## PHẦN 4: ĐỐI SOÁT TỒN ĐỌNG ENTITY & FEATURE TRONG MONOLITH, NGUYÊN NHÂN VÀ KHẢ NĂNG CHUYỂN ĐỔI NGAY

> [!NOTE]
> Phần 4 bên dưới là kết quả rà soát lịch sử 2026-10-24. Trạng thái Queue đã được cập nhật theo source và migration hiện hành ở ngày 2026-10-25; các nhận định cũ về Queue entity, handler và bảng ClinicDB đã bị thay thế. Trạng thái Appointment cần xem theo cập nhật Phase 3 riêng.

---

### 1. Bảng đối soát tổng hợp hiện trạng Entity & Feature giữa Microservices và Monolith

| Giai đoạn | Domain Entities trong Monolith | DbSet trong `ApplicationDbContext` | Bảng vật lý trong `ClinicDB` (SQL Server) | Features / Handlers trong Monolith | Repositories trong Monolith | Trạng thái thực thi ở API Gateway (`Clinic.API`) | Mức độ tồn đọng |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Phase 2** (Scheduling) | **0 entity** (Đã xóa toàn bộ) | **0 DbSet** (Đã xóa toàn bộ) | **0 bảng** (Đã DROP qua migration `RemoveLegacyStaffAndSchedulingSchema`) | **0 feature** (Đã xóa `Employees`, `Doctors`, `Specialties`, `WorkSchedules`, `Slots`) | **0 repo** | 100% Proxy qua `SchedulingProxyMiddleware` và controller điều phối | **0% tồn đọng** (Đã dọn dẹp triệt để) |
| **Phase 3** (Appointment) | `Appointment.cs`, `AppointmentSnapshot.cs` | `DbSet<Appointment>`, `DbSet<AppointmentSnapshot>` | `Appointments` (776 dòng), `AppointmentSnapshots` (50 dòng) | `Features/Appointments` (8 Handlers: Cancel, Confirm, GetById, Changelog, MyAppointments, PatientAppointments, Pending, Summary) | `IAppointmentRepository`, `AppointmentRepository` | Đa số proxy sang `Clinic.Appointment.Service`, **nhưng vẫn còn:**<br>1. `EnsureAppointmentInClinicDbAsync` ghi đè (dual-write)<br>2. `PatientsController.GetPatientAppointments` gọi handler cũ<br>3. `AdminController.GetDashboardData` & `GetStatistics` chọc trực tiếp vào repo | **Còn tồn đọng đáng kể** (Bị giữ lại do phụ thuộc nghiệp vụ) |
| **Phase 4** (Queue) | Không còn Queue entity; `QueueStatus` enum cũ đã được gỡ | Không còn Queue DbSet | Migration `CompleteAppointmentServiceCutover` drop Queue tables khỏi ClinicDB; Queue runtime data thuộc ClinicQueueDB | Không còn Queue handlers trong Clinic | Không còn Queue repository/configuration | Các API Queue proxy qua Queue Service | **Đã tách; chỉ còn DTO Gateway và migration lịch sử** |

---

### 2. Chi tiết thực trạng theo từng Phase

#### 2.1. Phase 2 (Staff & Scheduling Service)
- **Thực trạng**: 
  - Toàn bộ 6 thực thể (`Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`) đã được chuyển dời hoàn toàn sang `Clinic.Scheduling.Service` và cơ sở dữ liệu `ClinicSchedulingDB`.
  - Migration `20261024055019_RemoveLegacyStaffAndSchedulingSchema` đã drop triệt để 6 bảng trên khỏi `ClinicDB`.
  - Không còn tồn tại bất kỳ handler, repository, hay quan hệ foreign key/navigation nào trong monolith.
- **Đánh giá**: **Hoàn tất 100%, không còn tàn dư.**

---

#### 2.2. Phase 4 (Queue Service)
- Entity, DbSet, repository, handler và runtime ClinicDB table đã được gỡ; migration lịch sử vẫn có thể chứa schema/seed cũ và không cần xóa.
- API Queue của Gateway tiếp tục dùng một số DTO từ Clinic.Application làm metadata/response contract; đó không phải Queue persistence hay business logic.
- `QueueStatus` cũ trong `Clinic.Domain` đã được gỡ sau khi xác nhận không có caller.
- **Tồn đọng chức năng:** Appointment cancellation được xử lý bằng event sang Queue. Reconciliation trạng thái liên dịch vụ hiện là script chỉ đọc; mismatch của mock data không tự sửa. Hủy khi ticket đã `InProgress` không đảo ngược Queue state và cần được xử lý bằng nghiệp vụ/đối soát.

---

#### 2.3. Phase 3 (Appointment Service)
- **Những gì còn tồn đọng trong Monolith**:
  1. `Clinic.Domain/Entities/Appointment.cs` và `AppointmentSnapshot.cs`.
  2. `DbSet<Appointment>` và `DbSet<AppointmentSnapshot>` trong `ApplicationDbContext.cs`.
  3. Bảng `Appointments` (776 dòng) và `AppointmentSnapshots` (50 dòng) trong `ClinicDB` (trong khi `ClinicAppointmentDB` thực tế đã có 788 appointments và 68 snapshots - đã bị lệch dữ liệu/data drift).
  4. Thư mục `Clinic.Application/Features/Appointments` chứa 8 command/query handler.
  5. `IAppointmentRepository.cs` và `AppointmentRepository.cs`.
  6. Cơ chế Dual-write ngầm: Phương thức `EnsureAppointmentInClinicDbAsync` trong `AppointmentsController.cs` (dòng 59-105) tự động insert bản ghi lịch hẹn mới vào `ClinicDB` sau khi gọi thành công sang `Clinic.Appointment.Service`.
  7. API vẫn dùng handler cũ: `PatientsController.GetPatientAppointments` (dòng 61-67) vẫn gửi `GetPatientAppointmentsQuery` vào `AppointmentRepository` của Monolith.
  8. Dashboard Admin: `AdminController.GetDashboardData` và `GetStatistics` vẫn inject `IAppointmentRepository` để query số liệu tổng hợp trực tiếp từ `ClinicDB`.
- **Nguyên nhân tại sao còn tồn đọng**:
  1. **Sự phụ thuộc hạ tầng nặng nề từ Bounded Context `MedicalReports` (Phase 5 - Chưa di chuyển)**:
     - Module Bệnh án (`MedicalReports`) hiện vẫn 100% nằm trong monolith.
     - Entity `MedicalReport` duy trì quan hệ 1-1 với `Appointment` trong cùng DbContext (`builder.HasOne(m => m.Appointment)`).
     - `MedicalReportRepository.cs` (các dòng 35, 47, 62, 82, 92, 102, 107, 120) thực hiện `.Include(mr => mr.Appointment)` và query trực tiếp các trường `mr.Appointment.DoctorId`, `mr.Appointment.ScheduleDate`, `mr.Appointment.DoctorName` để phục vụ lọc, phân trang và tìm kiếm bệnh án cho Bác sĩ.
     - `SaveMedicalReport.cs` (dòng 43-44) bắt buộc phải gọi `_appointmentRepository.GetByIdAsync(request.AppointmentId)` để kiểm tra sự tồn tại của lịch hẹn.
     - **Hệ quả kiến trúc:** Vì Phase 5 chưa được bóc tách và chưa được thiết kế cơ chế denormalize lưu sẵn `DoctorId`/`ScheduleDate` trên `MedicalReport`, đội ngũ phát triển buộc phải giữ lại bảng `Appointments` trong `ClinicDB` và cài cắm hàm `EnsureAppointmentInClinicDbAsync` để copy dữ liệu sang, tránh cho Bác sĩ bị lỗi 500/NotFound khi tạo bệnh án.
  2. **Các API báo cáo tổng hợp chưa được bóc tách sang Appointment Service**:
     - `PatientsController.GetPatientAppointments` chưa được chuyển hướng proxy sang `AppointmentServiceClient`.
     - `AdminController.GetDashboardData` và `GetStatistics` chưa có API tương đương trên `Clinic.Appointment.Service` để lấy dữ liệu thống kê tổng hợp.
- **Có thể chuyển / dọn dẹp ngay được không?**:
  - **CHƯA THỂ XÓA BỎ NGAY LẬP TỨC (Chặn bởi phụ thuộc của Phase 5)**:
    - Nếu thực hiện drop bảng `Appointments` hoặc xóa `IAppointmentRepository` ngay tại thời điểm này, chức năng Ghi bệnh án của Bác sĩ (`SaveMedicalReport`, `FinalizeMedicalReport`), Tra cứu lịch sử khám (`GetPatientMedicalHistoryForDoctor`), và Báo cáo Admin (`DashboardData`, `Statistics`) sẽ **gãy đổ hoàn toàn (Crash HTTP 500 / NotFoundException)**.
  - **Lộ trình điều kiện tiên quyết (Prerequisites) để có thể dọn dẹp triệt để**:
    1. **Bước 1**: Bổ sung trường denormalized vào bảng `MedicalReports` trong Monolith: `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate`.
    2. **Bước 2**: Chuyển đổi `SaveMedicalReport.cs` từ việc gọi `_appointmentRepository.GetByIdAsync` sang gọi HTTP client `AppointmentServiceClient.GetAppointmentAsync(id)`. Khi tạo `MedicalReport`, điền trực tiếp các trường denormalized từ DTO trả về.
    3. **Bước 3**: Refactor `MedicalReportRepository.cs` để lọc theo các trường denormalized trên chính bảng `MedicalReports`, loại bỏ hoàn toàn `.Include(mr => mr.Appointment)`.
    4. **Bước 4**: Thêm endpoint thống kê (`GET /admin/dashboard-stats`, `GET /admin/statistics`) trên `Clinic.Appointment.Service` và cập nhật `AdminController` gọi sang.
    5. **Bước 5**: Chuyển đổi `PatientsController.GetPatientAppointments` sang proxy gọi `AppointmentServiceClient`.
    6. **Bước 6**: Xóa bỏ hoàn toàn phương thức `EnsureAppointmentInClinicDbAsync` trong `AppointmentsController.cs`.
    7. **Bước 7**: Xóa `Features/Appointments`, `IAppointmentRepository`, `AppointmentRepository`, `Appointment.cs`, `AppointmentSnapshot.cs`, và chạy EF Migration drop 2 bảng `Appointments`, `AppointmentSnapshots` khỏi `ClinicDB`.

---

### 3. Kế hoạch hành động trong audit lịch sử (đã được thay thế)

> Bảng hành động và mô tả ngay sau đây được lập trước các commit hoàn tất Phase 3/4. Nội dung giữ để truy vết lịch sử; trạng thái hiện hành nằm trong bảng tổng kết cập nhật bên dưới và các mục Phase 2–4 ở trên.

| Hạng mục | Khả năng thực hiện ngay | Rủi ro nếu xóa ngay | Hành động đề xuất |
| :--- | :---: | :---: | :--- |
| **Phase 2 (Scheduling)** | **Đã hoàn tất 100%** | Không có | Duy trì hiện trạng |
| **Phase 4 (Queue)** | **Đã tách khỏi ClinicDB** | Không còn Queue tables/handlers ở Clinic runtime | Các thay đổi quyền và thao tác lễ tân/bác sĩ mới đang chờ test/build/smoke sau commit `c13a7ef`; cải tiến vận hành DLQ/replay vẫn mở. |
| **Phase 3 (Appointment)** | **Đã hoàn tất; Appointment không còn schema/runtime legacy trong ClinicDB** | Không còn dual-write hay Appointment repository; MedicalReports giữ snapshot trong Clinic | MedicalReports Service riêng thuộc giai đoạn sau; không phải blocker để đóng Phase 3 hiện tại. |

---

## TỔNG KẾT TOÀN DIỆN CẢ 3 GIAI ĐOẠN (PHASE 2, 3, 4)

| Giai đoạn | Service phụ trách | Cơ sở Dữ liệu riêng | Trạng thái dọn dẹp trên Monolith (`ClinicDB`) | Saga / Concurrency / Idempotency | Cơ chế Outbox & Event-Driven | Kiểm thử tự động (Unit/Integration) | Trạng thái Nghiệm thu |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Phase 2** | `Clinic.Scheduling.Service` | `ClinicSchedulingDB` | ✅ **Sạch 100%** (Đã drop 6 bảng) | ✅ Đạt (Dual-Constraint capacity, 10m lease) | ✅ Đạt (`SchedulingOutboxPublisher`, 122/122 sent) | 37/37 tests pass | **HOÀN TẤT & ĐẠT 100%** |
| **Phase 3** | `Clinic.Appointment.Service` | `ClinicAppointmentDB` | ✅ Appointment schema/runtime đã gỡ khỏi ClinicDB; MedicalReports vẫn là module Clinic và dùng snapshot/API | ✅ Booking/Reschedule Saga, compensation | ✅ Appointment outbox/consumer | 33/33 Appointment tests ở mốc sau cutover | **HOÀN TẤT**; MedicalReports Service riêng là giai đoạn sau |
| **Phase 4** | `Clinic.Queue.Service` | `ClinicQueueDB` | ✅ Queue tables/runtime đã gỡ khỏi ClinicDB; migration lịch sử được giữ | ✅ Atomic counter, Inbox/idempotency, cancellation event | ✅ Queue Outbox/Appointment consumer | 50/50 Queue tests ở mốc commit trước working diff hiện tại | **Queue core đã tách**; cập nhật phân quyền/UX mới đang chờ xác minh; P1 alert/replay vẫn mở |

**Mốc kiểm thử gần nhất được ghi nhận trước working diff hiện tại:** Queue Service **50/50 passed**, Appointment Service **33/33 passed**. Đây không phải bằng chứng cho các thay đổi đang staged sau commit `c13a7ef`. Reconciliation script chỉ đọc, không tự sửa dữ liệu mock. Chưa chạy thao tác cancel/check-in bằng account đã xác thực trên live API.


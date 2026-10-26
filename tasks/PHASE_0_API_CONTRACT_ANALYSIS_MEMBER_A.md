# Phân Tích & Kế Hoạch Chuyển Đổi API Contract Sang Kiến Trúc Microservices

## Giai Đoạn 0: Chuẩn Hóa Contract & Ranh Giới Dịch Vụ (Dành Cho Thành Viên A)

> **Tài liệu tham chiếu**:
>
> - `tasks/TASK_MEMBER_A.md` (Nhiệm vụ Giai đoạn 0)
> - `tasks/COMMON_REQUIREMENTS_AND_NOTES.md` (Yêu cầu kỹ thuật chung)
> - `microservices_analysis.md` (Định hướng kiến trúc tổng thể)
> - `old-api-contract.json` (OpenAPI Spec Monolith v1.0)
> - Mã nguồn Monolith: `backend/Clinic.API/Controllers` & `backend/Clinic.Application/Features`

---

## 1. Bối Cảnh & Vấn Đề Cốt Lõi Của Hệ Thống Hiện Tại (Monolith)

Trong kiến trúc monolith hiện tại (`old-api-contract.json` và mã nguồn backend):

1. **Chia sẻ Database & Context**: Toàn bộ hệ thống dùng chung 1 `ApplicationDbContext` duy nhất quản lý hơn 20 entities trên một cơ sở dữ liệu SQL Server.
2. **Coupling dữ liệu qua Navigation Properties**: Các DTO và handler truy cập trực tiếp các chuỗi quan hệ sâu (ví dụ: `Appointment -> WorkSchedule -> Doctor -> Employee -> Person` hoặc `QueueTicket -> Appointment -> Patient -> Person`).
3. **Phụ thuộc vào Distributed Transaction ngầm định**: Các nghiệp vụ phức tạp như Đặt lịch, Check-in, Khám bệnh, Chốt bệnh án đang dùng transaction lock SQL Server (`InitializeTransactionLockAsync`) và lưu nhiều aggregate khác nhau trong cùng một lần `SaveChangesAsync`.
4. **Thiếu vắng các cơ chế bảo đảm tin cậy**:
    - Chưa có API Versioning nhất quán trên endpoint (`/api/v1/...`).
    - Chưa có cơ chế **Reservation** (giữ slot tạm thời có thời hạn), dẫn đến nguy cơ overbooking hoặc deadlock khi chịu tải cao.
    - Các lệnh ghi (mutating commands) chưa hỗ trợ **Idempotency-Key**, khiến việc client hoặc broker retry dễ gây trùng lặp dữ liệu (trùng vé khám, trùng lịch hẹn).
    - Chưa chuẩn hóa mô hình báo lỗi theo chuẩn quốc tế (RFC 7807 Problem Details).

---

## 2. Bản Đồ Ranh Giới Dịch Vụ & Database Ownership (6 Microservices)

Tuân thủ nguyên tắc: **Mỗi aggregate/entity chỉ có đúng 1 service sở hữu; không có foreign key hay navigation property xuyên service.**

| Service                           | Aggregates & Entities Sở Hữu                                                     | Trách Nhiệm Nghiệp Vụ Cốt Lõi                                                          | Cơ Chế Giao Tiếp Với Service Khác                                                                                                                                                |
| :-------------------------------- | :------------------------------------------------------------------------------- | :------------------------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Identity Service**           | `User`, `Role`, `Permission`, `UserRole`, `RolePermission`, `RefreshToken`, `Person`       | Quản lý xác thực, đăng nhập, phân quyền, cấp phát token JWT/OIDC.                      | Công khai endpoint JWKS (`/.well-known/jwks.json`) để các service khác tự xác thực JWT độc lập.                                                                                  |
| **2. Staff & Scheduling Service** | `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`, `SlotReservation` | Quản lý nhân viên y tế, bác sĩ, lịch trực, duyệt ca và sở hữu capacity/slot.           | Cung cấp Synchronous API (`GetAvailability`, `ReserveSlot`, `ReleaseSlot`, `ConfirmSlot`); phát sự kiện lịch trực qua Outbox; tiêu thụ `AppointmentCancelled` để giải phóng slot. |
| **3. Appointment Service**        | `Appointment`, `AppointmentSnapshot` (Traceback), `PatientProjection`            | Đặt lịch, xác nhận, đổi lịch, hủy lịch và check-in lịch hẹn khám bệnh.                 | Sử dụng Saga điều phối giữ chỗ với Scheduling Service; lưu thông tin hiển thị denormalized trên `Appointment` & nhật ký chỉnh sửa trên `AppointmentSnapshot`; phát sự kiện `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn`; tiêu thụ `ExamCompleted`, `UserProfileUpdated`. |
| **4. Queue Service**              | `QueueTicket`, `QueueCounter`                                                    | Tiếp nhận bệnh nhân, cấp số thứ tự khám tại chỗ, quản lý vòng đời hàng đợi phòng khám. | Nhận sự kiện check-in; tự sinh số khám với unique constraint cục bộ; phát sự kiện `PatientCalled`, `ExamStarted`, `ExamCompleted`.                                               |
| **5. Medical Records Service** | `Patient`, `MedicalReport`, `MedicalReportAmendment`, `MedicalAccessAudit` | Quản lý hồ sơ bệnh nhân và bệnh án trong cùng service/DB; không có Patient Service riêng. | Dùng `AppointmentId` làm encounter key, loose reference tới Appointment; permission riêng và audit truy cập y tế. |
| **6. Notification Service**       | `Notification`, `NotificationTemplate`, `DeliveryAudit`                          | Quản lý thông báo in-app, SignalR Hub, gửi SMS/Email.                                  | Lắng nghe event từ message broker qua outbox/inbox; không nhận domain entity.                                                                                                    |

---

## 3. Phân Tích & Chuyển Đổi Chi Tiết 7 Use Case Cốt Lõi (Giai Đoạn 0)

### THỐNG NHẤT KHÔNG CẦN DÙNG prefix `/api/v1`

### 3.1. Use Case 1: Login & Xác Thực

- **Hiện trạng Monolith**:
    - Endpoint: `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` (trong `AuthController.cs`).
    - Luồng xử lý: Truy vấn `User`, `Role`, nạp danh sách permissions từ DB monolith; refresh token lưu trong HttpOnly Cookie `/auth`.
- **Thay đổi khi chuyển sang Microservices**:
    - **Endpoint chuẩn hóa**: `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/revoke`.
    - **JWKS Endpoint mới**: `GET /.well-known/jwks.json` cung cấp public key để API Gateway và 5 service còn lại tự động kiểm tra chữ ký JWT mà **không cần gọi RPC/HTTP đồng bộ** sang Identity Service.
    - **Payload & Claims**: Access Token chỉ chứa claims thiết yếu: `sub (UserId)`, `roles`, `permissions`, `doctorId/patientId` (nếu có). Loại bỏ dữ liệu cá nhân (họ tên, email, số điện thoại) khỏi payload token để tránh rò rỉ và token quá lớn.

---

### 3.2. Use Case 2: Availability (Tra Cứu Khung Giờ Khả Dụng)

- **Hiện trạng Monolith**:
    - Endpoint: `GET /slots?doctorId=&specialtyId=&fromDate=&toDate=` (trong `SlotsController.cs`) và `GET /doctors/available`.
    - Luồng xử lý: `GetAvailableSlotsQueryHandler` query `WorkScheduleRepository`, sau đó join với bảng `Appointment` để trừ đi các khung giờ đã có người đặt.
- **Thay đổi khi chuyển sang Microservices**:
    - **Đổi quyền sở hữu**: Chuyển API này hoàn toàn về **Staff & Scheduling Service** (`GET /schedules/availability` hoặc `GET /slots`).
    - **Ranh giới dữ liệu & Xóa bỏ BookingSlot cũ**: Scheduling Service **tuyệt đối không được query sang Appointment DB**. Service này tự quản lý capacity, ca làm việc và các slot giữ chỗ/xác nhận thông qua `SlotReservations` ngay trong cơ sở dữ liệu `ClinicSchedulingDB`. Bảng `BookingSlots` trong monolith `ClinicDB` đã được xóa bỏ hoàn toàn (không còn entity `BookingSlot`).
    - **Quy tắc sinh slot thời gian thực**:
      - Các slot 15 phút được sinh tự động phủ kín ca trực nếu ca trực chưa đạt ngưỡng `RecommendedCapacity`.
      - **Chặn khung giờ quá khứ**: Chỉ sinh các slot trong tương lai cách thời điểm hiện tại tối thiểu 30 phút (`>= UtcNow + 30m`) để bệnh nhân có đủ thời gian chuẩn bị và di chuyển tới phòng khám.
      - Chuẩn hóa thời gian: Lưu trữ và tính toán theo UTC ở backend; frontend hiển thị theo múi giờ địa phương của người dùng (GMT+7).

---

### 3.3. Use Case 3: Reservation (Giữ Chỗ Tạm Thời - API Mới)

- **Hiện trạng Monolith**:
    - **Chưa có API này trong Monolith!** Trước đây, người dùng chọn slot và gửi thẳng `POST /appointments`. Monolith mở transaction lock SQL Server (`InitializeTransactionLockAsync`) để chặn race-condition, gây nghẽn kết nối khi có nhiều request đồng thời.
- **Thay đổi khi chuyển sang Microservices**:
    - **Bổ sung cụm API Reservation trong Staff & Scheduling Service**:
        - `POST /api/v1/schedules/{scheduleId}/reservations`: Giữ chỗ slot.
            - _Headers_: `Idempotency-Key` (bắt buộc).
            - _Request Body_: `{ "timeSlot": "08:30:00", "patientId": "guid" }`.
            - _Response_: `{ "reservationId": "guid", "scheduleId": "guid", "timeSlot": "08:30:00", "expiresAt": "2026-10-15T09:05:00Z" }`.
        - `POST /api/v1/schedules/{scheduleId}/reservations/{reservationId}/confirm`: Xác nhận slot thành công khi Appointment đã tạo xong.
        - `POST /api/v1/schedules/{scheduleId}/reservations/{reservationId}/release`: Giải phóng slot (compensation) nếu bước tạo Appointment thất bại hoặc timeout.
    - **Tính toàn vẹn**: Slot reservation xử lý atomic trong Scheduling DB kèm cơ chế timeout (ví dụ: 10 phút) tự động hồi phục slot nếu giao dịch không hoàn tất.

---

### 3.4. Use Case 4: Appointment (Đặt, Đổi, Hủy Lịch Khám)

- **Hiện trạng Monolith**:
    - Endpoint: `POST /appointments`, `PATCH /appointments/{id}`, `POST /appointments/{id}/cancel`, `POST /appointments/{id}/confirm` (trong `AppointmentsController.cs`).
    - Luồng xử lý: `CreateAppointmentCommandHandler` tiêm `IWorkScheduleRepository`, `IPatientRepository`, `IAppointmentRepository`. DTO trả về lấy tên qua quan hệ `workSchedule.Doctor.Employee.Person.FullName`.
- **Thay đổi khi chuyển sang Microservices**:
    - **Endpoint chuẩn hóa**: `/appointments/...` (không bắt buộc tiền tố `/api/v1`), bắt buộc header `Idempotency-Key` khi tạo lịch.
    - **Cắt chuyển sớm quan hệ với WorkSchedule (Monolith)**:
      - Loại bỏ Foreign Key `FK_Appointments_WorkSchedules_WorkScheduleId` trong `ClinicDB`.
      - Bổ sung các cột denormalized: `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate` trực tiếp vào bảng `Appointments`.
      - Phân tách 2 luồng: **Đặt lịch Online** (bắt buộc giữ chỗ qua `ReserveSlot`, ràng buộc bởi `RecommendedCapacity` và slot 15p) và **Khách Vãng lai (Walk-in)** (lễ tân tiếp nhận linh hoạt, không chiếm slot online, được phép vượt ngưỡng đề xuất).
      - Đã gộp toàn bộ thay đổi schema của giai đoạn này trong Monolith vào migration `20261021175324_DecoupleSchedulingAndRemoveBookingSlots.cs`.
    - **Quy trình Saga Booking (Orchestration)**:
        ```text
        Client -> Appointment Service: CreateAppointment(idempotencyKey)
                     |
                     +-> (1) Staff & Scheduling: POST ReserveSlot
                     |         <-- Trả về reservationId
                     |
                     +-> (2) Appointment Service: Lưu Appointment (Pending) & Snapshot vào Appointment DB
                     |         (Nếu lỗi -> Gọi ReleaseReservation để bồi hoàn)
                     |
                     +-> (3) Staff & Scheduling: POST ConfirmReservation
                     |
                     +-> (4) Appointment Service: Chuyển trạng thái sang Confirmed, Outbox phát AppointmentCreated
        ```
    - **Denormalized Fields & AppointmentSnapshot (Traceback)**: Thông tin hiển thị chính thức (`DoctorName`, `SpecialtyName`, `ScheduleDate`, `PatientName`, `PatientPhone`, `PatientGender`, `PatientDateOfBirth`) được lưu denormalized trực tiếp trên bảng `Appointments` (kèm bản chiếu `PatientProjections` đồng bộ qua `UserProfileUpdated`) để tránh join phân tán. Bảng `AppointmentSnapshot` giữ đúng vai trò lưu nhật ký chỉnh sửa (1-N changelog, có lưu kèm `OldDoctorId`, `OldDoctorName`, `OldScheduleDate`, `UpdatedByUserName`) phục vụ traceback khi người khám có vấn đề về đặt/đổi/hủy lịch.
    - **Hủy lịch & Đổi lịch (Cancel & Reschedule)**: Khi hủy lịch, phát sự kiện `AppointmentCancelled` để Scheduling Service giải phóng `SlotReservation` đã confirm. Khi đổi lịch, thực thi Reschedule Saga 2 pha (giữ slot mới $\rightarrow$ lưu snapshot & cập nhật lịch $\rightarrow$ confirm slot mới & giải phóng slot cũ).
    - **Vòng đời trạng thái**: `Pending` $\rightarrow$ `Confirmed` $\rightarrow$ `CheckedIn` $\rightarrow$ `Completed` / `Cancelled`.

---

### 3.5. Use Case 5: Check-in (Tiếp Nhận & Cấp Số Thứ Tự)

- **Hiện trạng Monolith**:
    - Endpoint: `POST /appointments/{appointmentId}/check-in` (trong `AppointmentsController.cs`).
    - Luồng xử lý: `CheckInHandler` đọc `Appointment`, truy cập `appointment.WorkSchedule.DoctorId`, gọi `appointment.CheckIn()` (sửa status của `Appointment` đồng thời tạo entity `QueueTicket`), sau đó lưu cả hai vào DB chung qua 1 lệnh `SaveChangesAsync`.
- **Thay đổi khi chuyển sang Microservices**:
    - **Tách ranh giới Aggregate**:
        - **Appointment Service** sở hữu trạng thái check-in của lịch hẹn (`Confirmed` $\rightarrow$ `CheckedIn`).
        - **Queue Service** sở hữu việc sinh số thứ tự và quản lý vé khám (`QueueTicket`, `QueueCounter`).
    - **Workflow Check-in qua Event / API**:
        - Khi tiếp nhận bệnh nhân qua `POST /api/v1/appointments/{id}/check-in`, Appointment Service cập nhật trạng thái lịch sang `CheckedIn` và ghi Outbox phát sự kiện:
          `AppointmentCheckedIn { appointmentId, patientId, doctorId, scheduleDate, patientName, roomNumber }`.
        - **Queue Service** lắng nghe sự kiện trên (hoặc tiếp nhận API check-in trực tiếp từ Lễ tân kèm snapshot dữ liệu): tiến hành cấp số thứ tự trong transaction cục bộ của Queue DB.
    - **Bảo đảm tính duy nhất**: Queue DB thiết lập **Unique Constraint** trên bộ `(DoctorId, ScheduleDate, QueueNumber)` để đảm bảo không bao giờ trùng số khám trong cùng một ca. Hỗ trợ Idempotency trên `appointmentId` để việc retry không tạo vé thứ hai.

---

### 3.6. Use Case 6: Queue (Gọi Số, Khám Bệnh, Hoàn Tất Khám)

- **Hiện trạng Monolith**:
    - Endpoint: `POST /queue/call-next/{doctorId}`, `POST /queue/{ticketId}/start-exam`, `POST /queue/{ticketId}/complete-exam`, `POST /queue/{ticketId}/skip` (trong `QueueController.cs`).
    - Luồng xử lý: Trong `CompleteExamHandler`, khi vé hoàn tất, code gọi tiếp `queueTicket.Appointment.Complete(...)` để cập nhật trạng thái Appointment trong cùng transaction.
- **Thay đổi khi chuyển sang Microservices**:
    - **Endpoint chuẩn hóa**: `/api/v1/queue/tickets/...`, `/api/v1/queue/doctors/{doctorId}/call-next`.
    - **Tách biệt quyền ghi**: Queue Service **chỉ sở hữu trạng thái vé**:
      $$\text{Waiting} \longrightarrow \text{Called} \longrightarrow \text{InProgress} \longrightarrow \text{Completed} \text{ (hoặc Skipped)}$$
    - **Phát sự kiện thay vì sửa trực tiếp**: Khi bác sĩ bấm hoàn tất khám (`complete-exam`), Queue Service chỉ cập nhật `QueueTicket.Status = Completed` trong Queue DB, sau đó ghi Outbox phát `ExamCompleted { ticketId, appointmentId, doctorId, completedAt }`. `AppointmentId` là encounter key; không có `visitId` riêng.
    - **Đồng bộ hóa Eventual Consistency**: Appointment Service tiêu thụ sự kiện `ExamCompleted` và tự chuyển trạng thái của `Appointment` thành `Completed`. Không phụ thuộc vào distributed transaction.

---

### 3.7. Use Case 7: Medical Report (Hồ Sơ Bệnh Án)

- **Hiện trạng Monolith**:
    - Endpoint: `POST /medical-reports`, `GET /medical-reports/ticket/{queueTicketId}`, `POST /medical-reports/{id}/finalize` (trong `MedicalReportsController.cs`).
    - Luồng xử lý: `MedicalReport` gắn chặt vào khóa ngoại `QueueTicketId`. Khi `FinalizeReport`, handler tự động gọi `ticket.Complete()` và `ticket.Appointment.Complete()`. DTO lấy thông tin bệnh nhân qua navigation `ticket.Appointment.Patient.Person`.
- **Thay đổi khi chuyển sang Microservices**:
    - **Định danh hồ sơ**: Bỏ việc phụ thuộc vào `QueueTicketId`; dùng `AppointmentId` ổn định làm khóa encounter duy nhất. Không tạo `visitId`/`encounterId` riêng.
    - **Endpoint chuẩn hóa**:
        - `POST /api/v1/medical-records/appointments/{appointmentId}/draft`: Tạo/cập nhật nháp hồ sơ khám bệnh.
        - `POST /api/v1/medical-records/reports/{reportId}/finalize`: Chốt hồ sơ (bắt buộc `Idempotency-Key`).
        - `GET /api/v1/medical-records/appointments/{appointmentId}`: Xem hồ sơ bệnh án theo lượt khám.
        - `POST /api/v1/medical-records/reports/{reportId}/amendments`: Tạo bản ghi chỉnh sửa/bổ sung hồ sơ sau khi đã chốt.
    - **Lifecycle & Audit**: `ExamStarted` mở context idempotent; bác sĩ tạo Draft khi bắt đầu ghi. `ExamCompleted` chỉ đánh dấu lượt khám kết thúc, không finalize report. Report có trạng thái `Draft` $\rightarrow$ `Finalized`; hồ sơ đã chốt không được sửa trực tiếp, amendment là bản ghi append-only có tác giả, thời điểm, lý do và liên kết phiên bản trước.
    - **Bảo mật & Phân quyền**: Toàn bộ thao tác truy cập hồ sơ bệnh án phải có quyền riêng biệt và ghi nhận **Audit Log độc lập** trong Medical Records DB (không đưa nội dung chẩn đoán/bệnh án vào event notification thông thường).

---

## 4. Chuẩn Hóa API Contract Chung Cho Toàn Hệ Thống

Để đảm bảo các contract đạt tiêu chuẩn nghiệm thu Giai đoạn 0, mọi API mới phải tuân thủ các quy tắc sau:

### 4.1. Quy Ước URL & Versioning

- Format: `/api/v1/{service-route}/{resource}`
- Ví dụ:
    - `/api/v1/auth/login`
    - `/api/v1/schedules/{scheduleId}/reservations`
    - `/api/v1/appointments`
    - `/api/v1/queue/tickets`
    - `/api/v1/medical-records/encounters`

### 4.2. Chuẩn Hóa Headers Bắt Buộc

- `X-Correlation-Id`: Mã định danh phân tán cho distributed tracing.
- `Idempotency-Key`: Khóa chống lặp (UUID) bắt buộc trên các command có rủi ro side-effect cao (`ReserveSlot`, `CreateAppointment`, `CheckIn`, `FinalizeReport`).
- `Authorization`: Bearer Token JWT.

### 4.3. Mô Hình Báo Lỗi Chuẩn (RFC 7807 Problem Details)

Tất cả các service trả về cấu trúc lỗi đồng nhất khi phát sinh exception:

```json
{
    "type": "https://clinic.example.com/errors/slot-already-reserved",
    "title": "Conflict",
    "status": 409,
    "errorCode": "SLOT_ALREADY_RESERVED",
    "detail": "Khung giờ này đã được giữ chỗ bởi một yêu cầu khác.",
    "instance": "/api/v1/schedules/e2f4a1.../reservations",
    "correlationId": "c8a1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
    "timestamp": "2026-10-15T14:30:00Z"
}
```

---

## 5. Danh Mục Thay Đổi Tóm Tắt Theo Use Case

| STT   | Use Case           | Endpoint Monolith Cũ                                             | Endpoint Microservices Mới                                                                                                 | Service Sở Hữu                  | Thay Đổi Trọng Yếu                                                                                                        |
| :---- | :----------------- | :--------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------- | :------------------------------ | :------------------------------------------------------------------------------------------------------------------------ |
| **1** | **Login & Auth**   | `POST /auth/login`                                               | `POST /api/v1/auth/login`<br>`GET /.well-known/jwks.json`                                                                  | Identity Service                | Thêm JWKS endpoint; Access Token chỉ chứa claims cơ bản; tách biệt Identity DB.                                           |
| **2** | **Availability**   | `GET /slots`                                                     | `GET /api/v1/schedules/availability`                                                                                       | Staff & Scheduling              | Tự quản lý capacity; không join sang Appointment DB.                                                                      |
| **3** | **Reservation**    | _(Chưa có)_                                                      | `POST /api/v1/schedules/{id}/reservations`<br>`POST .../reservations/{id}/confirm`<br>`POST .../reservations/{id}/release` | Staff & Scheduling              | **API hoàn toàn mới**: giữ chỗ tạm thời có timeout; chống overbooking atomic; hỗ trợ compensation.                        |
| **4** | **Appointment**    | `POST /appointments`<br>`PATCH /appointments/{id}`               | `POST /api/v1/appointments`<br>`PATCH /api/v1/appointments/{id}`                                                           | Appointment Service             | Saga Booking 3 bước; lưu `AppointmentSnapshot`; hỗ trợ `Idempotency-Key`; phát event qua Outbox.                          |
| **5** | **Check-in**       | `POST /appointments/{id}/check-in`                               | `POST /api/v1/appointments/{id}/check-in`<br>`POST /api/v1/queue/tickets/check-in`                                         | Appointment $\rightarrow$ Queue | Tách việc đổi trạng thái lịch khám với việc sinh số thứ tự; Unique Constraint `(Doctor, Date, QueueNumber)`.              |
| **6** | **Queue**          | `POST /queue/complete-exam`                                      | `POST /api/v1/queue/tickets/{id}/complete-exam`                                                                            | Queue Service                   | Không sửa trực tiếp aggregate Appointment; phát sự kiện `ExamCompleted` qua broker (Eventual Consistency).                |
| **7** | **Medical Report** | `POST /medical-reports`<br>`POST /medical-reports/{id}/finalize` | `PUT /medical-records/appointments/{appointmentId}/draft`<br>`POST /medical-records/reports/{id}/finalize` | Medical Records | `AppointmentId` là encounter key; Patient và MedicalReport cùng owner; amendment append-only và audit log y tế. |

---

## 6. Kế Hoạch Triển Khai Tiếp Theo Cho Thành Viên A

1. **Sinh bộ tài liệu OpenAPI v1**: Viết các file contract OpenAPI 3.0/3.1 riêng cho từng service theo các endpoint đã xác định ở trên.
2. **Thiết lập Data Boundary Scripts**: Viết kịch bản migration tách schema cho 6 database độc lập.
3. **Viết Baseline Integration Tests**: Triển khai các bài kiểm thử tích hợp mô phỏng đúng luồng hành vi hiện tại (đặt lịch, giữ chỗ, check-in, gọi số, hoàn tất khám, duyệt ca) để làm căn cứ đối chiếu khi bóc tách dịch vụ.

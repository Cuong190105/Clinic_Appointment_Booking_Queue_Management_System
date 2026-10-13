# Nhiệm vụ Thành viên A

## Vai trò chính

Thành viên A phụ trách kiến trúc, backend service, database, API, business workflow, saga và bảo mật.

## Yêu cầu chung khi thực hiện

- Mỗi service phải sở hữu database, migration và repository riêng.
- Không truy cập database, EF entity hoặc navigation property của service khác.
- API phải sử dụng DTO và contract được version hóa.
- Business transaction chỉ được thực hiện trong phạm vi một service.
- Các command có khả năng retry phải hỗ trợ idempotency key.
- Mỗi thay đổi phải có unit test và integration test phù hợp.

---

## Giai đoạn 0: Chuẩn hóa contract và baseline

### Nhiệm vụ

1. Xác định service boundary và ownership của entity.
2. Chốt 6 service:
   - Identity Service
   - Staff & Scheduling Service
   - Appointment Service
   - Queue Service
   - Medical Records Service
   - Notification Service
3. Thiết kế database boundary cho từng service.
4. Thiết kế API contract cho login, availability, reservation, appointment, check-in, queue và medical report.
5. Viết baseline integration test cho booking, check-in, gọi số, hoàn tất khám và duyệt ca.

### Yêu cầu cần đạt

- Mỗi entity có đúng một service sở hữu.
- Không có foreign key xuyên service.
- Có tài liệu mô tả service boundary và ownership.
- API contract có request, response, error model và version.
- Baseline test mô tả đúng hành vi hiện tại của hệ thống.

### Lưu ý

- Không chỉ chia project nhưng vẫn dùng chung `ApplicationDbContext`.
- Không đưa domain entity hoặc navigation object vào API/event contract.
- Chốt contract trước khi bắt đầu tách database.

---

## Giai đoạn 1: Tách Identity Service

### Nhiệm vụ

1. Tạo Identity Service.
2. Di chuyển `User`, `Role`, `Permission`, `UserRole`, `RolePermission` và `RefreshToken` sang Identity DB.
3. Xây API đăng ký, đăng nhập, refresh token, revoke token và permission.
4. Triển khai JWT/OIDC và public key/JWKS.
5. Thay dependency trực tiếp vào User/Person bằng `userId`, claims và permission.
6. Bổ sung key rotation và chính sách token expiration.

### Yêu cầu cần đạt

- Identity Service sở hữu toàn bộ dữ liệu tài khoản và phân quyền.
- Service khác validate JWT bằng JWKS, không truy cập Identity DB.
- Login, refresh, revoke và permission test pass.
- Có migration và rollback plan cho dữ liệu Identity.

### Lưu ý

- Gateway không phải điểm tin cậy duy nhất; từng service vẫn phải validate JWT.
- Không đưa dữ liệu cá nhân không cần thiết vào access token.
- Không xóa endpoint auth cũ trước khi regression test hoàn tất.

---

## Giai đoạn 2: Tách Staff & Scheduling Service

### Nhiệm vụ

1. Tạo Staff & Scheduling Service.
2. Di chuyển `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `ShiftRequest` và `WorkSchedule`.
3. Giữ `ShiftRequest.Approve()` và conflict detection trong local transaction.
4. Xây API `GetAvailability`.
5. Xây `ReserveSlot` và `ReleaseSlot`.
6. Thêm timeout, retry và idempotency cho reservation.
7. Phát schedule event thông qua outbox do Thành viên B tích hợp.

### Yêu cầu cần đạt

- Concurrent reservation không vượt capacity.
- Duyệt ca và conflict detection vẫn atomic trong Scheduling DB.
- `ReserveSlot` và `ReleaseSlot` xử lý được request trùng.
- Appointment chỉ nhận schedule ID, doctor ID, ngày và capacity cần thiết.
- Không còn join hoặc foreign key tới Staff/Scheduling DB.

### Lưu ý

- Không triển khai flow `CheckAvailability` rồi mới `CreateAppointment`.
- Capacity phải được giữ atomic trong Scheduling Service.
- Reservation phải có timeout để tránh giữ slot vô thời hạn.

---

## Giai đoạn 3: Tách Appointment Service

### Nhiệm vụ

1. Tạo Appointment Service.
2. Di chuyển `Appointment` và `AppointmentSnapshot` sang Appointment DB.
3. Xây saga:
   `ReserveSlot -> CreateAppointment -> ConfirmReservation`.
4. Xử lý compensation bằng `ReleaseReservation` khi tạo appointment thất bại.
5. Lưu snapshot thông tin cần hiển thị.
6. Xây các trạng thái `Pending`, `Confirmed`, `Cancelled`, `CheckedIn`, `Completed`.
7. Cung cấp API đổi lịch, hủy lịch và xác nhận lịch.

### Yêu cầu cần đạt

- Booking idempotent.
- Không overbooking khi có request đồng thời.
- Saga resume được sau khi service restart.
- Reservation được release khi bước tạo appointment thất bại.
- Appointment DB độc lập với Scheduling DB.

### Lưu ý

- Không dùng distributed transaction giữa Appointment và Scheduling.
- Không sửa trực tiếp dữ liệu của Scheduling Service để rollback.
- Phân biệt lỗi nghiệp vụ và lỗi tạm thời.
- Snapshot chỉ chứa dữ liệu cần thiết, không sao chép toàn bộ Patient entity.

---

## Giai đoạn 4: Tách Queue Service

### Nhiệm vụ

1. Tạo Queue Service.
2. Di chuyển `QueueTicket` và `QueueCounter` sang Queue DB.
3. Thiết kế dữ liệu denormalized gồm `appointmentId`, `patientId`, `doctorId`, `scheduleDate` và `visitId`.
4. Xây workflow `Waiting`, `Called`, `InProgress`, `Completed`, `Skipped`.
5. Xây check-in và cấp queue number trong local transaction.
6. Thêm unique constraint cho queue number.
7. Phát `ExamStarted` và `ExamCompleted`.

### Yêu cầu cần đạt

- Không trùng queue number.
- Queue Service không truy cập Appointment DB.
- Check-in retry được.
- Duplicate event không tạo QueueTicket trùng.
- Có owner rõ ràng cho trạng thái `Completed`.

### Lưu ý

- Không duy trì quan hệ runtime kiểu `QueueTicket -> Appointment -> WorkSchedule -> Doctor`.
- Không dùng `QueueTicketId` làm định danh lâu dài cho hồ sơ bệnh án.
- Phải kiểm tra transaction boundary khi cấp queue number.

---

## Giai đoạn 5: Tách Medical Records Service

### Nhiệm vụ

1. Chuẩn hóa `visitId` hoặc `encounterId`.
2. Tạo Medical Records Service.
3. Di chuyển `MedicalReport` và version history sang Medical Records DB.
4. Xây trạng thái `Draft`, `Finalized` và cơ chế amendment nếu cần.
5. Tách authorization và audit log của hồ sơ bệnh án.
6. Cung cấp API lưu, xem và finalize medical report.

### Yêu cầu cần đạt

- Medical Records không phụ thuộc Queue repository.
- Report sử dụng `visitId`/`encounterId` ổn định.
- Hồ sơ bệnh án có permission và audit log riêng.
- Report Finalized không thể sửa trực tiếp nếu không có quy trình amendment.
- Không chứa dữ liệu bệnh án không cần thiết trong event.

### Lưu ý

- Đây là service có yêu cầu bảo mật cao.
- Phải kiểm thử quyền truy cập theo bác sĩ, nhân viên và bệnh nhân.
- Không truyền nội dung bệnh án qua notification event.

---

## Giai đoạn 6: Tách Notification Service và hoàn thiện Gateway

### Nhiệm vụ

1. Tạo Notification Service.
2. Di chuyển notification persistence, template và delivery attempt.
3. Tách Notification DB.
4. Chuyển SignalR Hub sang Notification Service.
5. Hoàn thiện route Gateway cho toàn bộ service.
6. Phối hợp với Thành viên B để thay thế `Channel<T>` bằng outbox và broker consumer.

### Yêu cầu cần đạt

- Notification Service sở hữu persistence và delivery audit.
- Frontend chỉ truy cập hệ thống qua API Gateway.
- SignalR hoạt động đúng khi chạy một hoặc nhiều instance.
- Không còn dependency vào notification entity hoặc `Person` từ service khác.

### Lưu ý

- Notification chỉ nhận recipient ID và contact snapshot cần thiết.
- Gateway không được trở thành nơi duy nhất validate JWT.
- Cấu hình route, timeout và rate limit phải được version hóa.

---

## Tiêu chí hoàn thành của Thành viên A

- Các service backend và database boundary được triển khai đúng ownership.
- Các API chính có contract, validation, authorization và error model.
- Booking saga hoạt động với compensation và idempotency.
- Reservation không overbook.
- Queue number không trùng.
- Medical Records độc lập với Queue và có audit.
- Không có truy cập database xuyên service.
- Có unit test, integration test và tài liệu vận hành cho phần phụ trách.

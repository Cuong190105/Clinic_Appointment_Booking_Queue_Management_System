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
    - Medical Service
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

**Trạng thái: đã kiểm thử phát hành sau cutover; các luồng Identity/Clinic trong phạm vi giai đoạn đã được xác nhận trên Docker. Identity Service tests đạt 4/4; các failure còn lại của full solution thuộc fixture thời gian/nghiệp vụ cũ ngoài phạm vi Identity.**

### Nhiệm vụ

1. Tạo Identity Service.
2. Chuyển quyền sở hữu xác thực, credentials, `User`, `Role`, `Permission`, `UserRole`, `RolePermission`, `RefreshToken` và password-reset token sang Identity DB. Clinic có thể tạm giữ `Person` và hồ sơ nghiệp vụ.
3. Cung cấp API đăng ký, đăng nhập, refresh token, revoke token, đổi/đặt lại mật khẩu và permission.
4. Triển khai JWT RS256, discovery/JWKS có URL cấu hình theo môi trường và key rotation.
5. Thay truy vấn role database dùng để phân quyền request bằng role/permission claims; đặt authorization policy ở API khi phù hợp. Chỉ giữ kiểm tra trong Application nếu cần xác minh quan hệ hoặc quyền sở hữu tài nguyên.
6. Chuyển luồng tạo/cập nhật tài khoản nhân viên, bác sĩ và gán role sang Identity; loại bỏ credentials khỏi Clinic khi các luồng đó đã chuyển.
7. Bổ sung chính sách token expiration, test, migration và rollback.

### Yêu cầu cần đạt

- Identity Service là nguồn chuẩn duy nhất cho credential, account, role và permission. Clinic `Person`/hồ sơ nghiệp vụ có thể tiếp tục được sở hữu riêng; bản chiếu `User/Role` không được dùng làm nguồn phân quyền.
- Không có handler phân quyền request bằng cách query `UserRoles`/`Role` trong Clinic DB.
- Service khác validate JWT bằng JWKS, không truy cập Identity DB.
- Login, refresh, revoke, permission, reset-password và các luồng account provisioning có test pass.
- Có migration và rollback plan cho dữ liệu Identity.

### Lưu ý

- Gateway không phải điểm tin cậy duy nhất; từng service vẫn phải validate JWT.
- Không đưa dữ liệu cá nhân không cần thiết vào access token.
- Không xóa endpoint auth cũ trước khi regression test hoàn tất.

### Phạm vi đã chuyển

Đã có Identity DB/service, import dữ liệu, JWT RS256, JWKS/discovery, key rotation, đăng ký Patient, login/refresh/revoke, đổi mật khẩu và luồng reset bằng mã dùng một lần. Các kiểm tra quyền cho appointment, patient, doctor, employee profile và shift request đang dùng role/permission từ claims; kiểm tra ownership của tài nguyên vẫn ở Application.

Đã chuyển trong phạm vi hiện tại: login, refresh, logout/revoke, change/reset-password và `/auth/me` gọi Identity; tạo account nhân viên/bác sĩ, gán/cập nhật role, danh sách/chi tiết account quản trị và role catalog cũng gọi Identity. Đã xóa Clinic login/refresh/logout/patient-registration/account-query handlers, dịch vụ phát hành token đối xứng và seeder tạo credentials cục bộ. API không còn fallback đọc credential/profile Clinic. Test hợp đồng auth, smoke Docker và việc tạo account thực đã được xác nhận thành công.

### Bằng chứng kiểm thử phát hành sau cutover (2026-10-21)

- Đối soát: `backend/Clinic.Identity.Service/Persistence/verify-identity-import.sql` chạy thành công trên Docker SQL Server.
- Đối soát mapping: `Persons linked to Identity = 56/56`, `Identity Users = 56`; `Clinic IdentityUserId links missing from Identity = 0`.
- Đối soát liên kết: user/person, user-role, role-permission, refresh-token và inactive account đều có `MismatchCount = 0`.
- Docker readiness: `GET http://localhost:5000/readyz = 200`, `GET http://localhost:5001/readyz = 200`.
- Login flow: register test account `201`, login `200`, `/auth/me` `200` qua API Gateway.
- Provisioning flow: `POST /v1/internal/users/provision = 200`; account mới trả `created = true`, `isActive = false` trước khi Clinic hoàn tất liên kết hồ sơ, đúng lifecycle thiết kế.
- Identity Service tests: `4 passed, 0 failed`, bao phủ registration/login, single-use reset, refresh revoke, inactive staff provisioning và role linking.
- Database migration: `ClinicDB` đã có migration hợp nhất `20261020155017_RenamePatientLimitToRecommendedCapacity`; các bảng auth legacy không còn trong ClinicDB.

### Chuyển sang giai đoạn sau

- Clinic có thể tiếp tục sở hữu `Person` và hồ sơ Patient/Employee/Doctor trong giai đoạn này. Hợp nhất hoặc chuyển `Person` sang profile service chỉ thực hiện khi ranh giới Patient/Staff đã sẵn sàng, tránh tạo distributed transaction cho hồ sơ nghiệp vụ.
- OAuth authorization-code flow và federation với nhà cung cấp bên ngoài chưa cần cho bản chạy Docker của môn học; discovery/JWKS và JWT validation là phạm vi hiện tại. Đưa OAuth/OIDC đầy đủ vào kế hoạch nếu có client/federation bên ngoài.

---

## Giai đoạn 2: Tách Staff & Scheduling Service

**Trạng thái: Đã kiểm thử phát hành sau cutover; các luồng Staff & Scheduling, Booking Saga và Availability đã được xác nhận trên Docker và Integration Tests. 100% test cases pass; SQL Server test databases được tự động dọn dẹp sạch sẽ.**

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

### Phạm vi đã chuyển

- Đã khởi tạo `Clinic.Scheduling.Service` (.NET 10 Web API) và cơ sở dữ liệu `ClinicSchedulingDB` hoàn toàn độc lập, cấu hình JWT authentication qua discovery/JWKS của Identity Service.
- Chuyển quyền sở hữu các aggregate/entity: `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`, `SlotReservation`, `OutboxMessage`.
- Nghiệp vụ duyệt ca `ShiftRequest.Approve()` atomic trong local transaction kèm conflict detection (trùng ca trong ngày) và ghi `WorkScheduleCreated` vào bảng `OutboxMessages`.
- API Availability (`GET /schedules/availability` / `/slots`) áp dụng Dual-Constraint capacity rule (`RecommendedCapacity` + unique 15-minute slots), chặn các slot trong quá khứ và cách hiện tại dưới 30 phút.
- Bộ API Reservation (`POST /schedules/{id}/reservations`, `POST .../confirm`, `POST .../release`) với hạn 10 phút, hỗ trợ Idempotency-Key và RowLock transaction. Background service `ReservationExpirationWorker` quét mỗi 30s giải phóng slot hết hạn.
- Phía Monolith (`ClinicDB`):
  - Denormalize các cột `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate` vào bảng `Appointments`.
  - Xóa bỏ hoàn toàn Foreign Key `FK_Appointments_WorkSchedules_WorkScheduleId`.
  - Xóa bỏ hoàn toàn entity và bảng thừa `BookingSlot` trong `ClinicDB` (chuyển trọn vẹn việc quản lý slot sang Scheduling Service).
  - Triển khai Booking Saga trong `CreateAppointmentCommandHandler` (`ReserveSlot -> CreateAppointment -> ConfirmSlot / ReleaseSlot`). Phân định rạch ròi luồng Online (ràng buộc capacity) và Walk-in (linh hoạt, không chiếm slot online, được phép vượt `RecommendedCapacity`).
  - Gộp các migration của giai đoạn 2 phía Monolith thành 1 migration duy nhất: `20261021175324_DecoupleSchedulingAndRemoveBookingSlots.cs`.

### Bằng chứng kiểm thử phát hành sau cutover

- Kịch bản import dữ liệu `backend/Clinic.Scheduling.Service/Persistence/import-from-clinicdb.sql` và kịch bản đối soát `verify-scheduling-import.sql` đạt `MismatchCount = 0`.
- Unit tests `Clinic.Scheduling.Service.Tests`: 100% pass (bao gồm Capacity rule, 15m intervals, slot expiration, concurrency simulation, conflict detection).
- Integration tests `Clinic.IntegrationTests` (`BaselineBusinessWorkflowTests`): 3/3 passed.
- Tự động hóa dọn dẹp DB test tích hợp: `ApiContractFactory.DisposeAsync` dọn sạch 100% test database trên SQL Server sau khi test kết thúc (`0 rows affected` trong `sys.databases`).

### Chuyển sang giai đoạn sau (Giai đoạn 3: Tách Appointment Service)

- Cắt chuyển hoàn toàn `Appointment` và `AppointmentSnapshot` sang `ClinicAppointmentDB`.
- Chuyển `CreateAppointmentCommandHandler` và Saga từ Monolith sang Appointment Service độc lập.

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

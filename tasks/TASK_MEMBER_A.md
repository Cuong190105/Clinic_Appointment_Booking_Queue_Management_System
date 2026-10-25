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

- Clinic tiếp tục sở hữu `Patient` và các dữ liệu hồ sơ/y tế thuộc Patient trong giai đoạn này. `Employee`, `Doctor` cùng dữ liệu nghề nghiệp/lịch làm việc được chuyển quyền sở hữu sang Scheduling Service theo Giai đoạn 2; Identity tiếp tục sở hữu tài khoản, role và nhân thân cơ bản. `Person` trong Clinic chỉ được giữ trong thời gian chuyển tiếp cho đến khi các luồng phụ thuộc được chuyển hoặc thay bằng projection/event.

- OAuth authorization-code flow và federation với nhà cung cấp bên ngoài chưa cần cho bản chạy Docker của môn học; discovery/JWKS và JWT validation là phạm vi hiện tại. Đưa OAuth/OIDC đầy đủ vào kế hoạch nếu có client/federation bên ngoài.

---

## Giai đoạn 2: Tách Staff & Scheduling Service

**Trạng thái cập nhật 2026-10-25: Giai đoạn 2 đã hoàn tất. Toàn bộ ownership Staff/Scheduling đã chuyển sang Scheduling, dữ liệu đã import/đối soát, schema legacy đã được gỡ khỏi ClinicDB. Sau cập nhật các API lịch và outbox, full solution pass 273/273; frontend type-check/build và Docker build/health/smoke pass. Chi tiết nghiệm thu ở [`PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md`](PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md), mục 8.7–8.8.**

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

> Danh sách dưới đây ghi nhận các năng lực và hạ tầng đã được chuyển, không đồng nghĩa toàn bộ API quản lý Staff & Scheduling đã cutover. Phần còn mở và tiêu chí đóng giai đoạn được theo dõi trong execution plan, mục 8.

- Đã khởi tạo `Clinic.Scheduling.Service` (.NET 10 Web API) và cơ sở dữ liệu `ClinicSchedulingDB` hoàn toàn độc lập, cấu hình JWT authentication qua discovery/JWKS của Identity Service.

- Đã tạo schema/model tương ứng trong Scheduling cho `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`, `SlotReservation`, `OutboxMessage`; việc chuyển toàn bộ API và caller sang dùng các model này vẫn còn mở theo execution plan.

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

> Các nội dung trong phần này ghi lại trạng thái chuyển tiếp của Giai đoạn 3. Trong giai đoạn đó, Queue vẫn ở Monolith; Giai đoạn 4 thay thế luồng này bằng Queue Service sở hữu ticket và Queue DB riêng.

**Trạng thái: Đã kiểm thử phát hành sau cutover; `Clinic.Appointment.Service` (`ClinicAppointmentDB`) đã tiếp nhận 100% lưu lượng lịch hẹn từ Frontend qua Gateway, hoàn thiện toàn bộ Booking Saga, Reschedule Saga, Event-Driven Check-in/ExamCompleted qua RabbitMQ, cùng giao diện Bác sĩ (Medical Records) & Lễ tân (Đặt khám / Walk-in 4 bước). 100% Unit, Integration, Frontend và Live E2E Tests pass.**

> **Tài liệu kế hoạch & báo cáo nghiệm thu chi tiết**: `tasks/PHASE_3_APPOINTMENT_SERVICE_EXECUTION_PLAN_MEMBER_A.md` (Xem Mục 7)

### Nhiệm vụ

1. Tạo Appointment Service (`Clinic.Appointment.Service`, cổng `5003`, DB `ClinicAppointmentDB`).

2. Di chuyển `Appointment` và `AppointmentSnapshot` sang `ClinicAppointmentDB`.

3. Xây saga đặt lịch:

    `ReserveSlot -> CreateAppointment -> ConfirmReservation`.

4. Xử lý compensation bằng `ReleaseReservation` khi tạo appointment thất bại.

5. Lưu thông tin hiển thị denormalized trên `Appointment` (kèm bản chiếu `PatientProjections`) và giữ `AppointmentSnapshot` làm nhật ký chỉnh sửa (1-N changelog) để traceback.

6. Xây các trạng thái `Pending`, `Confirmed`, `Cancelled`, `CheckedIn`, `Completed`, `NoShow`.

7. Cung cấp API đặt lịch, đổi lịch (Reschedule Saga), hủy lịch, xác nhận lịch và check-in.

### Yêu cầu cần đạt

- Booking idempotent.

- Không overbooking khi có request đồng thời.

- Saga resume được sau khi service restart.

- Reservation được release khi bước tạo appointment thất bại.

- Appointment DB độc lập hoàn toàn với Scheduling DB và ClinicDB.

### Các quyết định thiết kế đã chốt

1. **Vai trò `AppointmentSnapshot` & Denormalized Read Model**: `AppointmentSnapshot` giữ đúng vai trò lưu nhật ký chỉnh sửa (1-N) để traceback khi người khám có vấn đề về đặt/đổi lịch (lưu kèm `OldDoctorId`, `OldDoctorName`, `OldSpecialtyName`, `OldScheduleDate`, `UpdatedByUserName` để không cần join xuyên service). Thông tin hiển thị chính thức đọc trực tiếp từ các cột denormalized trên bảng `Appointments`.

2. **Bản chiếu `PatientProjections`**: Khi chưa tách Patient Service (GĐ 5), `ClinicAppointmentDB` lưu bản chiếu nhẹ `PatientProjections` được nạp từ script import và đồng bộ qua sự kiện `UserProfileUpdated` từ Identity Service.

3. **Event-Driven Check-in**: `POST /appointments/{id}/check-in` tại Appointment Service chuyển trạng thái sang `CheckedIn` và ghi Outbox phát `AppointmentCheckedIn`. Monolith tiêu thụ sự kiện này để cấp `QueueNumber` và tạo `QueueTicket` trong `ClinicDB`.

4. **Gỡ bỏ Foreign Key trên `ClinicDB`**: Tạo migration xóa `FK_QueueTickets_Appointments_AppointmentId` và `FK_MedicalReports_Appointments_AppointmentId` trên `ClinicDB`, chuyển `AppointmentId` thành loose UUID reference.

5. **Đồng bộ `ExamCompleted`**: Appointment Service lắng nghe sự kiện `ExamCompleted` từ Queue/MedicalReports để tự động chuyển `Appointment.Status = Completed` (Eventual Consistency).

6. **Hủy lịch & Đổi lịch với Scheduling Service**: Khi hủy lịch hoặc đổi lịch, giải phóng `SlotReservation` cũ qua cả internal API (`POST /internal/slots/release-by-appointment`) và sự kiện `AppointmentCancelled` trên RabbitMQ; khi đổi lịch, thực hiện Reschedule Saga 2 pha (giữ slot mới $\r\nightarrow$ lưu snapshot & cập nhật $\r\nightarrow$ confirm slot mới & giải phóng slot cũ).

### Lưu ý

- Không dùng distributed transaction giữa Appointment và Scheduling.

- Không sửa trực tiếp dữ liệu của Scheduling Service để rollback.

- Phân biệt lỗi nghiệp vụ và lỗi tạm thời.

- Bản chiếu chỉ chứa dữ liệu hiển thị cần thiết, không sao chép toàn bộ Patient entity.

### Phạm vi đã chuyển

- **Khởi tạo Microservice & Database độc lập**: Đã khởi tạo `Clinic.Appointment.Service` (.NET 10 Web API, cổng `5003:8080`) và cơ sở dữ liệu `ClinicAppointmentDB` độc lập, xác thực JWT RS256 qua discovery/JWKS của Identity Service và chuẩn hóa `JsonStringEnumConverter`.

- **Chuyển quyền sở hữu Aggregate & Read Model**: Chuyển `Appointment` (kèm đầy đủ các cột denormalized của Bác sĩ & Bệnh nhân, cột `IdempotencyKey` có Filtered Unique Index và trạng thái `NoShow`), `AppointmentSnapshot` (1-N audit changelog traceback), `PatientProjection`, `OutboxMessage` sang `ClinicAppointmentDB`.

- **Booking Saga, Idempotency & Reschedule Saga**:
    - Triển khai Booking Saga (`ReserveSlot -> CreateAppointment -> ConfirmReservation`) kèm kiểm tra `IdempotencyKey` ở tầng bản ghi `Appointment` và cơ chế compensation `ReleaseReservation` khi bước tạo lịch hoặc xác nhận thất bại.

    - Phân định rõ luồng Online Booking (qua saga, ràng buộc slot & capacity) và Walk-in Receptionist (trực tiếp `Confirmed`/`CheckedIn`, bỏ qua capacity online).

    - Triển khai Reschedule Saga 2 pha (`Reserve new -> Reschedule & Snapshot -> Confirm new -> Release old slot & Outbox AppointmentCancelled`).

- **Event-Driven Messaging (Outbox & RabbitMQ Consumers) & Cutover Gateway**:
    - Triển khai `AppointmentOutboxPublisher` & `AppointmentEventConsumer` tại `Clinic.Appointment.Service` và `SchedulingEventConsumer` tại `Clinic.Scheduling.Service` kết nối qua RabbitMQ exchange `clinic.events`.

    - Phía Monolith (`Clinic.API` & `ClinicDB`): Áp dụng migration `20261023013218_DecoupleAppointmentForeignKeys.cs` gỡ bỏ hoàn toàn `FK_QueueTickets_Appointments_AppointmentId` và `FK_MedicalReports_Appointments_AppointmentId`; triển khai `AppointmentServiceClient` cắt chuyển toàn bộ định tuyến lịch hẹn (`/appointments/**`, `/me/appointments/**`, `/receptionist/appointments/**`, `/admin/appointments/**`) sang `Clinic.Appointment.Service`.

- **Hoàn thiện tính năng Bác sĩ & Lễ tân trên Frontend và Backend**:
    - **Bác sĩ (`/doctor/medical-records` & `/doctor/medical-records/:id`)**: Bổ sung API `GET /medical-reports` (hỗ trợ tìm kiếm từ khóa, lọc trạng thái `Draft`/`Finalized`, khoảng ngày khám, phân trang) và `PUT /medical-reports/{id}` (lưu bản nháp hoặc chốt hồ sơ kèm phát sự kiện `ExamCompleted`); xây dựng giao diện danh sách và chi tiết hồ sơ (cho phép sửa/lưu nháp/finalize đối với `Draft`, khóa chỉnh sửa và hỗ trợ in ấn `window.print()` đối với `Finalized`).

    - **Lễ tân (`/reception/booking`)**: Xây dựng quy trình đặt khám 4 bước (chọn bệnh nhân có sẵn hoặc tạo mới qua `POST /patients/add` $\r\nightarrow$ đối chiếu xác nhận thông tin $\r\nightarrow$ chọn Đặt lịch trước hoặc Đăng ký khám ngay Walk-in với API `GET /receptionist/working-doctors` và `POST /receptionist/appointments/walk-in` tự động phân bổ bác sĩ đang trực có ít ca khám nhất, tự động check-in cấp số thứ tự và gán cờ ưu tiên khẩn cấp `Priority = true`, đồng bộ tức thời sang `ClinicAppointmentDB` $\r\nightarrow$ màn hình xác nhận & in phiếu).

### Bằng chứng kiểm thử phát hành sau cutover (2026-10-23)

- **Di chuyển & Đối soát dữ liệu**: `backend/Clinic.Appointment.Service/Persistence/import-from-clinicdb.sql` và `verify-appointment-import.sql` đạt `MismatchCount = 0` (**750 Appointments, 21 Snapshots, 59 PatientProjections** khớp 100%).

- **Kiểm thử tự động (Unit & Integration Tests)**:
    - `Clinic.Appointment.Service.Tests`: **29/29 passed**.

    - `Clinic.Scheduling.Service.Tests`: **35/35 passed**.

    - `Clinic.Application.UnitTests`: **187/187 passed** (bổ sung test cho `CreateWalkInAppointment`, `GetMedicalReportsQuery`, `UpdateDraftMedicalReport`).

    - `Clinic.Infrastructure.UnitTests`: **18/18 passed**.

    - `Clinic.IntegrationTests` (`BaselineBusinessWorkflowTests`): **3/3 passed**, tự động dọn sạch 100% database test tạm trên SQL Server.

    - `Frontend`: `vue-tsc --build` đạt **0 lỗi**, `vitest` đạt **22/22 test files (94/94 tests passed)**.

- **Chạy thực tế toàn bộ 6 kịch bản E2E đứng từ Frontend (`http://localhost:3000/api/...`) và trực tiếp `5003`**:
    - **Kịch bản 1 (`GET /api/me/appointments`)**: `200 OK` (trả về 38 lịch hẹn của `patient1`, mọi `status` đều serialize dạng chuỗi chuẩn `["Confirmed", "Cancelled", "Pending", "NoShow", "Completed"]`).

    - **Kịch bản 2 (`POST /api/appointments` — Booking Saga & Idempotency)**:
        - Lần 1 (`Idempotency-Key: idemp-e2e-1792747687271`) tại Slot 1 (`19:30:00`) $\r\nightarrow$ `201 Created` (`id = 1dec3874-e72d-4d58-96ac-71185eceaa3d`, `status = Confirmed`).

        - Lần 2 (Retry cùng `Idempotency-Key`) $\r\nightarrow$ `201 Created`, trả về chính xác `1dec3874-e72d-4d58-96ac-71185eceaa3d`, không chiếm thêm slot.

        - Lần 3 (`Idempotency-Key` khác nhưng cùng Slot 1 `19:30:00`) $\r\nightarrow$ `409 Conflict` (chặn đặt trùng slot).

    - **Kịch bản 3 (`PATCH /api/appointments/{id}` — Reschedule Saga, Traceback & Giải phóng Slot cũ)**:
        - Đổi lịch `1dec3874-e72d-4d58-96ac-71185eceaa3d` từ Slot 1 (`19:30:00`) sang Slot 2 (`19:45:00`) $\r\nightarrow$ `200 OK`.

        - `GET /api/admin/appointments/1dec3874-e72d-4d58-96ac-71185eceaa3d/history` $\r\nightarrow$ `200 OK` (trả về 3 bản ghi nhật ký `changelog`: `Current State` tại `19:45:00` và `AppointmentSnapshot` lưu vết lịch cũ tại `19:30:00`).

        - `GET /api/slots` xác nhận Slot 1 (`19:30:00`) đã được giải phóng khả dụng trở lại (`slot1AvailableAgain = true`) và Slot 2 (`19:45:00`) đã được giữ chỗ (`slot2TakenNow = true`).

    - **Kịch bản 4 (`POST /api/appointments/{id}/cancel` — Hủy lịch & Giải phóng Slot)**:
        - Hủy lịch `1dec3874-e72d-4d58-96ac-71185eceaa3d` $\r\nightarrow$ `204 No Content`, `GET /api/appointments/{id}` xác nhận `status = Cancelled`.

        - `GET /api/slots` xác nhận Slot 2 (`19:45:00`) đã được giải phóng khả dụng trở lại ngay lập tức (`slot2AvailableAgain = true`).

    - **Kịch bản 5 (Luồng E2E Lễ tân Walk-in $\r\nightarrow$ Hàng đợi $\r\nightarrow$ Bác sĩ Khám & Chốt Bệnh án $\r\nightarrow$ Đồng bộ `ExamCompleted` qua RabbitMQ)**:
        - `GET /api/receptionist/working-doctors`: `200 OK` (3 bác sĩ đang trực hôm nay kèm `activeAppointmentsCount`).

        - `POST /api/patients/add`: `201 Created` (`patientId = d00c4470-34a3-47a1-a284-f80d1136c937`, đồng bộ sang `PatientProjections`).

        - `POST /api/receptionist/appointments/walk-in` (`doctorId = null`, `isUrgent = true`): `201 Created` (`appointmentId = 4220d0ce-2b8c-4e80-853e-d05f40633389`, tự động phân bổ bác sĩ ít lượt khám nhất, tự động check-in cấp `queueTicketId = 1c81910b-23d4-40c4-adfb-4c65b530c262`, `queueNumber = 4`, `isUrgent = true`, đồng bộ hồ sơ sang `ClinicAppointmentDB` với `status = CheckedIn`).

        - `POST /api/queue/call-next/{doctorId}` (`200 OK`) $\r\nightarrow$ `POST /api/queue/{ticketId}/start-exam` (`204 No Content`) $\r\nightarrow$ `POST /api/medical-reports` (`200 OK`, tạo `Draft`, `id = 53410c32-3a49-41f5-a33b-0960833add78`) $\r\nightarrow$ `PUT /api/medical-reports/{id}` (`isFinalize = false` lưu nháp `200 OK`) $\r\nightarrow$ `POST /api/queue/{ticketId}/complete-exam` (`204 No Content`) $\r\nightarrow$ `PUT /api/medical-reports/{id}` (`isFinalize = true` chốt hồ sơ `200 OK`; gọi lại `PUT` sau khi chốt trả về `400 Bad Request` chặn sửa đổi; `GET /api/medical-reports?Search=...&Status=Finalized` trả về `200 OK`).

        - Sau khi hoàn tất khám & chốt bệnh án, kiểm tra `GET /api/appointments/4220d0ce-2b8c-4e80-853e-d05f40633389` ghi nhận trạng thái lịch hẹn trên `Clinic.Appointment.Service` tự động chuyển từ `CheckedIn` sang `Completed` qua luồng sự kiện `OutboxPublisher -> RabbitMQ (clinic.events) -> AppointmentEventConsumer`.

    - **Kịch bản 6 (Admin & Receptionist Endpoints Cutover)**:
        - `GET /api/admin/appointments/summary`: `200 OK` (`totalAppointments: 760, appointmentOnlineCount: 474, completedAppointments: 203, cancelledAppointments: 256, noShowAppointments: 176`).

        - `GET /api/admin/appointments?PageNumber=1&PageSize=10`: `200 OK` (`totalCount: 10, items: 10, totalPages: 1`).

        - `GET /api/receptionist/appointments/pending`: `200 OK`.

### Chuyển sang giai đoạn sau (Giai đoạn 4: Tách Queue Service)

- Tách `QueueTicket` và bộ đếm số thứ tự (`QueueCounter`) sang `Clinic.Queue.Service` (`ClinicQueueDB`) với dữ liệu denormalized (`appointmentId`, `patientId`, `doctorId`, `queueDate`, `patientName`, `doctorName`, `priority`), giải phóng sự phụ thuộc cuối cùng của hàng đợi vào thực thể `Appointment` trong `ClinicDB`.
- Chuyển Consumer lắng nghe `AppointmentCheckedIn` từ Monolith sang `Clinic.Queue.Service`.

---

## Giai đoạn 4: Tách Queue Service

### Nhiệm vụ

1. Tạo Queue Service.

2. Di chuyển `QueueTicket` và `QueueCounter` sang Queue DB.

3. Thiết kế dữ liệu denormalized gồm `appointmentId`, `patientId`, `doctorId`, `queueDate`, `patientName`, `doctorName` và `priority`. `queueDate` là ngày địa phương của phòng khám được suy ra từ thời điểm check-in UTC theo IANA timezone cấu hình cho phòng khám (hiện mặc định `Asia/Ho_Chi_Minh`); giả định hiện tại ca đầu bắt đầu 07:00 chỉ là quy ước vận hành, không phải giới hạn kỹ thuật. Dùng `appointmentId` làm liên kết lịch hẹn/lượt khám hiện tại, không tạo `visitId` riêng.
4. Xây workflow `Waiting`, `Called`, `InProgress`, `Completed`, `Skipped`.

5. Tiêu thụ sự kiện `AppointmentCheckedIn` và cấp queue number/tạo QueueTicket trong local transaction của Queue DB; Appointment Service xác thực check-in cho appointment đúng ca/ngày đã đặt.
6. Thêm unique constraint cho `(doctorId, queueDate, queueNumber)`; counter dùng `(doctorId, queueDate)`. Không tái sử dụng số đã cấp trong ngày kể cả ticket bị hủy/soft-delete.

7. Phát `ExamStarted` và `ExamCompleted`.

### Yêu cầu cần đạt

- Không trùng queue number.

- Queue Service không truy cập Appointment DB.

- Check-in retry được.

- Duplicate event không tạo QueueTicket trùng.

- Có owner rõ ràng cho trạng thái `Completed`.
- Mỗi lần lễ tân check-in một appointment riêng; frontend chờ Queue lưu ticket thật, hiển thị và in vé đó rồi mới cho tiếp nhận/check-in người tiếp theo. Khi còn pending không hiển thị số 0, không cho in hoặc chuyển lượt; retry/in lại dùng cùng AppointmentId/ticket và không cấp số lần hai. Không gom check-in thành batch.

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

\r\n

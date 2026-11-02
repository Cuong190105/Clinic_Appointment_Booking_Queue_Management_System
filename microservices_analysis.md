# Phân Tích Kiến Trúc & Lộ Trình Chuyển Đổi Microservices

## Clinic Appointment Booking & Queue Management System

> **Phạm vi**: Khảo sát hiện trạng và xác định phương hướng chuyển bắt buộc sang microservices. Các nhận định về hiện trạng có nguồn gốc từ code thực tế.  
> **Ngày phân tích**: 2026-10-12 | **Phiên bản**: Định hướng microservices
>
> **Cập nhật theo quyết định 2026-10-26:** các mô tả ở Phần 1–2 là baseline lịch sử trước khi hoàn tất các cutover Phase 1–4; không dùng các flow monolith ở đó làm trạng thái runtime hiện tại. Các quyết định Medical Records chuẩn hiện nằm ở Phần 3.5, 4.4 và Giai đoạn 5 bên dưới: AppointmentId là encounter key; Patient và MedicalReport cùng Medical Records Service/DB; không có Patient Service riêng.

---

## Mục Lục

1. [Kiến trúc hiện tại](#1-kiến-trúc-hiện-tại)
2. [Bản đồ entity và coupling](#2-bản-đồ-entity-và-coupling)
3. [Kiến trúc microservices đích](#3-kiến-trúc-microservices-đích)
4. [Phương hướng giải quyết coupling](#4-phương-hướng-giải-quyết-coupling)
5. [Lộ trình thực hiện](#5-lộ-trình-thực-hiện)
6. [Lưu ý khi thực hiện](#6-lưu-ý-khi-thực-hiện)
7. [Câu hỏi cần nghiên cứu thêm](#7-câu-hỏi-cần-nghiên-cứu-thêm)

---

## 1. Kiến Trúc Hiện Tại

### 1.1 Cấu trúc solution

```text
Clinic_Appointment_Booking_Queue_Management_System/
├── backend/
│   ├── Clinic.Domain
│   ├── Clinic.Application
│   ├── Clinic.Infrastructure.Sqlserver
│   ├── Clinic.API
│   ├── Clinic.Application.UnitTests
│   └── Clinic.Domain.UnitTests
├── frontend/                  Vue.js SPA
└── docker-compose.yml         MSSQL, backend, frontend
```

Đã xác minh:

- Clean Architecture gồm Domain, Application, Infrastructure và API.
- CQRS dùng MediatR, Repository Pattern và Unit of Work.
- Một `ApplicationDbContext` quản lý khoảng 20 entity trong một database.
- Một chuỗi kết nối SQL Server dùng chung cho toàn bộ schema.
- `NotificationWorker` và `ScheduledNotificationWorker` chạy trong process API.
- `INotificationQueue` dùng `Channel<T>` singleton in-memory.
- JWT và permission authorization được xử lý trong API.

### 1.2 Các luồng có coupling cao

**Đặt lịch**: `CreateAppointmentHandler` đọc Patient và WorkSchedule, mở transaction lock, kiểm tra capacity, tạo Appointment rồi commit. Việc kiểm tra slot và tạo appointment hiện nằm trong một transaction.

**Check-in**: `CheckInHandler` đọc Appointment cùng WorkSchedule, cấp queue number, gọi `Appointment.CheckIn()` để tạo `QueueTicket`, sau đó lưu ticket. Đây là coupling trực tiếp giữa Appointment và Queue.

**Hoàn tất khám**: `CompleteExamHandler` cập nhật QueueTicket và Appointment trong cùng một `SaveChangesAsync`. `SaveMedicalReportHandler` cũng có thể finalize MedicalReport và hoàn tất QueueTicket/Appointment.

**Duyệt ca**: `ShiftRequest.Approve()` đổi trạng thái ShiftRequest, tạo WorkSchedule và gọi `Doctor.AddWorkSchedule()` atomically.

**Thông báo**: nhiều command inject `INotificationQueue`; job chứa `Person` domain entity. Notification chưa được persist trước khi đưa vào channel.

---

## 2. Bản Đồ Entity Và Coupling

### 2.1 Ownership hiện tại

| Nhóm            | Entity chính                             | Coupling quan trọng                       |
| --------------- | ---------------------------------------- | ----------------------------------------- |
| Identity        | User, Role, Permission, RefreshToken     | Person, UserRole, RolePermission          |
| Staff           | Employee, Doctor, Specialty, WorkHistory | Person, User                              |
| Scheduling      | WorkSchedule, ShiftRequest               | Doctor, Appointment                       |
| Appointment     | Appointment, AppointmentSnapshot         | Patient, WorkSchedule, QueueTicket        |
| Queue           | QueueTicket, QueueCounter                | Appointment, WorkSchedule, Doctor         |
| Medical Records | Patient, MedicalReport, MedicalReportAmendment, MedicalAccessAudit | QueueTicket, Appointment, Doctor |
| Notification    | Notification                             | Person và các notification handler        |

### 2.2 Coupling hotspots

1. **Appointment - QueueTicket: rất cao**. `Appointment.CheckIn()` tạo QueueTicket; cancel và complete có thể tác động cả hai entity.
2. **WorkSchedule - Appointment: cao**. Slot conflict và patient limit cần được kiểm tra cùng lúc với booking.
3. **Doctor - WorkSchedule - ShiftRequest: trung bình-cao**. Approve request tạo schedule và kiểm tra conflict trong một operation.
4. **QueueTicket - MedicalReport: trung bình (legacy coupling, mục tiêu đã chốt)**. Clinic hiện còn API orchestration qua Queue/Appointment; Phase 5 dùng AppointmentId và local context/projection, không truy vấn Queue để dựng report.
5. **Person - mọi module: trung bình**. Person đang là bảng trung tâm của User, Employee, Patient và Notification.
6. **Notification queue: trung bình về dữ liệu, cao về reliability**. Restart process có thể làm mất job chưa gửi.

### 2.3 Nguyên tắc tách

- Mỗi service sở hữu database, migration và repository riêng.
- Không dùng foreign key, EF entity, navigation property hoặc `ApplicationDbContext` xuyên service.
- Chỉ trao đổi ID, DTO hoặc versioned event.
- Service sở hữu aggregate là service duy nhất được quyền ghi aggregate đó.
- Giao dịch ACID chỉ áp dụng trong một service; cross-service workflow dùng reservation, saga và outbox.

---

## 3. Kiến Trúc Microservices Đích

Đây là kiến trúc mục tiêu duy nhất của hệ thống. Các service được triển khai độc lập, có database riêng và giao tiếp qua API Gateway, synchronous API và message broker.

### 3.1 Identity Service

Sở hữu `User`, `Role`, `Permission`, `UserRole`, `RolePermission` và `RefreshToken`.

- Phụ trách đăng ký, đăng nhập, refresh token, permission và phát JWT/OIDC token.
- Các service khác validate token bằng public key/JWKS.
- Không gọi Identity Service cho mỗi request.
- Không chia sẻ Identity database cho service khác.

### 3.2 Staff & Scheduling Service

Sở hữu `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `ShiftRequest` và `WorkSchedule`.

- Giữ `ShiftRequest.Approve()` trong một local transaction.
- Cung cấp `GetAvailability` cho query.
- Cung cấp `ReserveSlot` và `ReleaseSlot` cho command.
- Phát `WorkScheduleCreated`, `WorkScheduleUpdated`, `WorkScheduleCancelled`.

Service này không chỉ trả lời "còn slot không". Nó phải sở hữu thao tác giữ capacity atomic để tránh overbooking.

### 3.3 Appointment Service

Sở hữu `Appointment`, `AppointmentSnapshot` và dữ liệu Patient/Profile cần thiết cho appointment.

- Phụ trách đặt, xác nhận, đổi và hủy lịch.
- Giao tiếp với Scheduling bằng reservation API, không đọc database của Scheduling.
- Phát `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn`.
- Lưu snapshot tên/liên hệ cần hiển thị để không phải join dữ liệu xuyên service.

### 3.4 Queue Service

Sở hữu `QueueTicket`, `QueueCounter` và workflow của lượt khám.

- Phụ trách check-in, gọi số, skip, bắt đầu và kết thúc khám.
- Lưu denormalized `appointmentId`, `patientId`, `doctorId`, `scheduleDate`.
- Cấp queue number trong local transaction.
- Phát `PatientCheckedIn`, `PatientCalled`, `ExamStarted`, `ExamCompleted`.

Queue Service không truy cập chuỗi `QueueTicket -> Appointment -> WorkSchedule -> Doctor`.

### 3.5 Medical Records Service

Sở hữu `Patient`, `MedicalReport`, lịch sử amendment và audit truy cập trong cùng service/database. Không có Patient Service riêng.

- Dùng `AppointmentId` làm định danh lượt khám hiện tại và lâu dài; không tạo `visitId`/`encounterId` riêng.
- Lưu `appointmentId` dạng loose reference, không tạo FK hay navigation sang Appointment DB.
- Chỉ nhận `appointmentId`, `patientId`, `doctorId` và snapshot tối thiểu cần thiết.
- Consume `ExamStarted` và `ExamCompleted`.
- Không inject Queue repository và không có navigation property đến QueueTicket.
- `ExamStarted` mở encounter context idempotent; Draft nội dung được tạo khi bác sĩ bắt đầu ghi hồ sơ.
- `ExamCompleted` chỉ đánh dấu lượt khám kết thúc; không finalize report. Draft/Finalized là lifecycle riêng.
- Hồ sơ Finalized không sửa trực tiếp; amendment được ghi append-only, có tác giả, thời điểm, lý do và liên kết phiên bản trước.

### 3.6 Notification Service

Sở hữu notification persistence, inbox/idempotency, delivery outbox, delivery attempt, email/SMS/in-app và SignalR delivery. Clinic API tiếp tục là Gateway/BFF; không tạo Gateway service riêng ở Phase 6.

- Consume business event, không nhận `Person` hoặc entity từ service khác; dùng `RecipientId`, `RecipientType` và contact snapshot tối thiểu do service sở hữu recipient phát ra.
- Thay `Channel<T>` bằng durable inbox/outbox và broker consumer. Notification DB là nguồn lịch sử in-app; SignalR chỉ là hint để client tải lại từ Gateway.
- Có retry hữu hạn với exponential backoff, dead-letter queue, idempotency và delivery audit cho từng channel/attempt.
- Người có tài khoản có in-app inbox theo policy; khách chưa có tài khoản chỉ nhận email/SMS, không dùng in-app hoặc SignalR.
- Bỏ notification cancellation cho `AppointmentCancelled` mang cờ reschedule-release. Password reset dùng contract/template riêng; token không xuất hiện trong log hay event business chung.
- SignalR chạy một instance trên Compose hiện tại; chỉ thêm Redis backplane khi chạy nhiều instance.

### 3.7 API Gateway và nền tảng chung

API Gateway dùng YARP, Nginx hoặc Ocelot để route request, xử lý CORS, rate limiting, correlation ID và JWT validation ở biên. Mỗi service vẫn phải validate JWT riêng; Gateway không phải điểm tin cậy duy nhất.

Nền tảng dùng message broker như RabbitMQ hoặc Azure Service Bus, OpenTelemetry cho tracing và Redis khi cần SignalR backplane hoặc distributed lease.

### 3.8 Sơ đồ giao tiếp

```text
Frontend -> API Gateway
              |
              +-> Identity Service
              +-> Appointment Service <-> Staff & Scheduling Service
              +-> Queue Service
              +-> Medical Records Service
              +-> Notification Service

All services -> Message Broker -> Outbox/Consumers
Each service -> Its own database
```

---

## 4. Phương Hướng Giải Quyết Coupling

### 4.1 Appointment và Scheduling: reservation saga

Không dùng flow nguy hiểm `CheckAvailability` rồi `CreateAppointment`. Dùng:

```text
Appointment Service -> ReserveSlot(idempotencyKey)
Scheduling Service -> giữ capacity trong local transaction
Appointment Service -> tạo appointment
Appointment Service -> ConfirmReservation hoặc ReleaseReservation
```

Reservation cần timeout, retry, idempotency và compensation. Unique constraint hoặc lock chỉ nằm trong Scheduling database.

### 4.2 Appointment và Queue: tách ownership, không cố giữ distributed transaction

Appointment và Queue vẫn được tách thành hai service dù coupling nghiệp vụ cao. Mỗi service sở hữu trạng thái riêng:

```text
Queue Service: Waiting -> Called -> InProgress -> Completed
Appointment Service: Pending -> Confirmed -> CheckedIn -> Completed
Medical Records Service: Draft -> Finalized
```

Queue Service phát `ExamCompleted`; Appointment Service consume event để chuyển trạng thái Appointment. Nếu consumer lỗi, message được retry và reconciliation xử lý. Không rollback transaction đã commit ở service khác.

`CompleteExam` không được để cả Queue và Appointment cùng sửa một aggregate. Mỗi trạng thái phải có một owner duy nhất.

### 4.3 Check-in và QueueCounter

`AppointmentCheckedIn` phải chứa `appointmentId`, `patientId`, `doctorId` và snapshot tối thiểu cần cho Queue. `AppointmentId` cũng là định danh lượt khám; không thêm `visitId`. Queue Service dùng dữ liệu event để cấp số, không gọi ngược Appointment Service để đọc graph.

Trước khi extract Queue Service, cần test concurrency cho nhiều check-in cùng bác sĩ/ngày, rollback khi tạo ticket thất bại và uniqueness của queue number. Không kết luận `MERGE ... HOLDLOCK` an toàn nếu chưa xác minh transaction boundary thực tế.

### 4.4 Medical Records và AppointmentId

Không dùng `QueueTicketId` làm liên kết xuyên service lâu dài. Dùng `AppointmentId` làm khóa encounter lâu dài trong phạm vi hiện tại và tương lai. Queue ticket chỉ là mã ticket của Queue; không được dùng làm khóa hồ sơ.

```text
ExamStarted(AppointmentId) -> Medical Records mở encounter context idempotent
SaveReport(AppointmentId) -> Medical Records tạo/cập nhật Draft
ExamCompleted(AppointmentId) -> Medical Records đánh dấu encounter đã kết thúc
FinalizeReport(AppointmentId) -> Medical Records chuyển report Draft sang Finalized
```

Medical Records có thể hoạt động khi Queue Service tạm thời không khả dụng vì dữ liệu cần thiết đã nằm trong event hoặc projection local.
`ExamCompleted` không finalize hồ sơ; hoàn thành khám và finalize bệnh án là hai trạng thái độc lập.

### 4.5 Person và dữ liệu hồ sơ

Không duy trì một `Person` entity dùng chung giữa mọi service.

- Identity sở hữu tài khoản và credential.
- Staff sở hữu hồ sơ nhân viên/bác sĩ.
- Medical Records sở hữu Patient profile và dữ liệu bệnh nhân; Identity là SSOT cho account/basic person profile, đồng bộ projection qua event.
- Notification chỉ nhận recipient ID và contact snapshot cần gửi.

Event không được chứa domain entity, navigation object hoặc dữ liệu cá nhân không cần thiết.

### 4.6 Notification và reliability

Business transaction phải ghi outbox record cùng local transaction. Outbox publisher gửi message ít nhất một lần; consumer dùng inbox/idempotency để không xử lý trùng.

Mỗi event cần có `eventId`, `eventType`, `schemaVersion`, `occurredAt`, `correlationId` và `causationId`.

### 4.7 Read model và API composition

Màn hình cần dữ liệu từ nhiều service dùng read model hoặc API composition ở Gateway/BFF. Không join database, không query trực tiếp bảng service khác và không expose EF entity ra API.

---

## 5. Lộ Trình Thực Hiện

### Giai đoạn 0: Chuẩn hóa contract và baseline

1. Chốt ownership entity, service boundary và database boundary.
2. Viết API/event contract có version, error model và idempotency key.
3. Bổ sung integration test cho booking, check-in, call-next, complete-exam và approve shift.
4. Bổ sung correlation ID, structured logging, health check, metrics và distributed tracing.
5. Tạo outbox/inbox schema và message broker trong Docker Compose.
6. Ghi migration, dual-read/dual-write và rollback plan cho từng service.

**Tiêu chí hoàn thành**: contract test, concurrency test cho booking/queue và dashboard theo dõi message failure hoạt động.

### Giai đoạn 1: Tách Identity Service

1. Di chuyển User, Role, Permission và RefreshToken sang database riêng.
2. Identity phát JWT/OIDC; service khác validate bằng public key/JWKS.
3. Thay dependency trực tiếp vào User/Person bằng `userId` và claims.
4. Chạy song song endpoint auth cũ và mới, sau đó chuyển Gateway/frontend.
5. Thu hồi endpoint cũ sau khi regression test pass.

**Tiêu chí hoàn thành**: Không service nào truy cập Identity database; login, refresh, revoke và permission test pass.

### Giai đoạn 2: Tách Staff & Scheduling Service

1. Di chuyển Staff/Scheduling schema và migration riêng.
2. Giữ `ShiftRequest.Approve()` và conflict detection trong local transaction.
3. Thêm `GetAvailability`, `ReserveSlot` và `ReleaseSlot`.
4. Appointment chỉ nhận schedule ID, doctor ID, ngày và capacity cần thiết.
5. Phát schedule events qua outbox.

**Tiêu chí hoàn thành**: Concurrent reservation không vượt capacity; không còn join hoặc foreign key tới Staff database.

### Giai đoạn 3: Tách Appointment Service

1. Di chuyển Appointment và AppointmentSnapshot sang database riêng.
2. Thực hiện saga `ReserveSlot -> CreateAppointment -> ConfirmReservation`.
3. Xử lý lỗi bằng `ReleaseReservation`, timeout và retry.
4. Phát appointment events và xây read model cho lịch bệnh nhân.
5. Thay frontend gọi trực tiếp backend cũ bằng Gateway route.

**Tiêu chí hoàn thành**: Booking idempotent, không overbook khi concurrent và saga resume được sau restart.

### Giai đoạn 4: Tách Queue Service

1. Di chuyển QueueTicket và QueueCounter sang database riêng.
2. Queue consume `AppointmentCheckedIn` hoặc nhận command qua Gateway.
3. Lưu denormalized appointment/patient/doctor/schedule data trong Queue DB.
4. Dùng local transaction và unique constraint cho queue number.
5. Phát `ExamStarted` và `ExamCompleted`.
6. Xây reconciliation job giữa queue state và appointment state.

**Tiêu chí hoàn thành**: Không trùng queue number, duplicate event không tạo ticket trùng và Queue không truy cập Appointment DB.

### Giai đoạn 5: Tách Medical Records Service (Patient cùng ownership)

1. Dùng `AppointmentId` làm encounter key; không phát sinh `visitId`/`encounterId`.
2. Di chuyển Patient, MedicalReport, amendment history và audit record vào Medical Records DB riêng.
3. Consume `ExamStarted`/`ExamCompleted` idempotently theo `AppointmentId`.
4. Tách authorization và audit log của hồ sơ bệnh án.
5. Cung cấp truy vấn lịch sử bệnh nhân từ Medical Records Service; không tạo Patient Service riêng.

**Tiêu chí hoàn thành**: Report không phụ thuộc Queue repository; duplicate/out-of-order event được xử lý an toàn.

### Giai đoạn 6: Tách Notification Service và hoàn thiện Gateway

1. Tạo Notification DB riêng, migrate/reconcile lịch sử cần giữ từ ClinicDB, rồi tắt persistence notification cũ.
2. Thay channel in-memory bằng durable inbox/outbox, event policy và broker consumer idempotent.
3. Chuyển template/provider, scheduled reminder, delivery audit và password-reset delivery riêng sang service mới.
4. Chuyển SignalR Hub vào service; persistence là nguồn chuẩn, SignalR là hint. Một instance ở Compose hiện tại; Redis khi scale nhiều instance.
5. Clinic API tiếp tục làm Gateway và route toàn bộ REST/SignalR traffic từ frontend; mỗi service tự xác thực JWT.
6. Chạy failure/recovery tests cho broker, provider, service restart và client reconnect.

**Tiêu chí hoàn thành**: Restart không làm mất notification; duplicate event không nhân bản side effect; delivery attempt truy vết được; frontend chỉ dùng Gateway; không còn runtime dependency vào `Person`/Clinic notification persistence.

---

## 6. Lưu Ý Khi Thực Hiện

### 6.1 Không chia sẻ database

Database dùng chung chỉ có thể là bước chuyển tiếp có kiểm soát. Mục tiêu cuối cùng là mỗi service sở hữu database riêng. Service mới không được query trực tiếp bảng của service khác.

### 6.2 Migration theo Strangler Fig

```text
contract -> shadow read -> dual write có kiểm soát -> chuyển read -> chuyển write -> tắt đường cũ
```

Mỗi bounded context chuyển trong release riêng, có feature flag, migration backward-compatible và rollback plan.

### 6.3 Eventual consistency có kiểm soát

Chấp nhận eventual consistency cho notification, dashboard, medical history projection và Appointment sau `ExamCompleted`. Không chấp nhận overbooking hoặc trùng queue number.

Mọi eventual consistency phải có retry, dead-letter queue, idempotency và reconciliation report.

### 6.4 Retry và idempotency

Các command sau bắt buộc có idempotency key:

- Reserve/release slot
- Create appointment
- Check-in
- Create/finalize medical report
- Send notification

Retry chỉ áp dụng cho lỗi transient. Lỗi nghiệp vụ không được retry vô hạn.

### 6.5 Bảo mật và dữ liệu cá nhân

- Mỗi service validate JWT; Gateway không phải điểm tin cậy duy nhất.
- Dùng access token ngắn hạn, JWKS và permission version/revocation.
- Không đưa dữ liệu bệnh án hoặc contact không cần thiết vào event.
- Audit mọi truy cập Medical Records.

### 6.6 Vận hành

Mỗi service cần health/readiness/liveness endpoint, structured log, trace ID, metrics latency/error/retry/consumer lag và graceful shutdown.

Docker Compose cần health check, persistent volume, resource limit, dependency ordering và retry kết nối. `depends_on` không thay thế health check hoặc retry.

### 6.7 Kiểm thử bắt buộc

- Unit test domain invariant của từng service.
- Integration test với database thật cho constraint, transaction và concurrency.
- Contract test cho API và event.
- Failure test khi broker restart, consumer crash sau commit, timeout, duplicate/out-of-order event.
- E2E test cho booking, check-in, exam completion và notification.

### 6.8 Các quyết định cần chốt

1. RabbitMQ hay Azure Service Bus.
2. YARP hay Nginx cho Gateway.
3. Giữ SQL Server hay chọn database riêng theo service.
4. Saga owner cho reservation và exam completion.
5. Định danh encounter đã chốt: `AppointmentId`; không tạo `visitId`/`encounterId` riêng.
6. TTL và chính sách thu hồi JWT.
7. SLA, retry policy và retention cho từng event.

---

## 7. Câu Hỏi Cần Nghiên Cứu Thêm

1. Hệ thống phục vụ bao nhiêu bệnh nhân/ngày và bao nhiêu concurrent users để sizing service, broker và database?
2. SLA riêng của Queue, Appointment, Identity và Notification là bao nhiêu?
3. Hệ thống phục vụ một cơ sở hay nhiều cơ sở; có cần tenant ID trong mọi aggregate không?
4. **Đã chốt 2026-10-26:** Patient và MedicalReport cùng thuộc Medical Records Service/DB; không có Patient Service riêng. Identity vẫn là SSOT cho tài khoản/person profile cơ bản.
5. `SmsSender` đã gửi SMS thật hay vẫn là stub?
6. Scheduled notification dùng lease/lock nào khi chạy nhiều instance?
7. SQL Server `MERGE ... HOLDLOCK` có transaction boundary nào trong luồng check-in hiện tại?
8. **Đã quyết định cho Phase 6:** frontend dùng SignalR client qua Clinic API Gateway; giữ REST history làm recovery path. Chạy một instance hiện tại, Redis backplane chỉ khi scale nhiều instance.
9. Permission deprecated như `appointment.cancel` được mapping sang permission mới thế nào khi tách Identity?
10. Retention, mã hóa và quyền truy cập audit cho Medical Records là gì?

---

_Tài liệu này giả định quyết định chuyển sang microservices đã được thông qua. Trọng tâm của tài liệu là giảm coupling, bảo toàn business invariant và thực hiện chuyển đổi theo từng bounded context có thể rollback._

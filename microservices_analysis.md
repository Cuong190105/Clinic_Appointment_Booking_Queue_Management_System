# Phân Tích Kiến Trúc & Lộ Trình Chuyển Đổi Microservices
## Clinic Appointment Booking & Queue Management System

> **Phạm vi**: Chỉ khảo sát, không sửa code. Mọi nhận định đều có nguồn gốc từ code thực tế.  
> **Ngày phân tích**: 2026-10-12 | **Phiên bản**: Phân tích ban đầu

---

## Mục Lục

1. [Kiến trúc hiện tại](#1-kiến-trúc-hiện-tại)
2. [Bản đồ Entity & Quan hệ](#2-bản-đồ-entity--quan-hệ)
3. [Phân tích module & điểm kết nối](#3-phân-tích-module--điểm-kết-nối)
4. [Các phương án kiến trúc](#4-các-phương-án-kiến-trúc)
5. [Phân tích vấn đề khi phân tán](#5-phân-tích-vấn-đề-khi-phân-tán)
6. [So sánh các phương án](#6-so-sánh-các-phương-án)
7. [Lộ trình chuyển đổi](#7-lộ-trình-chuyển-đổi)
8. [Câu hỏi cần nghiên cứu thêm](#8-câu-hỏi-cần-nghiên-cứu-thêm)

---

## 1. Kiến Trúc Hiện Tại

### 1.1 Cấu trúc Solution

```
Clinic_Appointment_Booking_Queue_Management_System/
├── backend/
│   ├── Clinic.Domain            ← Entities, Enums, Domain logic
│   ├── Clinic.Application       ← CQRS Handlers (MediatR), Interfaces, Notifications
│   ├── Clinic.Infrastructure.Sqlserver  ← EF Core, Repositories, Email/SMS
│   ├── Clinic.API               ← Controllers, Workers, Auth, SignalR Hubs
│   ├── Clinic.Application.UnitTests
│   └── Clinic.Domain.UnitTests
├── frontend/                    ← Vue.js (inferred)
└── docker-compose.yml           ← 3 services: db (MSSQL), backend, frontend
```

**Nhận xét (đã xác minh)**:
- Kiến trúc Clean Architecture với 4 tầng rõ ràng.
- Pattern: CQRS (MediatR) + Repository Pattern + Unit of Work.
- Tầng Domain có business logic phong phú (DDD-lite), không phụ thuộc framework.
- **1 DbContext duy nhất** (`ApplicationDbContext`) quản lý tất cả 20 entity.
- **1 chuỗi kết nối** tới SQL Server, chứa toàn bộ schema.
- **2 Background Workers** chạy trong-process: `NotificationWorker`, `ScheduledNotificationWorker`.
- **1 SignalR Hub** (`NotificationHub`) cho real-time in-app notification.
- Notification queue dùng `Channel<T>` in-memory (singleton), KHÔNG persisted.

### 1.2 Sơ đồ kiến trúc hiện tại

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLINIC MONOLITH                              │
│                                                                       │
│  ┌─── Clinic.API ──────────────────────────────────────────────────┐ │
│  │  Controllers: Auth, Appointments, Queue, Doctors, Shifts,        │ │
│  │               Employees, Patients, MedicalReports,               │ │
│  │               Specialties, Receptionist, Admin, Me               │ │
│  │  Workers: NotificationWorker, ScheduledNotificationWorker        │ │
│  │  Hubs: NotificationHub (SignalR)                                 │ │
│  │  Auth: JWT + Permission-based (custom IAuthorizationHandler)     │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│           │ MediatR (ISender)                                         │
│  ┌─── Clinic.Application ─────────────────────────────────────────┐  │
│  │  Features/: Auth, Appointments, Queue, Doctors, Employees,      │  │
│  │             Patients, MedicalReports, Specialties, WorkSchedules│  │
│  │             Slots, Admin, Users                                  │  │
│  │  Notifications/: 4 Handlers (Account, Appointment, Custom, Due) │  │
│  │  Services: UserService, PersonService, TokenService,            │  │
│  │            NotificationService, NotificationQueue (in-memory)   │  │
│  └─────────────────────────────────────────────────────────────────┘ │
│           │ Interfaces (IRepository, IUnitOfWork)                     │
│  ┌─── Clinic.Infrastructure.Sqlserver ────────────────────────────┐  │
│  │  Persistence: ApplicationDbContext (1 DbContext, 20 DbSets)    │  │
│  │  Repositories: 15 repositories (tất cả cùng 1 DbContext)       │  │
│  │  Notifications: EmailSender (SMTP), SmsSender (stub)           │  │
│  │  Auth: TokenProvider, PasswordHasher                            │  │
│  └─────────────────────────────────────────────────────────────────┘ │
│           │ EF Core                                                    │
│  ┌─── SQL Server (MSSQL 2022) ─────────────────────────────────── ┐  │
│  │  1 database, toàn bộ bảng                                       │  │
│  └─────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
         ↑ REST API + SignalR
┌─────────────────┐
│   Frontend      │
│  (Vue.js SPA)   │
└─────────────────┘
```

---

## 2. Bản Đồ Entity & Quan Hệ

### 2.1 Danh sách 20 Entity (đã xác minh từ ApplicationDbContext)

| Entity | Vai trò | Quan hệ trực tiếp |
|---|---|---|
| `Person` | Thông tin cá nhân dùng chung | User (0..1), Employee (0..1), Patient (0..1), Notifications (collection) |
| `User` | Tài khoản đăng nhập | Person (1), UserRoles (collection), RefreshTokens (collection) |
| `Role` | Vai trò (Admin/Doctor/Patient/Receptionist) | RolePermissions (collection) |
| `Permission` | Quyền chi tiết | RolePermissions (collection) |
| `UserRole` | Join table User-Role | User, Role |
| `RolePermission` | Join table Role-Permission | Role, Permission |
| `RefreshToken` | Token làm mới JWT | User |
| `Employee` | Thông tin nghiệp vụ nhân viên | Person (1), Doctor (0..1), Manager (self-ref) |
| `Doctor` | Thông tin chuyên môn bác sĩ | Employee (1), WorkSchedules, WorkHistories, ShiftRequests |
| `Specialty` | Chuyên khoa | WorkHistories (collection) |
| `WorkHistory` | Lịch sử chuyên khoa của bác sĩ | Doctor, Specialty |
| `WorkSchedule` | Ca làm việc của bác sĩ | Doctor (1), Appointments (collection) |
| `ShiftRequest` | Yêu cầu xin ca của bác sĩ | Doctor (1) |
| `Patient` | Hồ sơ bệnh nhân | Person (1), Appointments (collection) |
| `Appointment` | Lịch hẹn khám | Patient, WorkSchedule, QueueTicket (0..1), Snapshots |
| `AppointmentSnapshot` | Lịch sử thay đổi lịch hẹn | Appointment |
| `QueueTicket` | Số thứ tự hàng đợi | Appointment (1), MedicalReport (0..1) |
| `QueueCounter` | Bộ đếm số thứ tự hàng đợi theo bác sĩ/ngày | (raw SQL MERGE) |
| `MedicalReport` | Phiếu khám bệnh | QueueTicket (1) |
| `Notification` | Thông báo (in-app/email/SMS) | Person |

### 2.2 Chuỗi quan hệ nghiệp vụ chính (đã xác minh)

```
Person ─── User ─── UserRole ─── Role ─── RolePermission ─── Permission
  │
  ├── Employee ─── Doctor ─── WorkSchedule ─── Appointment ─── QueueTicket ─── MedicalReport
  │                   │             │               │
  │              WorkHistory    ShiftRequest    AppointmentSnapshot
  │              Specialty
  │
  └── Patient ─────────────── Appointment (PatientId)
```

**Điểm quan trọng (đã xác minh)**:
- `MedicalReport` truy cập Doctor thông qua chuỗi dài: `QueueTicket → Appointment → WorkSchedule → Doctor`.
- `Appointment.CheckIn()` tạo `QueueTicket` trực tiếp trong Domain Entity (coupling mạnh).
- `CompleteExam` handler cập nhật cả `QueueTicket` và `Appointment` trong 1 transaction.
- `ShiftRequest.Approve()` tạo `WorkSchedule` mới và gọi `Doctor.AddWorkSchedule()` trong 1 atomic operation.
- `QueueCounter` dùng SQL Server `MERGE` statement với `HOLDLOCK` — race condition được handle tại DB level.

---

## 3. Phân Tích Module & Điểm Kết Nối

### 3.1 Các luồng nghiệp vụ chính (đã xác minh từ handlers)

#### Luồng 1: Đặt lịch hẹn
```
Patient → POST /appointments
  → CreateAppointmentHandler
    → PatientRepository.GetPatientByUserIdAsync()
    → UnitOfWork.InitializeTransactionLockAsync()  [Serializable transaction]
    → WorkScheduleRepository.GetByIdAsync()
    → new Appointment(patient, workSchedule, ...)
    → WorkSchedule.AddAppointment(appointment)      [kiểm tra Full, conflict slot]
    → AppointmentRepository.AddAsync()
    → UnitOfWork.SaveChangesAsync() + CommitTransaction()
    → [Notification enqueued, NOT in transaction]
```

#### Luồng 2: Xác nhận lịch hẹn (Receptionist)
```
Receptionist → POST /appointments/{id}/confirm
  → ConfirmAppointmentHandler
    → AppointmentRepository.GetByIdAsync() [includes WorkSchedule, Patient.Person]
    → Appointment.Confirm()
    → UnitOfWork.SaveChangesAsync()
    → NotificationQueue.EnqueueAsync(AppointmentConfirmation)  [fire-and-forget]
    → NotificationService.ScheduleAsync(AppointmentReminder, 1 ngày trước 8AM)
```

#### Luồng 3: Check-in vào hàng đợi (Receptionist)
```
Receptionist → POST /appointments/{id}/check-in
  → CheckInHandler
    → AppointmentRepository.GetByIdAsync()
    → QueueTicketRepository.GetNextQueueNumberAsync()  [SQL MERGE HOLDLOCK]
    → Appointment.CheckIn(checkInTime, queueNumber)    [tạo QueueTicket trong Domain]
    → QueueTicketRepository.AddAsync()
    → UnitOfWork.SaveChangesAsync()
```

#### Luồng 4: Gọi bệnh nhân vào khám (Doctor/Receptionist)
```
Doctor → POST /queue/call-next/{doctorId}
  → CallNextQueueHandler
    → QueueTicketRepository.GetActiveTicketAsync()   [check không có vé đang khám]
    → retry loop (MaxRetries=5):
      → QueueTicketRepository.GetNextWaitingAsync()
      → QueueTicket.Call()
      → UnitOfWork.SaveChangesAsync()               [RowVersion concurrency check]
      → catch ConcurrencyConflictException → retry
```

#### Luồng 5: Hoàn thành khám (Doctor)
```
Doctor → POST /queue/{id}/complete-exam
  → CompleteExamHandler
    → QueueTicketRepository.GetByIdAsync()
    → CurrentUser.HasPermission("queue.complete-exam.any")  [permission check]
    → QueueTicket.Complete()
    → Appointment.Complete()                  [cập nhật cả 2 trong 1 transaction]
    → UnitOfWork.SaveChangesAsync()
```

#### Luồng 6: Duyệt ca làm việc (Admin/Receptionist)
```
Admin → POST /shifts/suggestions/{id}/approve
  → ApproveRequestHandler
    → ShiftRequestRepository.GetByIdAsync()
    → ShiftRequest.Approve()
      → new WorkSchedule(Doctor, date, ...)   [tạo WorkSchedule trong Domain]
      → Doctor.AddWorkSchedule(workSchedule)  [kiểm tra conflict trong Domain]
    → UnitOfWork.SaveChangesAsync()
```

### 3.2 Phân tích độ kết dính và phụ thuộc chéo

#### Ma trận phụ thuộc dữ liệu (đã xác minh từ code)

| Module nghiệp vụ | Entity sở hữu | Phụ thuộc entity khác |
|---|---|---|
| **Identity/Auth** | User, Person, Role, Permission, RefreshToken | — |
| **Staff Management** | Employee, Doctor, Specialty, WorkHistory | Person, User |
| **Scheduling** | WorkSchedule, ShiftRequest | Doctor |
| **Appointment** | Appointment, AppointmentSnapshot | Patient, WorkSchedule (→ Doctor) |
| **Queue Management** | QueueTicket, QueueCounter | Appointment (→ Patient, WorkSchedule, Doctor) |
| **Medical Records** | MedicalReport | QueueTicket (→ Appointment → WorkSchedule → Doctor, Patient) |
| **Notifications** | Notification | Person |
| **Patient** | Patient | Person |

#### Các điểm kết nối cao (coupling hotspots) — đã xác minh

**1. Appointment ↔ QueueTicket (VERY HIGH)**
- `Appointment.CheckIn()` tạo `QueueTicket` trực tiếp trong Domain method.
- `CompleteExamHandler` cập nhật `QueueTicket` + `Appointment` trong 1 transaction.
- `Appointment.AdminCancel()` cascade cancel `QueueTicket` nếu Waiting/Called.
- **Kết luận**: Hai entity này không thể tách ra 2 service riêng mà không phá vỡ tính nhất quán.

**2. WorkSchedule ↔ Appointment (HIGH)**
- `Appointment` luôn thuộc 1 `WorkSchedule`, slot conflict check trong `WorkSchedule.AddAppointment()`.
- `WorkSchedule.Status = Full` được cập nhật tự động khi thêm appointment.
- `CreateAppointmentHandler` dùng Serializable transaction để tránh race condition.

**3. Doctor ↔ WorkSchedule ↔ ShiftRequest (MEDIUM-HIGH)**
- `ShiftRequest.Approve()` tạo `WorkSchedule` mới và gọi `Doctor.AddWorkSchedule()` atomically.
- Conflict detection trong `Doctor.AddWorkSchedule()` cần đọc toàn bộ WorkSchedules của doctor.

**4. QueueTicket ↔ MedicalReport (MEDIUM)**
- `MedicalReport` chỉ gắn với `QueueTicket` (1-0..1).
- Truy cập Doctor phải đi qua chuỗi: `QueueTicket → Appointment → WorkSchedule → Doctor`.

**5. Person ↔ tất cả (MEDIUM)**
- `Person` là bảng trung tâm, được tham chiếu bởi User, Employee, Patient, Notification.
- `Notification` gắn với `PersonId` (không phải UserId) để hỗ trợ cả walk-in patients.

**6. Notification Queue (MEDIUM - in-process)**
- `INotificationQueue` là `Channel<T>` singleton in-memory.
- Nếu server restart, notification chưa gửi bị MẤT (không persisted).
- `ScheduledNotificationWorker` poll database để gửi notification đã lên lịch — pattern cũng fragile khi scale.

---

## 4. Các Phương Án Kiến Trúc

### Phương Án A: Modular Monolith (Củng Cố Ranh Giới)

**Mô tả**: Giữ 1 process, 1 database, nhưng tổ chức code thành các module với ranh giới rõ ràng. Các module chỉ giao tiếp qua interfaces/events nội bộ, không truy cập trực tiếp vào repository của nhau.

**Cấu trúc đề xuất**:
```
Clinic.Modules/
├── Identity/          (User, Role, Permission, RefreshToken)
├── Staff/             (Employee, Doctor, Specialty, WorkHistory)
├── Scheduling/        (WorkSchedule, ShiftRequest)
├── Appointments/      (Appointment, AppointmentSnapshot)
├── Queue/             (QueueTicket, QueueCounter)
├── MedicalRecords/    (MedicalReport)
├── Patients/          (Patient)
└── Notifications/     (Notification + channel handlers)
```

**Cải thiện cụ thể**:
- Persisted notification queue (dùng DB table thay `Channel<T>` in-memory).
- Internal event bus thay vì handler gọi trực tiếp repository của module khác.
- Mỗi module có interface rõ ràng, không expose EF entities ra ngoài.

**Ưu điểm**:
- Không thay đổi deployment, không overhead mạng.
- Transaction vẫn ACID đầy đủ.
- Dễ refactor từng bước, không phá vỡ hệ thống đang chạy.
- Ranh giới module tạo tiền đề cho việc tách service sau này nếu cần.

**Nhược điểm**:
- Không scale độc lập từng module.
- Vẫn phụ thuộc 1 codebase, team lớn có thể xung đột.

---

### Phương Án B: Microservices theo Bounded Context

**Mô tả**: Tách thành các service độc lập, mỗi service có database riêng, giao tiếp qua HTTP API hoặc message broker.

#### Đề xuất phân chia service:

**Service 1: Identity Service**
- **Sở hữu**: User, Role, Permission, UserRole, RolePermission, RefreshToken
- **Trách nhiệm**: Đăng ký, đăng nhập, refresh token, quản lý roles/permissions, reset password
- **Endpoints**: `/auth/*`, `/admin/users`, `/admin/roles`
- **Phát ra event**: `UserRegistered`, `PasswordChanged`
- **Database**: DB_Identity (SQL Server hoặc PostgreSQL)
- **Ghi chú**: Service duy nhất phát JWT; các service khác chỉ validate JWT (không gọi Identity)

**Service 2: Staff & Scheduling Service**
- **Sở hữu**: Employee, Doctor, Specialty, WorkHistory, ShiftRequest, WorkSchedule, Person (phần nhân viên)
- **Trách nhiệm**: Quản lý hồ sơ nhân viên/bác sĩ, ca làm việc, duyệt ca
- **Endpoints**: `/doctors/*`, `/employees/*`, `/shifts/*`, `/specialties/*`
- **Phát ra event**: `WorkScheduleCreated`, `WorkScheduleCancelled`, `DoctorSpecialtyChanged`
- **Database**: DB_Staff
- **Phức tạp nhất**: `ShiftRequest.Approve()` tạo WorkSchedule — cần atomic trong service này

**Service 3: Appointment Service**
- **Sở hữu**: Appointment, AppointmentSnapshot, Patient, Person (phần bệnh nhân)
- **Trách nhiệm**: Đặt lịch, xác nhận, hủy lịch, xem lịch sử
- **Endpoints**: `/appointments/*`, `/patients/*`, `/me/appointments`
- **Phụ thuộc**: Gọi Staff Service để lấy WorkSchedule và kiểm tra slot (sync HTTP call)
- **Phát ra event**: `AppointmentCreated`, `AppointmentConfirmed`, `AppointmentCancelled`, `AppointmentCheckedIn`
- **Database**: DB_Appointment
- **Vấn đề nghiêm trọng**: Race condition slot booking cần distributed lock hoặc saga

**Service 4: Queue Service**
- **Sở hữu**: QueueTicket, QueueCounter
- **Trách nhiệm**: Check-in, gọi số, skip, bắt đầu/hoàn thành khám
- **Endpoints**: `/queue/*` và `/doctors/{id}/queue`
- **Phụ thuộc**: Cần AppointmentId, DoctorId (lấy từ event hoặc sync call)
- **Phát ra event**: `PatientCheckedIn`, `PatientCalled`, `ExamCompleted`
- **Database**: DB_Queue
- **Vấn đề**: `CompleteExam` cần update cả QueueTicket và Appointment → distributed transaction hoặc eventual consistency

**Service 5: Medical Records Service**
- **Sở hữu**: MedicalReport
- **Trách nhiệm**: Tạo, cập nhật, finalize phiếu khám
- **Endpoints**: `/medical-reports/*`
- **Phụ thuộc**: Consume `ExamCompleted` event để tạo MedicalReport
- **Database**: DB_MedicalRecords

**Service 6: Notification Service**
- **Sở hữu**: Notification, NotificationQueue (persisted)
- **Trách nhiệm**: Gửi email, SMS, in-app; lên lịch nhắc; quản lý trạng thái thông báo
- **Endpoints**: Internal only (consumed bởi các service khác)
- **Consume events**: `AppointmentConfirmed`, `AppointmentCancelled`, `UserRegistered`, ...
- **Database**: DB_Notifications + Redis (cho real-time queue)
- **Tích hợp**: SignalR Hub nên chuyển vào service này

**Service 7: API Gateway** (không phải service nghiệp vụ)
- **Trách nhiệm**: Route requests, JWT validation, rate limiting, CORS
- **Công nghệ**: YARP, Nginx, hoặc Ocelot

#### Sơ đồ kiến trúc microservices:

```
                    ┌──────────┐
                    │ Frontend │
                    └────┬─────┘
                         │ HTTP/WS
                    ┌────▼─────┐
                    │  API     │
                    │ Gateway  │
                    └────┬─────┘
         ┌───────────────┼───────────────────┐
         │               │                   │
    ┌────▼────┐    ┌──────▼──────┐     ┌──────▼──────┐
    │Identity │    │   Staff &   │     │Appointment  │
    │Service  │    │ Scheduling  │     │  Service    │
    │         │    │   Service   │     │             │
    └────┬────┘    └──────┬──────┘     └──────┬──────┘
         │                │                   │
         └────────────────┴──────┬────────────┘
                                 │ Message Broker (RabbitMQ / Azure Service Bus)
              ┌──────────────────┼──────────────────┐
              │                  │                  │
       ┌──────▼──────┐   ┌───────▼──────┐   ┌──────▼──────┐
       │   Queue     │   │   Medical    │   │Notification │
       │  Service    │   │  Records     │   │  Service    │
       │             │   │  Service     │   │+SignalR Hub │
       └─────────────┘   └──────────────┘   └─────────────┘
```

---

### Phương Án C: Hybrid — Modular Monolith + Tách 2 Service Độc Lập

**Mô tả**: Giữ core (Appointment, Queue, Staff, Medical Records) là modular monolith. Chỉ tách ra 2 service thực sự có lý do rõ ràng:
1. **Identity Service**: Có thể dùng lại cho nhiều app, cần isolate security concerns.
2. **Notification Service**: Cần scale độc lập (gửi email/SMS có rate limit), có thể dùng external service (SendGrid, Twilio).

**Lý do C khả thi hơn B**:
- Giải quyết vấn đề notification queue không persisted (lỗi lớn nhất hiện tại).
- Tránh distributed transaction phức tạp giữa Appointment + Queue + Medical Records.
- Phù hợp quy mô dự án (team nhỏ, Docker Compose).

---

## 5. Phân Tích Vấn Đề Khi Phân Tán

### 5.1 Distributed Transaction — Vấn đề nghiêm trọng nhất

**Hiện tại (monolith)**:
- `CreateAppointment` dùng Serializable transaction để tránh race condition slot.
- `CompleteExam` cập nhật `QueueTicket` + `Appointment` cùng 1 transaction.
- `ShiftRequest.Approve()` tạo `WorkSchedule` + update `Doctor.WorkSchedules` atomic.

**Nếu tách service**:
```
Vấn đề 1: Đặt lịch
  Appointment Service → Staff Service: "WorkSchedule còn slot không?"
  → Staff Service trả lời "Còn" → Appointment Service tạo Appointment
  → NHƯNG: giữa 2 bước trên, patient khác cũng đặt cùng slot
  → Race condition, cần distributed lock (Redis) hoặc choreography saga

Vấn đề 2: Hoàn thành khám
  Queue Service: QueueTicket.Complete()
  Appointment Service: Appointment.Complete()
  → Nếu Queue Service commit xong, Appointment Service fail
  → QueueTicket = Completed, Appointment = CheckedIn (inconsistent)
  → Cần saga pattern với compensation actions

Vấn đề 3: Check-in
  Queue Service cần biết WorkSchedule.DoctorId để tạo QueueCounter
  → Cần gọi Appointment Service để lấy thông tin
  → Hoặc lưu denormalized DoctorId trong event
```

**Giải pháp cần nghiên cứu**:
- **Saga Choreography**: Service publish event, service khác react và publish event tiếp theo.
- **Saga Orchestration**: 1 orchestrator điều phối toàn bộ flow.
- **Outbox Pattern**: Đảm bảo message được publish cùng với DB commit (at-least-once delivery).

### 5.2 Tính Nhất Quán Dữ Liệu

**Hiện tại**: Strong consistency (ACID, 1 DB).

**Sau khi tách**: Eventual consistency — chấp nhận được với:
- Notification (gửi email chậm vài giây không ảnh hưởng UX).
- Báo cáo thống kê (Admin dashboard không cần real-time).

**KHÔNG chấp nhận eventual consistency**:
- Slot booking (2 người không được đặt cùng 1 slot).
- Queue number (2 patient không được có cùng số thứ tự).
- Appointment status (CheckedIn/Completed phải sync).

### 5.3 Authentication/Authorization

**Hiện tại**: 1 process, permission check qua `ICurrentUser.HasPermission()` đọc từ JWT claims.

**Vấn đề khi phân tán**:
- Mỗi service cần validate JWT (public key phải chia sẻ).
- Permission data trong JWT có thể stale nếu permission thay đổi.
- `ICurrentUser.DoctorId` — mapping User→Doctor cần được cache hoặc include trong JWT.

**Giải pháp**:
- JWT validation tại API Gateway (không gọi Identity Service cho mỗi request).
- Include `doctorId`, `patientId` vào JWT claims khi login.
- Sử dụng short-lived access token (15 phút) để giảm staleness.

### 5.4 Notification (Hiện trạng đáng lo ngại)

**Vấn đề đã xác minh**:
- `INotificationQueue` là `Channel<T>` singleton in-memory → mất notification khi restart.
- `ScheduledNotificationWorker` poll theo interval → có thể miss notification nếu restart đúng lúc.
- `SmsSender` có vẻ là stub (chỉ có file nhỏ 564 bytes).

**Cần sửa dù giữ monolith hay chuyển microservices**:
- Thay in-memory queue bằng persisted queue (DB table `NotificationOutbox` hoặc RabbitMQ).
- Implement idempotency key để tránh gửi trùng khi retry.

### 5.5 Lỗi Mạng, Retry, Idempotency

**Khi services giao tiếp qua HTTP**:
- Network partition: Service A không biết Service B đã xử lý hay chưa.
- Cần idempotency key trong mọi write operation.
- Timeout cần tuning cẩn thận (đặc biệt với slot booking có distributed lock).

**Khi dùng message broker**:
- At-least-once delivery → consumer cần idempotent.
- Message ordering quan trọng với Queue events (Call → Skip phải đúng thứ tự).

### 5.6 Logging & Tracing

**Hiện tại**: `RequestLoggingMiddleware` (xác minh có trong `Program.cs`), structured logging không rõ.

**Cần bổ sung cho microservices**:
- Distributed tracing (OpenTelemetry + Jaeger/Zipkin).
- Correlation ID propagation giữa services.
- Centralized log aggregation (ELK Stack hoặc Loki+Grafana).

### 5.7 Deployment & Testing

**Hiện tại**: Docker Compose đơn giản, 3 services (db, backend, frontend).

**Microservices trên Docker Compose**:
- Khả thi với 5-7 services nếu resource máy đủ.
- Cần health check, dependency ordering (`depends_on`).
- Integration test trở nên khó hơn nhiều (cần spin up nhiều service).

**Testing challenges**:
- Unit test: vẫn dễ (mỗi service test độc lập).
- Integration test: cần test contract giữa services (Pact / consumer-driven contracts).
- E2E test: phức tạp hơn nhiều với microservices.

---

## 6. So Sánh Các Phương Án

| Tiêu chí | Monolith hiện tại | A: Modular Monolith | B: Full Microservices | C: Hybrid (2 services tách) |
|---|---|---|---|---|
| **Chi phí phát triển** | Hiện tại | Thấp (refactor trong codebase) | Rất cao (rewrite + infra) | Trung bình |
| **Tài nguyên vận hành** | Thấp | Thấp | Cao (5-7 containers + broker) | Trung bình |
| **Độ phức tạp vận hành** | Thấp | Thấp | Rất cao | Trung bình |
| **Scale độc lập** | Không | Không | Có | Có (Notification) |
| **Tính nhất quán dữ liệu** | Strong ACID | Strong ACID | Eventual (phức tạp) | Mixed |
| **Khả năng kiểm thử** | Tốt | Tốt | Khó (integration) | Trung bình |
| **Phù hợp quy mô dự án** | Phù hợp | Rất phù hợp | Không phù hợp | Phù hợp |
| **Rủi ro chuyển đổi** | — | Thấp | Rất cao | Trung bình |
| **Giải quyết vấn đề hiện tại** | Không | Có (notification, boundaries) | Có nhưng over-engineered | Có (notification) |
| **Phù hợp Docker Compose** | Tốt | Tốt | Có thể (tốn RAM) | Tốt |

### 6.1 Nhận xét về quy mô dự án

**Dựa trên những gì đã xác minh**:
- Hệ thống quản lý phòng khám quy mô vừa/nhỏ.
- 1 database SQL Server, ~20 entity, 12 controllers.
- Không có dấu hiệu của traffic cao cần scale (không có cache layer như Redis, không có read replica).
- 4 role người dùng: Admin, Doctor, Receptionist, Patient.
- Team phát triển có vẻ nhỏ (inferred từ cấu trúc).

**Kết luận về phương án phù hợp**:

> ⚠️ **Full microservices (Phương án B) KHÔNG được khuyến nghị** cho dự án này ở thời điểm hiện tại.  
> Lý do: Chi phí vận hành, độ phức tạp distributed transaction, và quy mô dự án không biện minh cho lợi ích đạt được.

> ✅ **Phương án A (Modular Monolith)** là lựa chọn tốt nhất, giải quyết các vấn đề thực tế (notification queue, module boundaries) mà không thêm complexity không cần thiết.

> ✅ **Phương án C (Hybrid)** là bước tiếp theo hợp lý nếu có nhu cầu cụ thể (ví dụ: tích hợp notification provider bên thứ ba, hoặc reuse Identity cho nhiều ứng dụng).

---

## 7. Lộ Trình Chuyển Đổi

### Giai Đoạn 0: Ổn Định Trước Khi Chuyển (Prerequisite — 1-2 tuần)

**Mục tiêu**: Sửa các lỗi thiết kế hiện tại mà không thay đổi kiến trúc.

**Công việc**:
1. **Fix notification queue**: Thay `Channel<T>` in-memory bằng DB-backed outbox table (`NotificationOutbox`). Đây là lỗi reliability thực sự, cần sửa ngay dù không chuyển microservices.
2. **Verify SmsSender**: Xác nhận `SmsSender` có thực sự implement hay chỉ là stub. Nếu là stub, cần document rõ.
3. **Thêm integration tests** cho các luồng nghiệp vụ quan trọng (CheckIn, CompleteExam, CreateAppointment) trước khi refactor.
4. **Kiểm tra `ScheduledNotificationWorker`**: Đảm bảo không bỏ sót notification khi restart.

**Điều kiện tiên quyết**: Không có.  
**Tiêu chí thành công**: Notification không bị mất khi restart server. Integration test pass.  
**Rủi ro**: Thấp — thay đổi infrastructure, không thay đổi business logic.

---

### Giai Đoạn 1: Modular Monolith (Ưu tiên cao — 4-8 tuần)

**Mục tiêu**: Tổ chức lại code thành modules với ranh giới rõ ràng, không thay đổi runtime behavior.

**Thứ tự thực hiện**:

**1a. Tách Identity Module** (tuần 1-2)
- Tách User, Role, Permission, RefreshToken thành module riêng với interface rõ ràng.
- Các module khác chỉ nhận `UserId` (Guid) qua JWT, không truy cập trực tiếp User entity.
- Đây là module dễ tách nhất vì ít phụ thuộc vào.

**1b. Tách Patient & Notification Module** (tuần 2-3)
- Patient module: Patient, Person (phần bệnh nhân).
- Notification module: Notification + handlers, dùng outbox từ Giai đoạn 0.

**1c. Tách Staff & Scheduling Module** (tuần 3-5)
- Staff: Employee, Doctor, Specialty, WorkHistory.
- Scheduling: WorkSchedule, ShiftRequest.
- **Khó nhất**: `ShiftRequest.Approve()` cần atomic, phải giữ trong cùng 1 module.

**1d. Refactor Appointment & Queue** (tuần 5-7)
- Giữ Appointment và Queue trong 1 module (vì coupling quá mạnh).
- Tách MedicalReport thành module riêng.
- Định nghĩa internal events giữa modules.

**1e. API Gateway nội bộ** (tuần 7-8)
- Xem xét thêm YARP hoặc tổ chức lại routing để chuẩn bị cho tách service sau.

**Điều kiện tiên quyết**: Giai đoạn 0 hoàn thành, test coverage đủ.  
**Tiêu chí thành công**: Mỗi module chỉ truy cập repository của chính nó. Không có module nào inject repository của module khác trực tiếp.  
**Rủi ro**: Trung bình — cần refactor nhiều, dễ break nếu không có test.

---

### Giai Đoạn 2 (Optional): Tách Notification Service (4-6 tuần)

**Điều kiện**: Có nhu cầu cụ thể (scale, external provider, multi-app).

**Công việc**:
1. Extract Notification module thành service riêng.
2. Giao tiếp qua RabbitMQ (hoặc Azure Service Bus nếu cloud).
3. Chuyển SignalR Hub vào Notification Service.
4. Implement idempotency cho tất cả notification events.

**Điều kiện tiên quyết**: Giai đoạn 1 hoàn thành.  
**Rủi ro**: Trung bình — SignalR với nhiều service instances cần Redis backplane.

---

### Giai Đoạn 3 (Optional): Tách Identity Service (6-8 tuần)

**Điều kiện**: Có nhiều ứng dụng cần dùng chung Identity, hoặc muốn dùng OAuth2/OIDC chuẩn.

**Công việc**:
1. Xem xét dùng Keycloak hoặc IdentityServer thay vì tự implement.
2. Nếu tự implement: Extract Identity module, expose `/auth` endpoints riêng.
3. API Gateway validate JWT từ Identity Service.
4. Các service khác không gọi Identity Service (stateless JWT validation).

**Rủi ro**: Trung bình-cao — security-critical, cần kiểm tra kỹ.

---

### Giai Đoạn 4 (Không khuyến nghị trừ khi có nhu cầu rõ ràng): Full Microservices

**Điều kiện thực sự cần thiết**:
- Traffic đủ lớn để cần scale 1 service độc lập (ví dụ: Queue Service có 1000 concurrent check-in/ngày).
- Team đủ lớn để maintain nhiều service (ít nhất 2-3 người/service).
- SLA yêu cầu từng service có thể deploy độc lập.

**Lưu ý**: Nếu không đáp ứng các điều kiện trên, dừng ở Giai đoạn 1 hoặc 2 là hợp lý nhất.

---

## 8. Câu Hỏi Cần Nghiên Cứu Thêm

### Câu hỏi về kiến trúc & nghiệp vụ

1. **Scale thực tế**: Hệ thống phục vụ bao nhiêu bệnh nhân/ngày? Bao nhiêu concurrent users? (Cần biết để đánh giá có thực sự cần scale hay không.)

2. **SLA yêu cầu**: Có yêu cầu uptime cho từng tính năng riêng biệt không? (Ví dụ: Queue Management cần 99.9% nhưng Notification có thể 99%?)

3. **Multi-tenant**: Hệ thống phục vụ 1 phòng khám hay nhiều cơ sở? (Nếu nhiều cơ sở → có thể cần multi-tenancy thay vì microservices.)

4. **Walk-in Patient**: Luồng walk-in patient không rõ ràng — bệnh nhân vãng lai tạo appointment như thế nào? Receptionist tạo thay không? Code có `IsWalkIn=true` nhưng không thấy endpoint rõ ràng.

5. **SmsSender**: File chỉ 564 bytes — có thực sự gửi SMS không? Nếu không, notification qua SMS bị bỏ qua hoàn toàn.

### Câu hỏi về kỹ thuật

6. **Notification scheduled**: `ScheduledNotificationWorker` hoạt động như thế nào khi có nhiều instance (horizontal scaling)? Hiện tại có thể gửi trùng.

7. **QueueCounter với `MERGE HOLDLOCK`**: SQL Server-specific — nếu chuyển sang PostgreSQL (free) thì cần cơ chế khác. Có kế hoạch thay đổi DB không?

8. **SignalR scaling**: Nếu chạy nhiều instance backend, SignalR cần Redis backplane. Hiện tại chỉ có 1 instance (Docker Compose). Có kế hoạch horizontal scale không?

9. **`ScheduledNotificationWorker`**: Cách poll hiện tại có thể miss notification nếu server restart đúng lúc gửi. Cần xác nhận behavior.

10. **Test coverage**: Unit tests đã cover được bao nhiêu % business logic? Đây là prerequisite quan trọng trước bất kỳ refactor nào.

11. **Frontend stack**: Frontend thực sự dùng gì? Có sử dụng SignalR client không? (Ảnh hưởng đến cách di chuyển SignalR hub.)

12. **Permission `appointment.cancel` (deprecated)**: Code có nhiều permission deprecated với `isDeleted = true`. Cleanup chưa được thực hiện — có thể tạo nhầm lẫn khi đọc logic authorization.

---

## Phụ Lục: Luồng Check-in Có Vấn Đề Tiềm Ẩn

```
// AppointmentController.cs - CheckIn endpoint
[Authorize(Policy = "Permission:queue.check-in")]
public async Task<IActionResult> CheckIn([FromRoute] Guid appointmentId)

// CheckIn.cs handler
var doctorId = appointment.WorkSchedule.DoctorId;
var queueNumber = await _queueTicketRepository.GetNextQueueNumberAsync(doctorId, checkInTime.Date, cancellationToken);
// ↑ SQL MERGE HOLDLOCK - cần transaction active để giữ lock
// Nhưng không thấy InitializeTransactionLockAsync() được gọi ở đây!
// → Có thể có race condition nếu 2 check-in cùng lúc cho cùng 1 bác sĩ trong 1 ngày
```

> **Cần xác minh thêm**: Liệu SQL MERGE HOLDLOCK có đủ để đảm bảo atomicity mà không cần explicit transaction từ EF Core không? Hay đây là bug tiềm ẩn?

---

*Phân tích này dựa hoàn toàn trên code thực tế của repository. Mọi suy luận được đánh dấu rõ ràng. Các phần chưa có đủ thông tin cũng được nêu trong Mục 8.*

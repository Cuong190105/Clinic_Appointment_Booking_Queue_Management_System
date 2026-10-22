# Kế Hoạch Triển Khai Giai Đoạn 2: Tách Staff & Scheduling Service
> **Tài liệu tham chiếu**:
> - `tasks/TASK_MEMBER_A.md` (Nhiệm vụ Thành viên A - Giai đoạn 2)
> - `tasks/TASK_MEMBER_B.md` (Nhiệm vụ Thành viên B - Giai đoạn 2)
> - `tasks/COMMON_REQUIREMENTS_AND_NOTES.md` (Yêu cầu kỹ thuật chung)
> - `tasks/PHASE_0_API_CONTRACT_ANALYSIS.md` (Phân tích API Contract & Service Boundary)
> - `docs/identity-service-phase1.md` (Bài học kinh nghiệm & Tiền lệ từ Giai đoạn 1)

---

## 1. Tổng Quan & Mục Tiêu Giai Đoạn 2

### 1.1. Mục tiêu cốt lõi
Giai đoạn 2 tập trung vào việc tách toàn bộ chức năng quản lý nhân sự y tế, bác sĩ, chuyên khoa, lịch làm việc và năng lực phục vụ (capacity) ra khỏi Monolith để hình thành một microservice độc lập: **Staff & Scheduling Service** (`Clinic.Scheduling.Service`).

Dịch vụ này sở hữu cơ sở dữ liệu riêng biệt (`ClinicSchedulingDB`), không chia sẻ bảng, không liên kết khóa ngoại (Foreign Key) và không thực hiện truy vấn trực tiếp xuyên database với bất kỳ dịch vụ nào khác.

### 1.2. Quyết định kiến trúc nền tảng: Mô hình "Single Identity – Role-Specific Projections"
Hệ thống **duy trì trọn vẹn khả năng 1 con người nhận nhiều vai trò** (ví dụ: Bác sĩ vừa có thể khám bệnh, vừa có thể là Bệnh nhân tự đặt lịch khám cho chính mình). 

Kiến trúc thống nhất như sau:
1. **Identity Service làm Nguồn sự thật duy nhất (Single Source of Truth - SSOT)** cho tài khoản và nhân thân cơ bản: Sở hữu bảng `Users` và `Persons` (`PersonProfile` gồm `FullName`, `PhoneNumber`, `Email`, `DateOfBirth`, `Gender`, `Address`) trong `ClinicIdentityDB`.
2. **Các Business Services (Scheduling, Medical/Patient) sử dụng Projection (Bản chiếu)**:
   - Trong `Clinic.Scheduling.Service`: `Employee` và `Doctor` lưu projection các trường nhân thân (`PersonId`, `IdentityUserId`, `FullName`, `PhoneNumber`, `Email`, `Gender`) để phục vụ truy vấn tại chỗ siêu tốc mà không cần gọi RPC sang Identity.
   - Trong `Medical / Patient Service` (hiện ở `ClinicDB`, tương lai tách riêng ở GĐ 5): `Patient` lưu projection các trường nhân thân (`PersonId`, `IdentityUserId`, `FullName`, `PhoneNumber`, `DateOfBirth`, `Gender`, `Address`) kết hợp dữ liệu y tế (BHYT, tiền sử bệnh).
3. **Đồng bộ hóa qua Event-Driven**: Khi người dùng cập nhật thông tin tại Identity Service, sự kiện `UserProfileUpdated` được phát qua Message Broker để các service tự cập nhật bản chiếu của mình.
4. **Bệnh nhân vãng lai (Walk-in)**: Lễ tân tạo trực tiếp `Patient` trong Patient Service với `IdentityUserId = null`. Khi bệnh nhân đăng ký tài khoản online sau này bằng cùng số điện thoại, Identity Service sẽ liên kết `IdentityUserId` vào hồ sơ `Patient` sẵn có.
5. **Tiền đề vững chắc cho Giai đoạn 5 (Tách Patient Service)**: Đến GĐ 5, việc tách `Patient` thành service riêng hoàn toàn độc lập và sạch sẽ, không bị vướng mắc khóa ngoại với bất kỳ service nào.

### 1.3. Các Aggregate và Entity chuyển giao quyền sở hữu
| Entity / Aggregate | Vai trò nghiệp vụ trong Scheduling Service |
| :--- | :--- |
| `Employee` | Quản lý thông tin nghề nghiệp, ngày vào làm, cấp trên, kèm **bản chiếu nhân thân** (`PersonId`, `IdentityUserId`, `FullName`, `PhoneNumber`, `Email`). |
| `Doctor` | Quản lý thông tin chuyên môn bác sĩ, số CCHN, học vị, kinh nghiệm, liên kết `EmployeeId`. |
| `Specialty` | Danh mục chuyên khoa lâm sàng. |
| `WorkHistory` | Lịch sử phân công bác sĩ vào các chuyên khoa qua các thời kỳ. |
| `WorkSchedule` | Ca làm việc của bác sĩ theo ngày, khung giờ và hạn mức năng lực phục vụ (`RecommendedCapacity`). |
| `ShiftRequest` | Đề xuất đăng ký ca, đổi ca của bác sĩ chờ ban quản lý phê duyệt. |
| `SlotReservation` *(Entity mới)* | Quản lý giữ chỗ tạm thời (có timeout & idempotency) để loại bỏ hoàn toàn nguy cơ overbooking. |
| `OutboxMessage` *(Entity mới)* | Lưu trữ các sự kiện lịch trực trong cùng local transaction để publisher gửi sang Message Broker. |

---

## 2. Các Quyết Định Kiến Trúc Đã Chốt Cho Giai Đoạn 2

Dựa trên quá trình đối soát và thống nhất định hướng, dưới đây là **5 quyết định kiến trúc chính thức**:

```mermaid
flowchart TD
    subgraph Decisions ["5 Quyết Định Kiến Trúc Đã Chốt"]
        D1["1. Identity là SSOT cho Person; Scheduling & Patient dùng Projection"]
        D2["2. Triển khai Booking Saga sớm trong Monolith; Xóa FK sang WorkSchedule"]
        D3["3. Trừ Capacity theo cả Slot 15p & Trần Ca trực (Dual-Constraint)"]
        D4["4. Thu hồi Slot hết hạn bằng Hybrid: Passive Check + Background Worker"]
        D5["5. Thành viên A viết Outbox Local Tx; Thành viên B viết Publisher RabbitMQ"]
    end
```

### 2.1. Quyết định 1: Quản lý Nhân thân & Mô hình Đa vai trò (Multi-Role)
- **Nguồn sự thật (SSOT)**: `Clinic.Identity.Service` (`ClinicIdentityDB`) sở hữu bảng `Persons` (`PersonProfile`).
- **Liên kết**: `IdentityUser` lưu `PersonId`, `PatientId`, `DoctorId`, `EmployeeId` và nhúng trực tiếp vào Claims của Access Token (`sub`, `personId`, `roles`, `patientId`, `doctorId`, `employeeId`).
- **Bản chiếu (Projection)**: `Clinic.Scheduling.Service` lưu thông tin nhân sự `Employee` có sẵn các cột projection: `IdentityUserId`, `PersonId`, `FullName`, `PhoneNumber`, `Email`, `Gender`. Khi truy vấn danh sách bác sĩ, lịch trực, service đọc trực tiếp từ DB của mình.
- **Bệnh nhân vãng lai**: Lễ tân tạo hồ sơ trong Patient Service với `IdentityUserId = null`.
- **Đồng bộ Event**: Khi đổi thông tin cá nhân, Identity Service phát `UserProfileUpdated` để Scheduling Service cập nhật bản chiếu.

---

### 2.2. Quyết định 2: Tách biệt Quan hệ giữa `Appointment` và `WorkSchedule`
- **Tái cấu trúc bảng `Appointments` trong `ClinicDB`**:
  1. Thêm các cột denormalized: `DoctorId` (Guid), `DoctorName` (nvarchar 200), `SpecialtyName` (nvarchar 100), `ScheduleDate` (DateOnly).
  2. Xóa bỏ Foreign Key `FK_Appointments_WorkSchedules_WorkScheduleId` trong `ClinicDB`.
  3. Cập nhật các câu query lịch hẹn đọc trực tiếp từ các cột denormalized, không còn navigation property `WorkSchedule`.
- **Triển khai Booking Saga sớm trong Monolith (`Clinic.API`)**:
  - Tại `CreateAppointmentCommandHandler`:
    1. Gọi HTTP API `POST /schedules/{id}/reservations` sang `Clinic.Scheduling.Service` để giữ chỗ (kèm `Idempotency-Key`).
    2. Nếu thành công, lưu `Appointment` mới vào `ClinicDB` với trạng thái `Pending`.
    3. Gọi HTTP API `POST .../confirm` để chốt giữ chỗ $\rightarrow$ Chuyển `Appointment.Status = Confirmed`.
    4. Nếu bước lưu `Appointment` thất bại, gọi `POST .../release` để giải phóng slot ngay lập tức.
  - **Lợi ích**: Cắt đứt hoàn toàn FK/join xuyên service ngay từ GĐ 2, tạo nền tảng để đến Giai đoạn 3 tách `Appointment Service` chỉ là chuyển code sang project mới.

---

### 2.3. Quyết định 3: Quy tắc trừ Capacity, Hiển thị Slot Online và Cơ chế Linh hoạt cho Khách Vãng lai (Walk-in)
Thống nhất quy tắc quản lý năng lực ca trực và sinh slot giữa 2 luồng: **Đặt lịch Online** và **Tiếp nhận Khách vãng lai (Walk-in)**:

1. **Phân biệt rạch ròi 2 luồng đặt lịch**:
   - **Luồng Đặt lịch Online (Bệnh nhân tự đặt qua Web/App)**:
     - Bắt buộc tuân thủ hạn mức `RecommendedCapacity` (Hạn mức đề xuất cho online).
     - Bắt buộc tuân thủ slot 15 phút cố định, chống trùng lặp giờ giữa các bệnh nhân online.
   - **Luồng Khách vãng lai (Lễ tân tiếp nhận tại quầy - Walk-in)**:
     - Lễ tân có thể trao đổi linh hoạt với bác sĩ đang trực để quyết định có nhận thêm bệnh nhân hay không.
     - **Được phép đặt vượt ngưỡng đề xuất (`RecommendedCapacity`)**: Bác sĩ có thể nhận thêm ca ngoài định mức khi có ca khẩn cấp hoặc khi bác sĩ đồng ý tăng cường tiếp nhận.
     - **Không bị ràng buộc bởi Unique Slot 15 phút**: Bệnh nhân vãng lai có thể được xếp vào khám chèn/khám tiếp theo mà không chiếm slot đặt trước của bệnh nhân online (`Appointments.IsWalkIn = true`).
2. **Khoảng cách và Sinh Slot cho Online**:
   - Khoảng thời gian giữa mỗi slot là **15 phút**.
   - Các slot 15 phút được **sinh động phủ kín toàn bộ thời lượng ca trực** (từ `ShiftStart` đến `ShiftEnd`). Ví dụ ca 08:00 - 11:30 sẽ sinh ra các slot: 08:00, 08:15, 08:30, ..., 11:15 để người đặt lịch tự do chọn mốc thời gian phù hợp.
3. **Quy tắc Chặn khi Ca trực Đạt/Vượt Ngưỡng (`RecommendedCapacity`)**:
   - Mỗi ca làm việc có một hạn mức `RecommendedCapacity` (ví dụ: `RecommendedCapacity = 10`).
   - Tổng số lượt đặt online được tính bằng:
     $$\text{OnlineCount} = \text{Count}(\text{SlotReservations where Status = 'Confirmed' OR (Status = 'Reserved' AND ExpiresAt > UtcNow)})$$
   - Các lịch hẹn vãng lai (`IsWalkIn = true`) **không tính vào `OnlineCount`**.
   - **Hành vi khi xem lịch (Availability)**: Khi người dùng xem danh sách ca để đặt lịch online, **nếu một ca trực nào đã đạt hoặc vượt ngưỡng (`OnlineCount >= RecommendedCapacity`), hệ thống SẼ KHÔNG TRẢ VỀ BẤT KỲ SLOT NÀO CỦA CA ĐÓ NỮA**, mặc dù về mặt lý thuyết ca đó vẫn còn các mốc 15 phút chưa ai đặt. Ca trực được coi như đã "Hết chỗ" (Full) đối với luồng online.
4. **Kiểm tra 2 Lớp Nguyên Tử khi Giữ Chỗ Online (`ReserveSlot`)**:
   - Khi gửi yêu cầu `POST /schedules/{scheduleId}/reservations`:
     - **Lớp 1 (Kiểm tra Ngưỡng Ca)**: Nếu `OnlineCount >= RecommendedCapacity` $\rightarrow$ Từ chối ngay với mã lỗi `409 Conflict ("Ca làm việc đã đủ số lượng đặt khám online.")`.
     - **Lớp 2 (Kiểm tra Khung giờ cụ thể)**: Nếu khung giờ cụ thể đó (`TimeSlot`) đã có người khác giữ chỗ (`Reserved` chưa hết hạn) hoặc đã xác nhận (`Confirmed`) $\rightarrow$ Từ chối với mã lỗi `409 Conflict ("Khung giờ này đã được người khác đặt.")`.
     - Nếu thỏa mãn cả 2 lớp trong transaction: Tạo `SlotReservation` mới với `Status = Reserved`, `ExpiresAt = UtcNow + 10m`.
- Thực hiện kiểm tra và ghi nhận trong 1 local database transaction có khóa dòng (RowLock / Serializable) để đảm bảo không bao giờ bị overbooking khi có nhiều request online gửi đồng thời.

---

### 2.4. Quyết định 4: Cơ chế thu hồi Reservation khi hết hạn (Hybrid Expiration)
- **Passive Expiration (Thời gian thực)**: Trong query `GetAvailability` và command `ReserveSlot`, bất kỳ reservation nào có `Status == Reserved && ExpiresAt <= UtcNow` được tự động coi là đã hết hạn và không chiếm capacity.
- **Active Expiration (Background Worker)**: Một `IHostedService` (`ReservationExpirationWorker`) chạy định kỳ mỗi 30 giây trong Scheduling Service để cập nhật trạng thái các reservation quá hạn thành `Expired` và ghi log/metrics cho Thành viên B theo dõi.

---

### 2.5. Quyết định 5: Phân định ranh giới Outbox và Message Broker
- **Thành viên A**:
  - Thiết kế bảng `OutboxMessages` trong `SchedulingDbContext`.
  - Cài đặt `IOutboxWriter` để serialize domain events (`WorkScheduleCreated`, `WorkScheduleUpdated`, `WorkScheduleCancelled`) theo chuẩn `EventEnvelope<T>` và ghi vào `OutboxMessages` trong cùng local transaction của EF Core khi duyệt ca, sửa ca, hủy ca.
- **Thành viên B**:
  - Triển khai `OutboxPublisher` (Background Service) đọc bảng `OutboxMessages` để đẩy sang RabbitMQ.
  - Triển khai Consumer và Contract Test cho các sự kiện này.

---

## 3. Thiết Kế Kiến Trúc Dịch Vụ Staff & Scheduling

### 3.1. Cấu trúc Project
- **Project**: `backend/Clinic.Scheduling.Service/Clinic.Scheduling.Service.csproj` (.NET 10.0 Web API).
- **Test Project**: `backend/Clinic.Scheduling.Service.Tests/Clinic.Scheduling.Service.Tests.csproj` (xUnit).
- **Dependencies**: Tham chiếu `Clinic.Contracts` (để dùng chung `EventEnvelope<T>` và `WorkScheduleEvents`). Không phụ thuộc vào `Clinic.Domain` hay `Clinic.Infrastructure.Sqlserver` của Monolith.

### 3.2. Thiết Kế Cơ Sở Dữ Liệu `ClinicSchedulingDB`
Mỗi bảng trong `ClinicSchedulingDB` hoàn toàn độc lập, có audit columns (`CreatedAt`, `UpdatedAt`, `IsDeleted`):

```mermaid
erDiagram
    Specialties ||--o{ WorkHistories : "classifies"
    Employees ||--o| Doctors : "specializes"
    Doctors ||--o{ WorkHistories : "records"
    Doctors ||--o{ WorkSchedules : "assigned"
    Doctors ||--o{ ShiftRequests : "requests"
    WorkSchedules ||--o{ SlotReservations : "contains"

    Employees {
        guid Id PK
        guid PersonId "Projection tu Identity"
        guid IdentityUserId "Projection tu Identity"
        string FullName "Projection tu Identity"
        string PhoneNumber "Projection tu Identity"
        string Email "Projection tu Identity"
        string Gender "Projection tu Identity"
        date HireDate
        string Status
        guid ManagerId FK
    }

    Doctors {
        guid Id PK
        guid EmployeeId FK
        string LicenseNumber UK
        string Qualification
        int ExperienceYears
        string Biography
        string Status
    }

    Specialties {
        guid Id PK
        string Name UK
        string Description
        date EstablishedDate
    }

    WorkHistories {
        guid Id PK
        guid DoctorId FK
        guid SpecialtyId FK
        date StartDate
        date EndDate
    }

    WorkSchedules {
        guid Id PK
        guid DoctorId FK
        date Date
        time ShiftStart
        time ShiftEnd
        int RecommendedCapacity
        string Status
        string CancellationReason
    }

    SlotReservations {
        guid Id PK
        guid WorkScheduleId FK
        time TimeSlot
        guid PatientId
        string Status
        datetime ExpiresAt
        string IdempotencyKey UK
        datetime CreatedAt
    }

    ShiftRequests {
        guid Id PK
        guid DoctorId FK
        date Date
        time ShiftStart
        time ShiftEnd
        int RecommendedCapacity
        string Status
        string Reason
    }

    OutboxMessages {
        bigint Id PK
        guid EventId
        string EventType
        int SchemaVersion
        datetime OccurredAt
        guid CorrelationId
        string Payload
        string Status
        int RetryCount
    }
```

---

### 3.3. Các Quy Trình Nghiệp Vụ Cốt Lõi (Workflows)

#### Quy trình 1: Tra cứu khung giờ khả dụng (GetAvailability) với kiểm tra ngưỡng ca trực
```mermaid
sequenceDiagram
    autonumber
    actor Patient as Bệnh nhân (Frontend / Client)
    participant API as Scheduling API (/schedules/availability)
    participant DB as SchedulingDbContext
    
    Patient->>API: GET /schedules/availability?doctorId=...&fromDate=...&toDate=...
    API->>DB: Lấy danh sách WorkSchedule (Status = Active)
    loop Với từng ca trực (WorkSchedule)
        API->>DB: Đếm OnlineCount (Confirmed + Reserved còn hạn)
        alt OnlineCount >= RecommendedCapacity
            Note over API: CA ĐÃ ĐỦ NGƯỠNG:<br/>Bỏ qua toàn bộ ca trực, không sinh bất kỳ slot nào
        else OnlineCount < RecommendedCapacity
            Note over API: CA CÒN CHỖ:<br/>1. Sinh dải slot 15 phút (ShiftStart -> ShiftEnd)<br/>2. Loại bỏ các TimeSlot đã trùng với reservation<br/>3. Gom các slot trống còn lại
        end
    end
    API-->>Patient: 200 OK (Danh sách các SlotDto khả dụng)
```

#### Quy trình 2: Giữ chỗ slot (ReserveSlot) với kiểm tra Capacity nguyên tử & Idempotency
```mermaid
sequenceDiagram
    autonumber
    actor Client as Monolith / Appointment Service
    participant API as Scheduling API (/reservations)
    participant DB as SchedulingDbContext (Local Tx)
    
    Client->>API: POST /schedules/{id}/reservations (Header: Idempotency-Key)
    API->>DB: Kiểm tra IdempotencyKey đã tồn tại chưa
    alt Idempotency-Key đã xử lý trước đó
        DB-->>API: Trả về kết quả Reservation cũ đã lưu
        API-->>Client: 200 OK / 201 Created (Cached Response)
    else Request Mới
        API->>DB: Mở Local Transaction (RowLock / Serializable)
        API->>DB: 1. Kiểm tra Schedule tồn tại & Status = Active
        API->>DB: 2. Đếm Active Reservations (Reserved & chưa hết hạn) + Confirmed
        alt Vượt quá RecommendedCapacity HOẶC TimeSlot đã bị giữ
            DB-->>API: Conflict / Capacity Exceeded
            API-->>Client: 409 Conflict Problem Details
        else Đủ điều kiện giữ chỗ
            API->>DB: Tạo SlotReservation (Status: Reserved, ExpiresAt: UtcNow + 10m)
            API->>DB: Commit Local Transaction
            DB-->>API: Thành công
            API-->>Client: 201 Created (reservationId, expiresAt: 10m)
        end
    end
```

#### Quy trình 2: Duyệt ca làm việc (Approve Shift Request & Conflict Detection)
```mermaid
sequenceDiagram
    autonumber
    actor Admin as Lãnh đạo / Quản trị
    participant API as Scheduling API (/shifts/suggestions/{id}/approve)
    participant DB as SchedulingDbContext (Local Tx)
    participant Outbox as OutboxMessages Table
    
    Admin->>API: POST /shifts/suggestions/{id}/approve
    API->>DB: Mở Local Transaction
    API->>DB: 1. Đọc ShiftRequest (Status: Pending)
    API->>DB: 2. Kiểm tra trùng ca (Conflict Detection): Bác sĩ đã có ca nào cùng ngày chồng lấn giờ không?
    alt Có xung đột thời gian ca trực
        API-->>Admin: 409 Conflict ("Doctor already has an overlapping shift")
    else Không xung đột
        API->>DB: 3. Chuyển ShiftRequest.Status = Approved
        API->>DB: 4. Tạo WorkSchedule mới (Status: Active)
        API->>Outbox: 5. Ghi Event WorkScheduleCreated vào OutboxMessages
        API->>DB: 6. Commit Local Transaction
        API-->>Admin: 204 NoContent
    end
```

#### Quy trình 3: Đồng bộ Thông tin Cá nhân khi có thay đổi (UserProfileUpdated)
```mermaid
sequenceDiagram
    autonumber
    actor User as Bác sĩ / Bệnh nhân
    participant Identity as Identity Service (SSOT)
    participant Broker as RabbitMQ (Thành viên B)
    participant Scheduling as Scheduling Service (Projection)
    participant Patient as Patient Service (Projection)

    User->>Identity: Đổi Họ tên / SĐT / Email
    Identity->>Identity: Cập nhật bảng Persons trong ClinicIdentityDB
    Identity->>Broker: Phát sự kiện UserProfileUpdated(PersonId, FullName, PhoneNumber, Email)
    
    par Cập nhật Staff Projection
        Broker->>Scheduling: Consumer UserProfileUpdated
        Scheduling->>Scheduling: UPDATE Employees SET FullName = ..., PhoneNumber = ... WHERE PersonId = ...
    and Cập nhật Patient Projection
        Broker->>Patient: Consumer UserProfileUpdated
        Patient->>Patient: UPDATE Patients SET FullName = ..., PhoneNumber = ... WHERE PersonId = ...
    end
```

---

## 4. Kế Hoạch Thực Hiện Chi Tiết Từng Bước (Step-by-Step Execution Plan)

### Bước 1: Khởi Tạo Service & Thiết Lập Hạ Tầng Cơ Bản
1. **Tạo Project mới**:
   - `dotnet new webapi -n Clinic.Scheduling.Service -o backend/Clinic.Scheduling.Service`.
   - `dotnet new xunit -n Clinic.Scheduling.Service.Tests -o backend/Clinic.Scheduling.Service.Tests`.
   - Thêm vào `backend/Clinic.sln`.
   - Tham chiếu `Clinic.Contracts` vào `Clinic.Scheduling.Service`.
2. **Cài đặt thư viện**:
   - `Microsoft.EntityFrameworkCore.SqlServer` (10.x).
   - `Microsoft.EntityFrameworkCore.Design` (10.x).
   - `Microsoft.AspNetCore.Authentication.JwtBearer` (10.x).
   - `FluentValidation.AspNetCore`.
3. **Cấu hình Xác thực JWT độc lập qua JWKS (Identity Service)**:
   - Trong `Program.cs`, cấu hình `JwtBearer` sử dụng `MetadataAddress` hoặc `IssuerSigningKeyResolver` trỏ tới discovery endpoint `http://identity-service:8080/.well-known/jwks.json`. Không truy cập `ClinicIdentityDB`.
4. **Cấu hình Docker & Docker Compose**:
   - Tạo `backend/Clinic.Scheduling.Service/Dockerfile`.
   - Thêm service `scheduling-service` vào `docker-compose.yml`:
     - Cổng host: `5002:8080`.
     - Biến môi trường: `ConnectionStrings__SchedulingConnection`, `Jwt__Issuer`, `Jwt__Audience`, v.v.
     - Health check: `/healthz` và `/readyz`.

---

### Bước 2: Xây Dựng Domain Model & Persistence Layer (`ClinicSchedulingDB`)
1. **Định nghĩa các Entity**:
   - `Employee`: `Id`, `PersonId`, `IdentityUserId`, `FullName`, `PhoneNumber`, `Email`, `Gender`, `HireDate`, `Status`, `ManagerId`.
   - `Doctor`: `Id`, `EmployeeId`, `LicenseNumber`, `Qualification`, `ExperienceYears`, `Biography`, `Status`.
   - `Specialty` & `WorkHistory`: Chuyên khoa và quá trình công tác.
   - `WorkSchedule`: Lịch trực, trạng thái (`Active`, `Full`, `Cancelled`), hạn mức `RecommendedCapacity`.
   - `ShiftRequest`: Đề xuất trực, các phương thức domain: `Approve()`, `Reject()`, `Cancel()`.
   - `SlotReservation`: Bản ghi giữ chỗ với các trường:
     - `Id` (Guid), `WorkScheduleId` (Guid), `TimeSlot` (TimeOnly), `PatientId` (Guid).
     - `Status` (Enum: `Reserved`, `Confirmed`, `Released`, `Expired`).
     - `ExpiresAt` (DateTimeOffset - UTC).
     - `IdempotencyKey` (string - có Unique Index).
   - `OutboxMessage`: Bảng lưu sự kiện tích hợp.
2. **Tạo `SchedulingDbContext` & Fluent API Configurations**:
   - Cấu hình Unique Index cho `SlotReservation(WorkScheduleId, TimeSlot)` có điều kiện filter: `[Status] IN ('Reserved', 'Confirmed')`.
   - Cấu hình Unique Index cho `SlotReservation.IdempotencyKey`.
3. **Tạo Initial Migration**:
   - Tạo migration `20261021_InitialSchedulingSchema` cho `ClinicSchedulingDB`.

---

### Bước 3: Viết Kịch Bản Di Chuyển & Đối Soát Dữ Liệu
1. **Tạo kịch bản Import Dữ liệu**:
   - Tạo tệp `backend/Clinic.Scheduling.Service/Persistence/import-from-clinicdb.sql`:
     - Copy an toàn các bản ghi từ `ClinicDB.dbo.Specialties`, `ClinicDB.dbo.WorkHistories`, `ClinicDB.dbo.WorkSchedules`, `ClinicDB.dbo.ShiftRequests`.
     - Nạp dữ liệu vào `Employees`: JOIN giữa `ClinicDB.dbo.Employees`, `ClinicDB.dbo.Persons` và `ClinicIdentityDB.dbo.Users` để nạp đủ các trường projection nhân thân (`FullName`, `PhoneNumber`, `Email`, `Gender`, `IdentityUserId`).
     - Nạp dữ liệu vào `Doctors` từ `ClinicDB.dbo.Doctors`.
     - Script được thiết kế idempotent (chạy nhiều lần không gây trùng khóa).
2. **Tạo kịch bản Đối soát Dữ liệu**:
   - Tạo tệp `backend/Clinic.Scheduling.Service/Persistence/verify-scheduling-import.sql`:
     - Đối soát tổng số lượng bản ghi giữa 2 database (Yêu cầu `MismatchCount = 0`).
     - Kiểm tra toàn vẹn các cặp liên kết `Doctor -> Employee`.
3. **Thêm container `scheduling-data-importer` vào `docker-compose.yml`** (tương tự như `identity-data-importer` ở Phase 1).

---

### Bước 4: Xây Dựng Bộ API Nghiệp Vụ Cốt Lõi

1. **API Tra cứu Lịch & Khung giờ khả dụng (Availability API)**:
   - Endpoint: `GET /schedules/availability` (hỗ trợ alias `GET /slots`).
   - Query: `doctorId`, `specialtyId`, `fromDate`, `toDate`.
   - **Thuật toán sinh slot & chặn ngưỡng**:
     1. Truy vấn các `WorkSchedule` đang `Active` trong khoảng thời gian yêu cầu.
     2. Với mỗi ca trực, đếm tổng số lượt giữ chỗ/đặt online đang có hiệu lực:
        $$\text{OnlineCount} = \text{Count}(\text{SlotReservations where Status = 'Confirmed' OR (Status = 'Reserved' AND ExpiresAt > UtcNow)})$$
     3. **Kiểm tra ngưỡng đầy ca (`RecommendedCapacity`)**:
        - Nếu $\text{OnlineCount} \ge \text{RecommendedCapacity}$: **Bỏ qua toàn bộ ca trực này (không trả về bất kỳ slot nào)**, kể cả khi vẫn còn mốc giờ 15 phút chưa có ai đặt. Ca trực được xem là đã hết chỗ online.
     4. Nếu $\text{OnlineCount} < \text{RecommendedCapacity}$:
        - Sinh danh sách các khung giờ 15 phút phủ kín từ `ShiftStart` đến `ShiftEnd` (ví dụ `08:00`, `08:15`, `08:30`...).
        - Đánh dấu hoặc loại bỏ các khung giờ đã có người giữ chỗ (`Reserved` chưa hết hạn) hoặc đã xác nhận (`Confirmed`).
        - Trả về danh sách các slot trống còn lại để bệnh nhân tự do lựa chọn.
   - **Cam kết**: Tính toán hoàn toàn trên ca trực và reservations nội bộ của Scheduling Service, tuyệt đối không query sang `Appointments` table hay `ClinicDB`.

2. **Cụm API Reservation (Giữ chỗ - Xác nhận - Bồi hoàn)**:
   - `POST /schedules/{scheduleId}/reservations`:
     - Header bắt buộc: `Idempotency-Key`.
     - Body: `{ "timeSlot": "08:30:00", "patientId": "guid" }`.
     - Xử lý: Kiểm tra Idempotency $\rightarrow$ Bắt đầu Transaction có RowLock $\rightarrow$ **Kiểm tra 2 lớp**:
       1. Tổng số lượt đặt online hợp lệ (`OnlineCount`) phải $< \text{RecommendedCapacity}$ (nếu $\ge$, trả `409 Conflict: Ca làm việc đã đủ số lượng đặt online`).
       2. Khung giờ `timeSlot` cụ thể không được trùng với reservation active nào (nếu trùng, trả `409 Conflict: Khung giờ đã được người khác đặt`).
       $\rightarrow$ Tạo `SlotReservation` với hạn 10 phút $\rightarrow$ Commit Transaction $\rightarrow$ Trả về `201 Created`.
   - `POST /schedules/{scheduleId}/reservations/{reservationId}/confirm`:
     - Chuyển trạng thái `SlotReservation` từ `Reserved` sang `Confirmed`.
   - `POST /schedules/{scheduleId}/reservations/{reservationId}/release`:
     - Chuyển trạng thái `SlotReservation` từ `Reserved` sang `Released` để hồi phục capacity ngay lập tức.

3. **Background Service Tự Động Giải Phóng Slot Quá Hạn**:
   - Tạo `ReservationExpirationWorker : BackgroundService`.
   - Chu kỳ chạy: Mỗi 30 giây.
   - Nhiệm vụ: `UPDATE SlotReservations SET Status = 'Expired' WHERE Status = 'Reserved' AND ExpiresAt <= GETUTCDATE()`.

4. **Nghiệp vụ Phê Duyệt Ca & Phát Hiện Xung Đột (Shift Request Approval)**:
   - Endpoint: `POST /shifts/suggestions/{id}/approve`.
   - Logic: Kiểm tra xung đột lịch của bác sĩ trong cùng ngày $\rightarrow$ Chuyển `ShiftRequest.Status = Approved` $\rightarrow$ Thêm `WorkSchedule` $\rightarrow$ Ghi `WorkScheduleCreated` vào bảng `OutboxMessages` $\rightarrow$ Commit trong 1 local transaction.

---

### Bước 5: Tích Hợp Outbox Message Cho Schedule Events
1. **Định dạng Event Envelope**:
   - Sử dụng `EventEnvelope<WorkScheduleCreated>`, `EventEnvelope<WorkScheduleUpdated>`, `EventEnvelope<WorkScheduleCancelled>` từ `Clinic.Contracts`.
2. **Cài đặt `IOutboxWriter`**:
   - Service ghi dữ liệu outbox trực tiếp vào `SchedulingDbContext.OutboxMessages`.
   - Đảm bảo `producer = "Clinic.Scheduling.Service"`, `schemaVersion = 1`.
3. **Phối hợp với Thành viên B**:
   - Cung cấp schema bảng `OutboxMessages` và các event types để Thành viên B cấu hình RabbitMQ publisher worker.
   - Chuẩn bị consumer đón nhận sự kiện `UserProfileUpdated` từ Identity Service để cập nhật projection trong `Employees`.

---

### Bước 6: Cắt Chuyển (Cutover) & Tái Cấu Trúc Phía Monolith (`Clinic.API`)
1. **Bổ sung migration cho `Appointments` trong `ClinicDB`**:
   - Thêm các cột: `DoctorId` (Guid), `DoctorName` (nvarchar 200), `SpecialtyName` (nvarchar 100), `ScheduleDate` (DateOnly).
   - Chạy script data backfill để điền thông tin các cột này từ `WorkSchedules` và `Doctors` hiện có.
   - **Xóa bỏ Foreign Key** `FK_Appointments_WorkSchedules_WorkScheduleId` trong `ClinicDB`.
2. **Tích hợp Quy trình Đặt Lịch tại `CreateAppointmentCommandHandler`**:
   - Đăng ký `HttpClient` gọi tới `Clinic.Scheduling.Service`.
   - **Trường hợp 1: Bệnh nhân Đặt lịch Online (`IsWalkIn = false`)**:
     1. Gọi `POST /schedules/{id}/reservations` (ReserveSlot) kèm `Idempotency-Key` (bị ràng buộc bởi `RecommendedCapacity` và slot 15p).
     2. Nếu thành công, lưu `Appointment` mới vào `ClinicDB` với trạng thái `Pending` (`IsWalkIn = false`).
     3. Gọi `POST /schedules/{id}/reservations/{resId}/confirm` để chốt giữ chỗ $\rightarrow$ Chuyển `Appointment.Status = Confirmed`.
     4. Nếu bước lưu `Appointment` gặp lỗi, gọi `POST /schedules/{id}/reservations/{resId}/release` để giải phóng slot.
   - **Trường hợp 2: Lễ tân Tiếp nhận Khách Vãng lai tại Quầy (`IsWalkIn = true`)**:
     - Lễ tân trao đổi linh hoạt với bác sĩ để tiếp nhận khám trực tiếp.
     - **Bỏ qua bước `ReserveSlot` online**: Không cần giữ chỗ slot 15p cố định và **được phép vượt ngưỡng đề xuất (`RecommendedCapacity`)**.
     - Lưu trực tiếp `Appointment` vào `ClinicDB` với `IsWalkIn = true`, `Status = Confirmed` (hoặc `CheckedIn`), gán `WorkScheduleId`, `DoctorId` và thời điểm tiếp nhận thực tế.
     - Không tính vào `OnlineCount`, đảm bảo không ảnh hưởng đến hạn mức và trải nghiệm của các bệnh nhân đặt online.
3. **Chuyển hướng API Gateway**:
   - Cấu hình các route `/doctors/**`, `/shifts/**`, `/specialties/**`, `/employees/**`, `/schedules/**` chuyển tiếp sang `Clinic.Scheduling.Service`.
4. **Áp dụng Migration Dọn Dẹp (`RemoveSchedulingEntitiesFromClinicDb`)**:
   - Sau khi đối soát dữ liệu và kiểm thử phát hành thành công, áp dụng migration xóa các bảng `ShiftRequests`, `WorkSchedules`, `WorkHistories`, `Doctors`, `Employees`, `Specialties` khỏi `ClinicDB`.

---

### Bước 7: Kiểm Thử Tự Động & Tiêu Chí Nghiệm Thu (Acceptance)

1. **Unit Tests (`Clinic.Scheduling.Service.Tests`)**:
   - Test logic duyệt ca: Không cho duyệt ca đã bắt đầu hoặc ca có khung giờ chồng lấn với ca khác của bác sĩ.
   - Test logic tính slot khả dụng dựa trên khoảng cách 15 phút.
   - Test vòng đời trạng thái của `SlotReservation` (`Reserved -> Confirmed`, `Reserved -> Released`, `Reserved -> Expired`).

2. **Concurrency & Race-Condition Tests**:
   - Viết test mô phỏng **20 request gửi đồng thời** cùng đặt 1 slot duy nhất hoặc ca chỉ còn đúng 1 slot trống:
     - Kết quả mong muốn: Đúng **1 request thành công (201 Created)**, 19 request còn lại nhận **409 Conflict** (không bao giờ xảy ra overbooking).

3. **Idempotency & Timeout Tests**:
   - Gửi 2 request giống nhau với cùng `Idempotency-Key`: Request thứ hai phải trả về dữ liệu của request thứ nhất, không trừ thêm capacity.
   - Tạo reservation với hạn 1 giây, đợi 2 giây: kiểm tra `GetAvailability` phải hiển thị lại slot đó là khả dụng.

---

## 5. Kế Hoạch Rollback Dự Phòng (Rollback Strategy)

1. **Giai đoạn trước khi xóa bảng legacy ở `ClinicDB`**:
   - Nếu `Clinic.Scheduling.Service` gặp lỗi nghiêm trọng trong quá trình cutover:
     - Giữ nguyên dữ liệu trong `ClinicSchedulingDB` để điều tra log.
     - Cấu hình API Gateway chuyển ngược các route `/doctors`, `/shifts`, `/schedules` về Monolith.
     - Phục hồi lại code gọi trực tiếp trong Monolith.
2. **Giai đoạn sau khi xóa bảng legacy ở `ClinicDB`**:
   - Khôi phục `ClinicDB` từ bản snapshot backup đã thực hiện ngay trước thời điểm cutover.
   - Đồng bộ ngược các lịch hẹn phát sinh trong thời gian chạy thử nghiệm.

---

## 6. Bảng Tổng Hợp Trách Nhiệm Giữa Thành Viên A và Thành Viên B (GĐ 2)

| Nội dung công việc | Thành viên A (Backend & Data) | Thành viên B (Broker, QA & Obs) |
| :--- | :---: | :---: |
| Tạo service `Clinic.Scheduling.Service` & cấu hình JWKS | **Chủ trì** | Phối hợp |
| Thiết kế Schema `ClinicSchedulingDB` & EF Migrations | **Chủ trì** | Hỗ trợ review |
| Xây dựng API `GetAvailability`, `ReserveSlot`, `ReleaseSlot` | **Chủ trì** | Hỗ trợ review |
| Triển khai logic Atomic Capacity, Concurrency Lock & Timeout | **Chủ trì** | Kiểm thử độc lập |
| Bảng `OutboxMessages` & Ghi Outbox trong local transaction | **Chủ trì** | Hỗ trợ format |
| Triển khai `OutboxPublisher` đẩy event sang RabbitMQ | Hỗ trợ | **Chủ trì** |
| Xây dựng Consumer cho các schedule events & Profile Sync | Hỗ trợ | **Chủ trì** |
| Viết Concurrency Test & Contract Test tự động | Phối hợp | **Chủ trì** |
| Cấu hình Metrics (Reservation latency, failures, lag) | Hỗ trợ | **Chủ trì** |
| Tái cấu trúc Monolith `Appointments` và bỏ FK xuyên service | **Chủ trì** | Phối hợp |


---

## 7. Báo Cáo Nghiệm Thu & Cập Nhật Thay Đổi Thực Tế Sau Giai Đoạn 2

### 7.1. Trạng Thái Hoàn Thành Các Bước Kỹ Thuật
Toàn bộ 7 bước trong kế hoạch triển khai đã được hoàn tất và nghiệm thu đạt 100%:
1. **Bước 1 (Hạ tầng)**: Đã tạo `Clinic.Scheduling.Service` (.NET 10 Web API), thiết lập JWT Bearer qua discovery/JWKS của Identity Service (`/.well-known/jwks.json`), cấu hình Docker và Docker Compose.
2. **Bước 2 (Domain & Database)**: Thiết lập schema `ClinicSchedulingDB` với các bảng độc lập (`Employees`, `Doctors`, `Specialties`, `WorkHistories`, `WorkSchedules`, `ShiftRequests`, `SlotReservations`, `OutboxMessages`).
3. **Bước 3 (Di chuyển dữ liệu)**: Kịch bản import `import-from-clinicdb.sql` và kịch bản đối soát `verify-scheduling-import.sql` bảo đảm dữ liệu toàn vẹn giữa `ClinicDB` và `ClinicSchedulingDB`.
4. **Bước 4 (API nghiệp vụ cốt lõi)**:
   - Xây dựng API Availability (`GET /schedules/availability` / `/slots`) áp dụng Dual-Constraint capacity rule.
   - Xây dựng bộ API Reservation (`POST .../reservations`, `POST .../confirm`, `POST .../release`) với hạn 10 phút, RowLock transaction và Idempotency.
   - Background worker `ReservationExpirationWorker` chạy mỗi 30s giải phóng các slot quá hạn.
   - Duyệt ca `ShiftRequest.Approve()` atomic kèm conflict detection (trùng ca trong ngày).
5. **Bước 5 (Outbox & Integration Events)**:
   - Ghi outbox trong cùng local transaction cho `WorkScheduleCreated`, `WorkScheduleUpdated`, `WorkScheduleCancelled`.
   - Consumer cập nhật projection nhân viên khi nhận `UserProfileUpdated`.
6. **Bước 6 (Cutover phía Monolith)**:
   - Denormalize các trường `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate` vào bảng `Appointments`.
   - Xóa bỏ hoàn toàn Foreign Key `FK_Appointments_WorkSchedules_WorkScheduleId`.
   - Xóa bỏ hoàn toàn entity và bảng thừa `BookingSlot` khỏi `ClinicDB`.
   - Cài đặt Booking Saga trong `CreateAppointmentCommandHandler` (`ReserveSlot -> CreateAppointment -> ConfirmSlot / ReleaseSlot`).
7. **Bước 7 (Kiểm thử & Nghiệm thu)**:
   - `Clinic.Scheduling.Service.Tests`: 100% passed.
   - `Clinic.IntegrationTests` (`BaselineBusinessWorkflowTests`): 3/3 passed.
   - Không còn foreign key hoặc join xuyên database giữa Monolith và Scheduling Service.

### 7.2. Các Điểm Điều Chỉnh Kỹ Thuật Thực Tế So Với Thiết Kế Ban Đầu
Trong quá trình phát triển, kiểm thử và tích hợp thực tế, 6 nội dung kỹ thuật sau đã được tinh chỉnh:

1. **Xóa Bỏ Hoàn Toàn Entity & Bảng `BookingSlot` Trong Monolith (`ClinicDB`)**:
   - *Thực tế*: Trước đây trong Monolith tồn tại bảng `BookingSlots` được sinh trước. Khi quyền sở hữu lịch trực và năng lực phục vụ chuyển giao sang Scheduling Service, việc duy trì bảng `BookingSlots` trong `ClinicDB` trở nên dư thừa và gây phân mảnh dữ liệu.
   - *Điều chỉnh*: Toàn bộ entity `BookingSlot`, cấu hình `BookingSlotConfiguration` và bảng tương ứng trong `ClinicDB` đã được xóa bỏ hoàn toàn. Việc tính toán slot và giữ chỗ được ủy quyền 100% cho `Clinic.Scheduling.Service` qua cơ chế sinh slot động và bảng `SlotReservations`.

2. **Bổ Sung Quy Tắc Chặn Slot Trong Quá Khứ & Giới Hạn Tối Thiểu 30 Phút (`>= UtcNow + 30m`)**:
   - *Thực tế*: Thuật toán ban đầu sinh tất cả các slot từ `ShiftStart` đến `ShiftEnd` trong ngày, dẫn đến việc người dùng có thể thấy và đặt các slot thuộc ca trực của ngày hiện tại nhưng giờ khám đã trôi qua.
   - *Điều chỉnh*: Logic sinh slot tại `SchedulingManager` và Monolith `GetDoctorSchedulesForBooking` đã được bổ sung bộ lọc: chỉ sinh và hiển thị các slot trong tương lai cách thời điểm hiện tại tối thiểu 30 phút (`slotDateTimeUtc >= nowUtc.AddMinutes(30)`).

3. **Chuẩn Hóa Múi Giờ: UTC tại Backend & Giờ Địa Phương (GMT+7) tại Frontend**:
   - *Thực tế*: Backend chuẩn hóa lưu trữ và trả về toàn bộ mốc thời gian dạng UTC (ISO 8601). Tuy nhiên trên giao diện khám bệnh của Bác sĩ (`DoctorExaminationView.vue`), thời gian bắt đầu khám (`startExam`) bị hiển thị thô theo UTC, gây lệch 7 tiếng so với giờ thực tế của người dùng.
   - *Điều chỉnh*: Cập nhật frontend để chuyển đổi và format thời gian khám theo múi giờ địa phương của trình duyệt (GMT+7) khi hiển thị.

4. **Khắc Phục Lỗi Quản Lý Phiên & Điều Hướng Đa Vai Trò (Multi-Role) trên Frontend**:
   - *Header Profile*: Tên người dùng không cập nhật khi đăng xuất tài khoản này và đăng nhập tài khoản khác (phải F5 thủ công). Đã khắc phục bằng cách xóa sạch state khi logout và kích hoạt `fetchMe` đồng bộ profile ngay khi đăng nhập thành công.
   - *Sidebar Navigation*: Người dùng có nhiều vai trò (ví dụ: Lễ tân + Bác sĩ + Bệnh nhân) khi chọn vai trò Bác sĩ/Bệnh nhân thì Sidebar bên trái vẫn ghim cứng menu của Lễ tân. Đã sửa logic Sidebar để phản ứng linh hoạt theo `activeRole` được người dùng chọn.

5. **Gộp (Squash) Các Migration Trong Giai Đoạn 2 Phía Monolith (`ClinicDB`)**:
   - *Thực tế*: Trong quá trình phát triển Giai đoạn 2, nhiều migration trung gian được tạo ra (thêm cột denormalized, xóa FK, xóa BookingSlot).
   - *Điều chỉnh*: Toàn bộ các migration của giai đoạn 2 phía `Clinic.Infrastructure.Sqlserver` đã được gộp lại thành 1 migration duy nhất sạch sẽ: `20261021175324_DecoupleSchedulingAndRemoveBookingSlots.cs`.

6. **Tự Động Hóa Vòng Đời Database Kiểm Thử Tích Hợp (`ApiContractFactory.cs`)**:
   - *Thực tế*: Khi chạy `BaselineBusinessWorkflowTests`, hệ thống tạo database tạm `ClinicBaselineTests_<guid>`. Do Web Host và connection pool chưa giải phóng khi gọi `EnsureDeletedAsync`, SQL Server từ chối xóa DB, dẫn đến tồn đọng 21 database test trên SQL Server.
   - *Điều chỉnh*: Cập nhật `ApiContractFactory.DisposeAsync` để dừng WebHost trước (`base.DisposeAsync()`), dọn dẹp connection pool (`SqlConnection.ClearAllPools()`), kết nối tới `master` và thực thi lệnh ngắt kết nối cưỡng bức `SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ...`. Đồng thời bổ sung bộ dọn dẹp mồ côi (Orphan Cleanup) quét và xóa các DB test từ các PID đã chết.

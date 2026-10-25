# Kế Hoạch Hoàn Tất Chuyển Đổi Giai Đoạn 2: Staff & Scheduling Service (Member A)

> **Mục tiêu tài liệu:** Rà soát đối chiếu giữa [`TASK_MEMBER_A.md`](./TASK_MEMBER_A.md), [`PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md`](./PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md) (Mục 8) và mã nguồn đang thay đổi trong working tree; xác định các phần còn sót và lập kế hoạch chi tiết để chuyển 100% quyền sở hữu nghiệp vụ & dữ liệu Staff/Scheduling sang `Clinic.Scheduling.Service`, dọn dẹp hoàn toàn code/schema legacy khỏi `ClinicDB`.

> **Ghi chú trạng thái:** Mục 2.1–2.2 là ảnh chụp audit trước khi hoàn tất, không phải danh sách tồn đọng hiện hành. Giai đoạn 2 đã đóng; kết quả cuối và kiểm chứng tiếp nối nằm ở [`PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md`](./PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md), mục 8.7–8.8. Các số liệu/trạng thái cũ trong bảng 2.2 không được dùng làm blocker hiện tại.

---

## 1. Nguyên Tắc Kiến Trúc Bắt Buộc

1. **Đơn nguồn sự thật (Single Source of Truth - SSOT):**
    - `Clinic.Scheduling.Service` (`ClinicSchedulingDB`) sở hữu 100% nghiệp vụ và dữ liệu của `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`, `SlotReservation`.
    - `Clinic.Identity.Service` (`ClinicIdentityDB`) sở hữu tài khoản (`IdentityUser`), mật khẩu, phân quyền (`Role`/`Permission`) và thông tin nhân thân gốc (`PersonProfile`).
    - `Clinic` (`ClinicDB`) **tuyệt đối không** lưu bản sao, không dual-write, không giữ bảng hoặc handler xử lý hộ cho `Staff & Scheduling`.
2. **Không truy vấn/FK xuyên Database:**
    - Các dữ liệu liên quan đến bác sĩ/ca trực ở `Appointment`, `MedicalReport`, `Notification` chỉ được giữ dưới dạng ID và các trường snapshot (`WorkScheduleId`, `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate`).
3. **Dọn dẹp triệt để sau Cutover:**
    - Sau khi tách toàn bộ caller và đối soát dữ liệu thành công, phải xóa bỏ toàn bộ MediatR handlers, repositories, `DbSet`, entity configurations và tạo EF Core Migration để drop các bảng Staff/Scheduling khỏi `ClinicDB`.

---

## 2. Hiện Trạng Triển Khai Trong Working Tree

### 2.1. Phần đã hoàn thành trong Working Tree

1. **Tại `Clinic.Scheduling.Service`:**
    - Đã mở rộng entity `Employee` thêm `DateOfBirth`, `Address` và tạo migration `20261024030812_AddEmployeeIdentityProfileFields`.
    - Đã bổ sung `StaffSchedulingEndpoints.cs` xử lý trực tiếp trên `ClinicSchedulingDB` cho toàn bộ các nhóm:
        - **Employee:** `GET /employees`, `GET /employees/me`, `GET /employees/{id}`, `POST /employees`, `PUT /employees/{id}`, `PUT /employees/me`, `POST /employees/{id}/status`.
        - **Specialty:** `GET /specialties`, `GET /specialties/{id}`, `POST /specialties`, `PUT /specialties/{id}`, `PATCH /specialties/{id}/status`.
        - **Doctor:** `GET /doctors`, `GET /doctors/available`, `GET /doctors/me`, `GET /doctors/{id}`, `GET /doctors/{id}/available`, `GET /doctors/{id}/staff-link`, `POST /doctors`, `PUT /doctors/{id}`, `PUT /doctors/me`, `PATCH /doctors/{id}/status`, `PATCH /doctors/{id}/specialty`.
        - **WorkSchedule & ShiftRequest:** `GET /doctors/shifts`, `GET /doctors/{doctorId}/shifts`, `POST /doctors/{doctorId}/shifts`, `GET /doctors/{doctorId}/suggestions`, `GET /shifts`, `GET /shifts/suggestions`, `POST /shifts/suggestions`, `GET /shifts/{id}`, `PUT /shifts/{id}`, `PATCH /shifts/{id}`, `PATCH /shifts/{id}/cancel`, `PATCH /shifts/suggestions/{id}`.
    - Đã bổ sung `GET /schedules/walk-in-candidates` trong `SchedulingEndpoints.cs` và cập nhật `SchedulingManager.HandleUserProfileUpdatedAsync` đồng bộ cả `DateOfBirth`, `Address`.

2. **Tại `Clinic.API` & `Clinic.Identity.Service`:**
    - Đã thêm `SchedulingProxyMiddleware` chuyển tiếp các request `/employees/**`, `/doctors/**`, `/specialties/**`, `/shifts/**` sang `Clinic.Scheduling.Service` (ngoại trừ các route `/queue` và các route điều phối provisioning/profile update).
    - Đã cập nhật `EmployeesController` và `DoctorsController` để điều phối các thao tác tạo mới (`POST /employees`, `POST /employees/from-user`, `POST /doctors`, `POST /doctors/from-user`) và cập nhật hồ sơ (`PUT /employees/{id}`, `PUT /employees/me`, `PUT /doctors/{id}`, `PUT /doctors/me`) giữa `Identity` và `Scheduling`.
    - Đã bổ sung `PUT /v1/internal/users/{userId}/profile` tại `Clinic.Identity.Service` để cập nhật hồ sơ nhân thân gốc trước khi đồng bộ projection sang `Scheduling`.

3. **Tại `Clinic.Appointment.Service` (liên đới từ Phase 3):**
    - Đã chuyển `POST /receptionist/appointments/walk-in` và `GET /receptionist/working-doctors` sang `Clinic.Appointment.Service` (gọi `ISchedulingClient.GetWalkInCandidatesAsync`), xóa `CreateWalkInAppointment` và `GetWorkingDoctorsForToday` khỏi `Clinic.Application`.

---

### 2.2. Phần đang triển khai dở / còn sót cần hoàn tất

| STT   | Hạng mục còn thiếu                                                                                                                      | Chi tiết vị trí trong mã nguồn                                                                                                                                                                                                                                                                                                                     | Tác động nếu chưa xử lý                                                                                                                                                                                                          |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1** | **Provisioning vẫn đọc `person.Employee` / `Doctor` từ `ClinicDB`**                                                                     | `EmployeesController.Create` (`L88`), `DoctorsController.Create` (`L159`), `EnsureIdentityRegistrationProfileHandler` (`ProvisionIdentityPatient.cs`), `PersonRepository.cs`                                                                                                                                                                       | Khi tạo nhân viên/bác sĩ mới, `Clinic.API` vẫn kiểm tra `person.Employee` và `person.Employee.Doctor` trong `ClinicDB` thay vì dựa vào `Identity`/`Scheduling`, ngăn cản việc xóa bảng `Employees` và `Doctors` khỏi `ClinicDB`. |
| **2** | **Admin Dashboard vẫn truy vấn `IDoctorRepository` trong `ClinicDB`**                                                                   | `GetDashboardDataHandler` (`Features/Admin/Queries/GetDashboardData.cs`) gọi `_doctorRepository.GetDashboardData()`                                                                                                                                                                                                                                | `GET /admin/dashboard` vẫn đếm `TotalDoctors` và `ActiveDoctors` từ bảng `Doctors` cũ trong `ClinicDB`.                                                                                                                          |
| **3** | **Các luồng `MedicalReports`, `Admin`, `Notifications` và `AppointmentRepository` vẫn phụ thuộc navigation `Appointment.WorkSchedule`** | - `SaveMedicalReportHandler` (`L48`, `L96-98`) truy cập thẳng `appointment.WorkSchedule.DoctorId`, `appointment.WorkSchedule.Date`<br>- `FinalizeMedicalReportHandler`<br>- `Features/MedicalReports/Queries/*`<br>- `GetAppointmentsByDateHandler`<br>- `Notifications/Templates/*`<br>- `AppointmentRepository.cs`, `MedicalReportRepository.cs` | Gây lỗi `NullReferenceException` hoặc lỗi join SQL ngay khi gỡ bảng `WorkSchedules`, `Doctors`, `Employees`, `Specialties`, `WorkHistories` khỏi `ClinicDB`.                                                                     |
| **4** | **Dead Code trong Controllers, MediatR Handlers và Repositories của `Clinic`**                                                          | - `EmployeesController`, `DoctorsController`, `SpecialtiesController`, `ShiftsController`, `SlotsController`<br>- `Features/Employees/**`, `Features/Doctors/**`, `Features/Specialties/**`, `Features/WorkSchedules/**`, `Features/Slots/**`<br>- `EmployeeRepository`, `DoctorRepository`, `SpecialtyRepository`, `WorkScheduleRepository`       | Dư thừa mã nguồn không còn dùng tới, giữ các tham chiếu trực tiếp tới entity và `DbSet` legacy trong `Clinic.Application` và `Clinic.Infrastructure.Sqlserver`.                                                                  |
| **5** | **Chưa chạy Import & Đối soát dữ liệu `ClinicDB` → `ClinicSchedulingDB`**                                                               | Các bảng `Specialties`, `Employees`, `Doctors`, `WorkHistories`, `WorkSchedules`, `ShiftRequests` và liên kết claims `employeeId`/`doctorId` bên `ClinicIdentityDB`                                                                                                                                                                                | Có nguy cơ lệch dữ liệu lịch sử hoặc đứt liên kết `employeeId`/`doctorId` trên tài khoản hiện có nếu xóa bảng trước khi đối soát.                                                                                                |
| **6** | **Schema Legacy vẫn còn trong `ApplicationDbContext` và `ClinicDB`**                                                                    | - `ApplicationDbContext.cs` (`DbSet<Employee>`, `DbSet<Doctor>`, `DbSet<Specialty>`, `DbSet<WorkHistory>`, `DbSet<WorkSchedule>`, `DbSet<ShiftRequest>`)<br>- `Configurations/*Configuration.cs` & `SampleData.cs`                                                                                                                                 | Chưa đạt tiêu chí đóng Giai đoạn 2 (Mục 8.6): `ClinicDB` vẫn còn giữ 6 bảng Staff/Scheduling và FK từ `Appointments` sang `WorkSchedules`.                                                                                       |

---

## 3. Kế Hoạch Triển Khai Hoàn Tất (5 Bước)

### Bước 1: Tách toàn bộ Caller còn lại trong `Clinic` khỏi các Entity Staff & Scheduling [ĐÃ HOÀN TẤT]

1. **Loại bỏ Navigation sang Staff/Scheduling trên `Person` và `Appointment` (`Clinic.Domain`):**
    - Trong `Clinic.Domain/Entities/Person.cs`: Xóa thuộc tính navigation `public Employee? Employee { get; private set; }`.
    - Trong `Clinic.Domain/Entities/Appointment.cs`: Xóa thuộc tính navigation `public WorkSchedule WorkSchedule { get; private set; } = null!;` (chỉ giữ `WorkScheduleId`, `DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate` làm snapshot/ID).
    - Trong `Clinic.Infrastructure.Sqlserver/Configurations/AppointmentConfiguration.cs`: Bỏ cấu hình FK `.HasOne(a => a.WorkSchedule).WithMany(ws => ws.Appointments).HasForeignKey(a => a.WorkScheduleId)`.

2. **Cập nhật các Handlers, Queries, Templates và Repositories dùng `Appointment` và `Person`:**
    - **`SaveMedicalReport.cs`:** Thay `appointment.WorkSchedule.DoctorId` bằng `appointment.DoctorId`, `appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName` bằng `appointment.DoctorName`, và `appointment.WorkSchedule.Date` bằng `appointment.ScheduleDate != default ? appointment.ScheduleDate : DateOnly.FromDateTime(appointment.CreatedAt)`.
    - **`FinalizeMedicalReport.cs` & `Features/MedicalReports/Queries/*` (`GetMedicalReportByAppointment.cs`, `GetMedicalReportById.cs`, `GetMedicalReportsQuery.cs`, `GetMyMedicalHistory.cs`, `GetPatientMedicalHistoryForDoctor.cs`):** Chỉ đọc `appointment.DoctorId`, `appointment.DoctorName`, `appointment.SpecialtyName`, `appointment.ScheduleDate` từ snapshot của `Appointment`.
    - **`GetAppointmentsByDate.cs`:** Đọc trực tiếp `a.DoctorId`, `a.DoctorName`, `a.SpecialtyName`, `a.ScheduleDate` (không truy cập `a.WorkSchedule`).
    - **`Notifications/Templates/*` (`AppointmentConfirmationTemplate.cs`, `AppointmentReminderTemplate.cs`, `AppointmentCancelledTemplate.cs`, `AppointmentUpdatedTemplate.cs`):** Đọc trực tiếp `appointment.DoctorName` và `appointment.ScheduleDate`.
    - **`AppointmentRepository.cs` & `MedicalReportRepository.cs`:** Xóa toàn bộ `.Include(a => a.WorkSchedule).ThenInclude(...)` và thay điều kiện lọc theo `a.WorkSchedule.Date` / `a.WorkSchedule.DoctorId` bằng `a.ScheduleDate` / `a.DoctorId`.
    - **`PersonRepository.cs` & `ProvisionIdentityPatient.cs` (`EnsureIdentityRegistrationProfileHandler`):**
        - Xóa `.Include(p => p.Employee).ThenInclude(e => e.Doctor)` trong `PersonRepository.cs`.
        - Cập nhật `EnsureIdentityRegistrationProfileHandler` chỉ tạo/lấy `Person` và `PatientId` cho hồ sơ bệnh nhân đi kèm tài khoản; việc kiểm tra tài khoản đã có hồ sơ `Employee`/`Doctor` hay chưa sẽ do `IdentityAccountClient` và `Clinic.Scheduling.Service` đảm nhiệm.

3. **Chuyển thống kê bác sĩ của `GET /admin/dashboard` sang `Clinic.Scheduling.Service`:**
    - Bổ sung endpoint thống kê nội bộ/dashboard (hoặc mở rộng `ISchedulingServiceClient`) tại `Clinic.Scheduling.Service` trả về `TotalDoctors` và `ActiveDoctors`.
    - Cập nhật `GetDashboardDataHandler` (`Features/Admin/Queries/GetDashboardData.cs`) gọi `ISchedulingServiceClient` thay vì `IDoctorRepository`, loại bỏ hoàn toàn phụ thuộc vào bảng `Doctors` trong `ClinicDB`.

**Tiêu chí hoàn tất Bước 1:** Không còn bất kỳ file nào ngoài nhóm Staff/Scheduling legacy tham chiếu tới `Employee`, `Doctor`, `Specialty`, `WorkHistory`, `WorkSchedule`, `ShiftRequest`.

---

### Bước 2: Dọn dẹp Dead Code (Controllers, MediatR Handlers, Repositories) của Staff & Scheduling trong `Clinic` [ĐÃ HOÀN TẤT]

1. **Tinh gọn các Controllers tại `Clinic.API/Controllers`:**
    - `EmployeesController.cs`: Giữ lại các action orchestration với Identity (`Create`, `CreateFromUser`, `Update`, `UpdateOwnProfile`), chuyển các action còn lại sang proxy qua `SchedulingManagementClient` (hoặc xóa các action đã được `SchedulingProxyMiddleware` xử lý), bỏ `ISender` cho các command/query `Features/Employees`.
    - `DoctorsController.cs`: Giữ lại các action orchestration với Identity (`Create`, `CreateFromUser`, `Update`, `UpdateOwnProfile`) và các route Queue (`GetQueue`, `CallNext`), bỏ toàn bộ các lệnh gọi `_sender.Send(...)` tới `Features/Doctors` và `Features/WorkSchedules`.
    - `SpecialtiesController.cs`, `ShiftsController.cs`, `SlotsController.cs`: Chuyển thành thin proxy gọi `SchedulingManagementClient` hoặc để `SchedulingProxyMiddleware` đảm nhiệm hoàn toàn; xóa phụ thuộc vào `Features/Specialties`, `Features/WorkSchedules`, `Features/Slots`.

2. **Xóa toàn bộ MediatR Handlers và Repositories Legacy khỏi `Clinic.Application` & `Clinic.Infrastructure.Sqlserver`:**
    - Xóa các thư mục trong `Clinic.Application/Features`:
        - `Features/Employees`
        - `Features/Doctors`
        - `Features/Specialties`
        - `Features/WorkSchedules`
        - `Features/Slots`
    - Xóa các interface repository không còn dùng trong `Clinic.Application/Interfaces`:
        - `IEmployeeRepository.cs`
        - `IDoctorRepository.cs`
        - `ISpecialtyRepository.cs`
        - `IWorkScheduleRepository.cs`
    - Xóa các implementation repository trong `Clinic.Infrastructure.Sqlserver/Repositories`:
        - `EmployeeRepository.cs`
        - `DoctorRepository.cs`
        - `SpecialtyRepository.cs`
        - `WorkScheduleRepository.cs`
    - Cập nhật `Clinic.Infrastructure.Sqlserver/DependencyInjection.cs` để gỡ đăng ký DI của 4 repository trên.

**Tiêu chí hoàn tất Bước 2:** Build `Clinic.Application`, `Clinic.Infrastructure.Sqlserver`, `Clinic.API` thành công mà không còn bất kỳ handler hay repository Staff/Scheduling nào trong `Clinic`.

---

### Bước 3: Chạy Import & Đối soát dữ liệu `ClinicDB` → `ClinicSchedulingDB`

1. **Thực thi kịch bản Import & Đối soát Idempotent:**
    - Kiểm tra dữ liệu hiện có trong `ClinicDB` (`Specialties`, `Employees`, `Doctors`, `WorkHistories`, `WorkSchedules`, `ShiftRequests`) so với `ClinicSchedulingDB` và `ClinicIdentityDB`.
    - Đồng bộ đầy đủ các bản ghi còn thiếu từ `ClinicDB` sang `ClinicSchedulingDB` (giữ nguyên Primary Key `Id`, `PersonId`, `IdentityUserId`).
    - Cập nhật các cột snapshot (`DoctorId`, `DoctorName`, `SpecialtyName`, `ScheduleDate`) cho các bản ghi `Appointments` hiện có trong `ClinicDB` nếu còn bản ghi cũ chưa được điền snapshot trước khi gỡ FK `WorkScheduleId`.

2. **Lập bảng báo cáo đối soát:**
    - Đối chiếu số lượng bản ghi (count) trước và sau import cho từng bảng:
        - `Specialties`
        - `Employees`
        - `Doctors`
        - `WorkHistories`
        - `WorkSchedules`
        - `ShiftRequests`
    - Kiểm tra tính toàn vẹn của `employeeId` và `doctorId` lưu trong `ClinicIdentityDB` đảm bảo 100% trỏ tới các bản ghi tồn tại trong `ClinicSchedulingDB`.

**Tiêu chí hoàn tất Bước 3:** 100% dữ liệu Staff/Scheduling đã nằm ở `ClinicSchedulingDB`, toàn bộ liên kết `Identity` ↔ `Scheduling` khớp tuyệt đối.

**Kết quả triển khai và đối soát ngày 2026-10-24:**

- Đã nâng cấp [`import-from-clinicdb.sql`](../backend/Clinic.Scheduling.Service/Persistence/import-from-clinicdb.sql) thành kịch bản update-then-insert, chạy lại an toàn, giữ nguyên khóa và đồng bộ `PersonId`, `IdentityUserId`, `DateOfBirth`, `Address` cùng các trường nghiệp vụ.
- Đã mở rộng [`verify-scheduling-import.sql`](../backend/Clinic.Scheduling.Service/Persistence/verify-scheduling-import.sql) để kiểm tra count của cả sáu bảng, source ID coverage, quan hệ nội bộ, projection Employee, liên kết `ClinicIdentityDB.Users.EmployeeId/DoctorId` và snapshot Appointment.
- Đã cấu hình `scheduling-data-importer` chạy verifier ngay sau importer trong `docker-compose.yml`.
- Kết quả chạy thực tế trên SQL Server: `Specialties 5/5`, `Doctors 15/15`, `WorkHistories 15/15`, `ShiftRequests 3/3`; toàn bộ source rows thiếu ở Scheduling là `0`, projection và liên kết Identity đều `0` mismatch, snapshot Appointment không đầy đủ là `0`.
- Có `41 Employees` và `84 WorkSchedules` chỉ tồn tại ở Scheduling, được giữ lại vì là dữ liệu mới hơn do Scheduling sở hữu; verifier báo riêng nhóm `TargetOnlyRows`, không coi đây là dữ liệu nguồn bị mất. Lần chạy importer thứ hai không tạo thêm bản ghi, xác nhận tính idempotent.

**Đối chiếu tiêu chí:** Đạt phạm vi dữ liệu nguồn 100% và liên kết `Identity` ↔ `Scheduling` 100%. Nhóm `TargetOnlyRows` cần được rà soát nghiệp vụ trước Bước 4, không được xóa tự động trong migration cutover.

---

### Bước 4: Gỡ bỏ Schema Staff & Scheduling khỏi `ClinicDB` bằng EF Core Migration

1. **Xóa Entities, DbSets, Configurations và Seed Data khỏi `Clinic`:**
    - Xóa các file entity trong `Clinic.Domain/Entities`:
        - `Employee.cs`
        - `Doctor.cs`
        - `Specialty.cs`
        - `WorkHistory.cs`
        - `WorkSchedule.cs`
        - `ShiftRequest.cs`
    - Xóa các `DbSet` tương ứng trong `Clinic.Infrastructure.Sqlserver/Persistence/ApplicationDbContext.cs`.
    - Xóa các file configuration trong `Clinic.Infrastructure.Sqlserver/Configurations`:
        - `EmployeeConfiguration.cs`
        - `DoctorConfiguration.cs`
        - `SpecialtyConfiguration.cs`
        - `WorkHistoryConfiguration.cs`
        - `WorkScheduleConfiguration.cs`
        - `ShiftRequestConfiguration.cs`
    - Dọn dẹp phần seed dữ liệu Staff/Scheduling trong `SampleData.cs` của `Clinic.Infrastructure.Sqlserver`.

2. **Tạo và áp dụng EF Core Migration trên `ClinicDB`:**
    - Tạo migration mới (ví dụ: `RemoveLegacyStaffAndSchedulingSchema`) trong `Clinic.Infrastructure.Sqlserver`.
    - Kiểm tra nội dung migration đảm bảo:
        - Gỡ Foreign Key từ `Appointments.WorkScheduleId` sang `WorkSchedules.Id`.
        - Drop 6 bảng: `ShiftRequests`, `WorkSchedules`, `WorkHistories`, `Doctors`, `Employees`, `Specialties`.
        - Giữ nguyên vẹn dữ liệu `Patients`, `Persons` (phục vụ Patient), `MedicalReports`, `Notifications`.

**Tiêu chí hoàn tất Bước 4:** `ClinicDB` sau khi chạy migration không còn 6 bảng Staff/Scheduling; `Clinic.API` khởi động và kết nối `ClinicDB` bình thường.

**Kết quả triển khai và đối chiếu ngày 2026-10-24:**

- Đã xóa sáu entity legacy, sáu EF configuration, sáu `DbSet` khỏi `ApplicationDbContext` và toàn bộ seed Staff/Scheduling khỏi `SampleData`.
- Đã giữ `WorkScheduleId` cùng các trường snapshot trên `Appointment`/`AppointmentSnapshot` như identifier dữ liệu lịch sử, không còn navigation hoặc FK sang `ClinicDB` Staff/Scheduling.
- Đã tạo và áp dụng migration [`20261024055019_RemoveLegacyStaffAndSchedulingSchema`](../backend/Clinic.Infrastructure.Sqlserver/Migrations/20261024055019_RemoveLegacyStaffAndSchedulingSchema.cs). Migration drop constraint/index có điều kiện để tương thích với các môi trường đã gỡ FK từ migration trước.
- Kiểm tra trực tiếp `ClinicDB`: sáu bảng `Employees`, `Doctors`, `Specialties`, `WorkHistories`, `WorkSchedules`, `ShiftRequests` không còn; các bảng `Persons`, `Patients`, `Appointments`, `MedicalReports`, `Notifications` vẫn tồn tại với dữ liệu.
- `Clinic.API` build thành công, container backend khởi động, EF báo database up-to-date và `GET /readyz` trả HTTP `200`.

**Đối chiếu tiêu chí:** Đạt mục tiêu Bước 4: schema Staff/Scheduling đã được gỡ khỏi `ClinicDB`, dữ liệu ClinicDB cần giữ nguyên, và API hoạt động trên schema sau cutover.

---

### Bước 5: Cập nhật Tests, Kiểm thử E2E trên Docker/Frontend & Nghiệm thu đóng Phase 2

1. **Cập nhật Unit Tests & Integration Tests:**
    - Dọn dẹp các unit test của handlers/entities Staff & Scheduling cũ trong `Clinic.Application.UnitTests` và `Clinic.Domain.UnitTests` (đã chuyển sang `Clinic.Scheduling.Service.Tests`), cập nhật `TestDataFactory.cs`.
    - Bổ sung các bài test còn thiếu trong `Clinic.Scheduling.Service.Tests` cho các endpoint trong `StaffSchedulingEndpoints.cs`.
    - Chạy toàn bộ test suites trong solution đảm bảo pass 100%.

2. **Kiểm thử thực tế trên Docker Compose & Frontend:**
    - Build và khởi chạy toàn bộ hệ thống bằng `docker compose up -d --build`.
    - Kiểm chứng các luồng nghiệp vụ có xác thực (Admin, Doctor, Receptionist, Patient):
        - Admin tạo/cập nhật/đổi trạng thái Employee, Doctor, Specialty.
        - Doctor gửi đề xuất ca trực (`ShiftRequest`), xem lịch trực cá nhân; Admin duyệt/từ chối đề xuất ca trực và tạo/hủy ca trực.
        - Patient/Receptionist tra cứu danh sách bác sĩ, chuyên khoa, khung giờ trống (`availability`) và đặt lịch khám (online + walk-in).
        - Kiểm tra `GET /admin/dashboard` trả đúng số lượng bác sĩ từ `Scheduling`.
    - Kiểm tra trực tiếp trên SQL Server container: xác nhận `ClinicDB` không còn các bảng `Employees`, `Doctors`, `Specialties`, `WorkHistories`, `WorkSchedules`, `ShiftRequests`.

3. **Cập nhật tài liệu:**
    - Cập nhật kết quả đối soát dữ liệu, kết quả kiểm thử và đóng mục 8.7 trong [`PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md`](./PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md) cùng trạng thái Giai đoạn 2 trong [`TASK_MEMBER_A.md`](./TASK_MEMBER_A.md).

**Kết quả triển khai và đối chiếu ngày 2026-10-24:**

- Đã chuyển `Clinic.Application.UnitTests` sang fixture snapshot không phụ thuộc entity Staff/Scheduling; kết quả `84/84` pass.
- Đã xóa các test domain Staff/Scheduling cũ và giữ lại test cho Appointment, Patient, Person, QueueTicket, MedicalReport; kết quả `40/40` pass.
- `Clinic.Infrastructure.UnitTests` pass `18/18`; `Clinic.Contracts.UnitTests` pass `3/3`.
- Scheduling Service test assembly pass `35/35`; sau khi ổn định thứ tự khởi động SQL Server bằng `docker compose up --wait`, các test HTTP không còn lỗi pre-login.
- Full solution pass `271/271`, gồm Domain `40/40`, Application `84/84`, Contracts `3/3`, Infrastructure `18/18`, Scheduling `35/35`, Queue `44/44`, Identity `4/4`, Appointment `32/32` và Integration `11/11`.
- `docker compose up -d --build --wait` thành công; frontend, SQL Server, backend, Scheduling, Identity, Appointment và Queue healthy. Smoke host trả HTTP `200` cho frontend, backend `/readyz`, `/doctors`, `/specialties` và Scheduling `/doctors`.

**Đối chiếu tiêu chí:** Đạt mục tiêu Bước 5: test legacy đã được dọn, toàn bộ solution pass, Docker stack build/health/smoke pass và `ClinicDB` không còn sáu bảng Staff/Scheduling. Các luồng xác thực API được covered bởi Integration/Service tests; smoke frontend xác nhận frontend container hoạt động.

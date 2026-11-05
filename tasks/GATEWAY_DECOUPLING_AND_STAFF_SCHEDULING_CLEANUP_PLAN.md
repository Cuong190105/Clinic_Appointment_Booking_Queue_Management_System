# Kế hoạch dọn dẹp Gateway và hoàn tất ownership Staff & Scheduling

## 1. Mục tiêu

Đưa hệ thống về kiến trúc microservices rõ ràng, trong đó:

- `Clinic.StaffScheduling.Service` sở hữu hoàn toàn Employee, Doctor, Specialty, WorkHistory, ShiftRequest và WorkSchedule.
- `Clinic.API` chỉ là API Gateway, chịu trách nhiệm routing/proxy, authentication, authorization, rate limiting, observability và downstream health checks.
- Gateway không còn domain logic, CQRS/MediatR, Entity Framework, database connection, repository, unit of work, migration, outbox hoặc compatibility layer.
- Không cần bảo toàn API cũ vì hệ thống chưa triển khai production.

## 2. Kiến trúc đích

| Thành phần              | Ownership                                                            |
| ----------------------- | -------------------------------------------------------------------- |
| Identity Service        | User, role, permission, token                                        |
| Staff & Scheduling Service      | Employee, Doctor, Specialty, WorkHistory, ShiftRequest, WorkSchedule |
| Appointment Service     | Appointment và reservation workflow                                  |
| Queue Service           | Check-in và queue ticket                                             |
| Medical Records Service | Patient, medical record, audit                                       |
| Notification Service    | Notification và delivery                                             |
| API Gateway             | Edge routing, auth, policy, rate limiting, observability, proxy      |

## 3. Nguyên tắc triển khai

- Chuyển workflow trước, xóa dependency sau.
- Không để Gateway tạo hoặc ghi profile/domain data.
- Không dùng `ApplicationDbContext` hoặc database của service khác từ Gateway.
- Không giữ route compatibility chỉ để bảo toàn client cũ.
- Mọi cross-service workflow phải dùng API/event, retry và compensation phù hợp.
- Sau mỗi phase phải build/test phần bị ảnh hưởng trước khi chuyển phase tiếp theo.

## 4. Phase 1 — Chốt contract Staff & Scheduling

### Công việc

1. Xác định contract chính thức cho:
    - `/employees`
    - `/doctors`
    - `/specialties`
    - `/shifts`
    - `/schedules`
2. Hoàn thiện [StaffSchedulingEndpoints.cs](../backend/Clinic.StaffScheduling.Service/Endpoints/StaffSchedulingEndpoints.cs) để Staff & Scheduling Service xử lý toàn bộ staff lifecycle.
3. Xác định workflow tạo staff:
    - tạo/lấy account tại Identity;
    - tạo Employee/Doctor tại Scheduling;
    - tạo projection Patient tại Medical Records nếu cần;
    - xử lý lỗi, retry và compensation.
4. Chuẩn hóa DTO tại Scheduling hoặc contracts riêng; Gateway không lấy DTO từ `Clinic.Application`.

### Điều kiện đạt

- Không còn nghiệp vụ tạo staff trong Gateway.
- Scheduling có API đầy đủ cho create, update, status và profile linkage.
- Có test cho thành công, trùng số điện thoại, lỗi Identity, lỗi Medical Records và retry/compensation.

## 5. Phase 2 — Chuyển orchestration staff khỏi Gateway

### Công việc

Chuyển logic từ:

- [DoctorsController.cs](../backend/Clinic.API/Controllers/DoctorsController.cs)
- [EmployeesController.cs](../backend/Clinic.API/Controllers/EmployeesController.cs)

sang workflow/application service trong Scheduling.

Loại bỏ khỏi Gateway:

- `ISender`;
- `EnsureIdentityRegistrationProfileCommand`;
- `PersonId` creation từ monolith;
- `ProfileProjectionSyncClient`;
- chuỗi điều phối trực tiếp Identity → Medical Records → Scheduling;
- logic role/profile activation trong controller.

File [EnsureIdentityRegistrationProfile.cs](../backend/Clinic.Application/Features/Auth/Commands/EnsureIdentityRegistrationProfile.cs) chỉ được xóa sau khi toàn bộ caller đã được thay thế.

### Điều kiện đạt

- `POST /employees` và `POST /doctors` của Gateway chỉ forward request đến Scheduling.
- Gateway không gọi MediatR để tạo profile.
- Gateway không truy cập `PersonRepository`, `IPersonService` hoặc `ApplicationDbContext`.
- Workflow staff có trạng thái lỗi rõ ràng và không tạo dữ liệu mồ côi không thể xử lý.

## 6. Phase 3 — Xóa compatibility layer

### Công việc

Xóa:

- [SchedulingProxyMiddleware.cs](../backend/Clinic.API/Middlewares/SchedulingProxyMiddleware.cs)
- các route legacy `/specialties`, `/employees`, `/doctors`, `/shifts`;
- logic phân biệt route cũ/mới;
- `SchedulingProxyMiddlewareTests`;
- mô tả proxy cũ trong [GatewayOpenApiConfiguration.cs](../backend/Clinic.API/OpenApi/GatewayOpenApiConfiguration.cs).

Thay bằng route Gateway chính thức, mỗi route chỉ forward đến service sở hữu.

### Điều kiện đạt

- Không còn `UseMiddleware<SchedulingProxyMiddleware>()`.
- Không còn compatibility behavior.
- OpenAPI Gateway chỉ công bố contract hiện hành.

## 7. Phase 4 — Gỡ monolith infrastructure khỏi Gateway

### Công việc

Trong [Program.cs](../backend/Clinic.API/Program.cs), xóa:

- `AddInfrastructureSqlServer`;
- `ApplicationDbContext`;
- `DatabaseInitializer`;
- `OutboxPublisher`;
- `SkipDatabaseInitialization`;
- database connectivity check trong `/readyz`;
- legacy Gateway outbox.

`/readyz` chỉ kiểm tra downstream services bằng HTTP:

- Identity;
- Scheduling;
- Appointment;
- Queue;
- Medical Records;
- Notification.

Trong [Clinic.API.csproj](../backend/Clinic.API/Clinic.API.csproj), xóa:

- reference tới `Clinic.Application`;
- reference tới `Clinic.Infrastructure.Sqlserver`;
- EF Core, SQL Server và EF Tools packages nếu không còn caller.

### Điều kiện đạt

- Gateway không có `DbContext`, migration, repository hoặc unit of work.
- Gateway không kết nối trực tiếp SQL Server.
- Gateway khởi động được khi không có `ClinicDB`.

## 8. Phase 5 — Gỡ `Clinic.Application`, `Clinic.Domain` và MediatR khỏi Gateway

### Công việc

Rà soát và thay thế:

- `Clinic.Application.Contracts`;
- `Clinic.Application.Interfaces`;
- `Clinic.Application.Common.Exceptions`;
- `Clinic.Domain.Enums`;
- `Clinic.Domain.Common`;
- MediatR;
- `ICurrentUser`.

Sử dụng DTO/service-client contracts riêng tại boundary, `ClaimsPrincipal` cho current user và response/error contract của Gateway.

Các vùng cần xử lý:

- [AdminController.cs](../backend/Clinic.API/Controllers/AdminController.cs)
- [MeController.cs](../backend/Clinic.API/Controllers/MeController.cs)
- [AuthController.cs](../backend/Clinic.API/Controllers/AuthController.cs)
- [GlobalExceptionHandler.cs](../backend/Clinic.API/Common/GlobalExceptionHandler.cs)
- [ApiMappingRegister.cs](../backend/Clinic.API/Mappings/ApiMappingRegister.cs)

### Điều kiện đạt

Gateway:

- không reference `Clinic.Application`;
- không reference `Clinic.Domain`;
- không reference `Clinic.Infrastructure.Sqlserver`;
- không dùng MediatR;
- không dùng Entity Framework.

## 9. Phase 6 — Xóa legacy JWT và cấu hình cũ

Sau khi xác nhận toàn bộ client dùng Identity Service token, xóa:

- `LegacyJwtScheme`;
- `Jwt:Key`;
- symmetric-key validation;
- fallback token selection;
- cấu hình và test legacy token.

Giữ lại Identity JWT validation, JWKS và authorization policy cần thiết ở Gateway.

## 10. Phase 7 — Dọn các project monolith không còn reference

Chỉ thực hiện sau khi solution-wide search không còn runtime reference:

- `Clinic.Application`;
- `Clinic.Infrastructure.Sqlserver`;
- monolith handlers;
- Person service/repository;
- legacy migrations;
- legacy outbox/inbox;
- test projects chỉ phục vụ các project đã xóa.

### Điều kiện đạt

`dotnet build` toàn solution thành công và không còn:

- `ApplicationDbContext`;
- `AddInfrastructureSqlServer`;
- MediatR trong Gateway;
- `SchedulingProxyMiddleware`;
- `EnsureIdentityRegistrationProfile`;
- `LegacyJwt`;
- project reference monolith từ Gateway.

## 11. Phase 8 — Cập nhật tài liệu và acceptance

Cập nhật:

- [microservices_analysis.md](../microservices_analysis.md);
- [GatewayOpenApiConfiguration.cs](../backend/Clinic.API/OpenApi/GatewayOpenApiConfiguration.cs);
- Docker Compose;
- README;
- API catalog;
- phase execution/acceptance documents.

### Acceptance bắt buộc

1. Gateway khởi động không cần `ClinicDB`.
2. Readiness chỉ phụ thuộc downstream services.
3. CRUD Employee/Doctor chạy qua Scheduling.
4. Không còn compatibility route.
5. Gateway không truy cập trực tiếp database.
6. Gateway không chạy outbox worker.
7. Login, authorization và JWKS vẫn hoạt động.
8. Appointment, Queue, Medical Records và Notification proxy vẫn hoạt động.
9. OpenAPI Gateway không chứa endpoint đã xóa.
10. Build và test toàn solution đạt.

## 12. Thứ tự thực hiện

1. Chốt contract Staff & Scheduling.
2. Implement staff workflow trong Scheduling.
3. Chuyển Doctors/Employees Gateway thành proxy thuần.
4. Xóa `EnsureIdentityRegistrationProfile`.
5. Xóa `SchedulingProxyMiddleware`.
6. Xóa MediatR và Application contracts khỏi Gateway.
7. Xóa Infrastructure/EF/database/outbox khỏi Gateway.
8. Xóa legacy JWT.
9. Xóa monolith projects không còn reference.
10. Chạy acceptance và cập nhật tài liệu.

## 13. Tiêu chí kết thúc kế hoạch

Kế hoạch chỉ được đóng khi Gateway có thể build và chạy độc lập với monolith projects/database, toàn bộ staff workflow thuộc Staff & Scheduling Service, không còn compatibility route, và acceptance end-to-end chứng minh các proxy còn lại hoạt động đúng.

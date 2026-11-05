# API Inventory & Contract Specification — Member A (Phase 7)

Tài liệu này tổng hợp toàn bộ các HTTP route thực tế trong hệ thống bao gồm **Clinic API Gateway / BFF** (`Clinic.API`) và **6 microservices** (`Clinic.Identity.Service`, `Clinic.StaffScheduling.Service`, `Clinic.Appointment.Service`, `Clinic.Queue.Service`, `Clinic.MedicalRecords.Service`, `Clinic.Notification.Service`).

---

## 1. Nguyên tắc & Quyết định Contract (D1 & D2)

- **Quyết định D1 (API Versioning, Path Visibility & Threat Boundary):**
  - Giữ nguyên wire route hiện hành đang chạy ổn định sau 6 phase (`/appointments`, `/doctors`, `/queue`, `/forgot-password`, v.v.), không thêm tiền tố `/v1` vào URL toàn bộ hệ thống để tránh phá vỡ frontend và các integration test đã nghiệm thu.
  - Phiên bản contract được chuẩn hóa trong OpenAPI metadata: `info.version = "1.0.0"`.
  - Phân định rõ ràng phạm vi công khai (Visibility) và bảo vệ ở **cả hai lớp (Gateway Boundary & Direct Service Port Boundary)**:
    - **Public Gateway Specification (`/openapi/v1.json` trên `Clinic.API` cổng `5000`):** `Public` (`AllowAnonymous`). Mô tả toàn bộ bề mặt API công khai dành cho Frontend web/mobile qua API Gateway, bao gồm cả các route proxy qua `SchedulingProxyMiddleware`.
    - **Service-Internal Specifications (`/openapi/v1.json` trực tiếp trên 6 service cổng `5001`–`5006` và các route proxy `/identity/openapi.json` .. `/notification/openapi.json` trên Gateway):** `Internal`.
      - **Bảo vệ trên Gateway (`/identity/openapi.json`, `/scheduling/openapi.json`, `/appointment/openapi.json`, `/queue/openapi.json`, `/medical-records/openapi.json`, `/notification/openapi.json`):** Gắn `.RequireAuthorization(Roles = "Admin").ExcludeFromDescription()`, trả về `401 Unauthorized` cho anonymous và `403 Forbidden` cho non-Admin (`Patient`, `Doctor`, `Receptionist`). Khi `Admin` gọi qua Gateway, Gateway đính kèm internal service token (`X-Clinic-Service-Token` / `X-Identity-Service-Token`) để lấy spec từ downstream service.
      - **Bảo vệ trực tiếp trên từng Microservice (`app.MapOpenApi().AddEndpointFilter(...)` tại cổng `5001`–`5006`):** Ngay cả khi cổng container `5001`–`5006` được publish ra host trong `docker-compose.yml` để phục vụ debug/integration test, truy cập trực tiếp `GET /openapi/v1.json` trên từng service bắt buộc phải có **Internal Service Token hợp lệ** (`X-Clinic-Service-Token` / `X-Identity-Service-Token` so khớp hằng thời gian `CryptographicOperations.FixedTimeEquals`) hoặc **JWT Bearer có role `Admin`**. Mọi truy cập anonymous hoặc sai token đều bị chặn với `401 Unauthorized`; truy cập bằng JWT của role khác (`Patient`/`Doctor`/`Receptionist`) bị chặn với `403 Forbidden`.
- **Quyết định D2 (Error & Success Payload Schema vs Wire Behavior):**
  - **Controller bọc qua `ApiResponseWrapperFilter` (`ApiCatalogController`, `AuthController`, `AdminController` các route trực tiếp, `EmployeesController`/`DoctorsController` các nhánh lỗi/204):** Mọi `ObjectResult`/`StatusCodeResult` đều được bọc vào envelope `ApiResponse<T>` / `ApiResponse` (`{ statusCode, isSuccess, errorMessages, result }`). Ngay cả lỗi validation tự động `400 BadRequest` (`ValidationProblemDetails`) hay lỗi từ `GlobalExceptionHandler` cũng được chuẩn hóa thành `ApiResponse` (`application/json`) — không trả về `ProblemDetails` thô.
  - **Controller / Middleware Proxy (`ContentResult` / Stream copy):** Các action dùng `AppointmentServiceClient.ProxyAsync`, `QueueServiceClient.ProxyAsync`, `MedicalRecordsServiceClient.ProxyAsync`, `NotificationServiceClient.ProxyAsync`, `SchedulingManagementClient.ToActionResult`, `SlotsController`, `ReservationsController` và `SchedulingProxyMiddleware` trả thẳng JSON wire payload từ downstream service mà không bọc thêm `ApiResponse` ở Gateway.
  - **Topology Metadata trong `/api/catalog`:** Trường `authority` đi kèm `authorityScope = "InternalClusterOnly"`, lấy trực tiếp từ `IConfiguration` của môi trường đang chạy (không dùng fallback localhost cứng trong code controller), phản ánh địa chỉ downstream nội bộ từ góc nhìn Gateway.
- **Headers quy chuẩn:**
  - `Authorization`: `Bearer <JWT>` (khai báo trong `components.securitySchemes.Bearer` và gắn `security` requirement cho các operation có xác thực).
  - `X-Correlation-ID`: Sinh mới tại Gateway nếu thiếu (`RequestLoggingMiddleware`), trả về trên response header và truyền xuyên suốt qua `CorrelationIdDelegatingHandler` / proxy clients.
  - `Idempotency-Key`: Khóa duy nhất (UUID) cho các command nhạy cảm có khả năng retry (`POST /appointments`, `POST /appointments/{appointmentId}/check-in`, `PATCH /appointments/{appointmentId}`, `POST /receptionist/appointments/walk-in`, `POST /receptionist/appointments`, `POST /schedules/{scheduleId}/reservations`, `POST /medical-records/reports/{reportId}/finalize`).

---

## 2. Bảng đối chiếu Route Public Gateway và Microservices Downstream (Two-Way Verified)

<!-- GATEWAY_PUBLIC_ROUTES_START -->
| Public Gateway Path | Method | Service Owner | Downstream Route | Wire Response Style | Auth / Role | Headers |
|---|---|---|---|---|---|---|
| `/.well-known/jwks.json` | GET | Identity | `/.well-known/jwks.json` | Raw JSON (JWKS) | Anonymous | `X-Correlation-ID` |
| `/.well-known/openid-configuration` | GET | Gateway | In-memory discovery | Raw JSON (OIDC) | Anonymous | `X-Correlation-ID` |
| `/healthz` | GET | Gateway | In-memory health | Raw JSON | Anonymous | `X-Correlation-ID` |
| `/readyz` | GET | Identity | `/readyz` | Raw JSON | Anonymous | `X-Correlation-ID` |
| `/api/catalog` | GET | Gateway | In-memory controller | `ApiResponse<ApiCatalogResponse>` | Anonymous | `X-Correlation-ID` |
| `/auth/login` | POST | Identity | `/v1/auth/login` | `ApiResponse<AuthLoginResponse>` | Anonymous | `X-Correlation-ID` |
| `/auth/register` | POST | Identity | `/v1/auth/register` | `ApiResponse<ApiCreatedIdResult>` | Anonymous | `X-Correlation-ID` |
| `/auth/logout` | POST | Identity | `/v1/auth/revoke` | `ApiResponse` (204) | Anonymous | `X-Correlation-ID` |
| `/auth/revoke` | POST | Identity | `/v1/auth/revoke` | `ApiResponse` (204) | Anonymous | `X-Correlation-ID` |
| `/auth/refresh` | POST | Identity | `/v1/auth/refresh` | `ApiResponse<AuthRefreshResponse>` | Anonymous (Cookie) | `X-Correlation-ID` |
| `/auth/me` | GET | Identity | `/v1/auth/me` | `ApiResponse<IdentityUserProfileResponse>` | Bearer (Authenticated) | `X-Correlation-ID` |
| `/auth/permissions` | GET | Gateway | JWT Claims | `ApiResponse<IReadOnlyList<string>>` | Bearer (Authenticated) | `X-Correlation-ID` |
| `/forgot-password` | POST | Identity | `/v1/auth/forgot-password` | `ApiResponse<ApiMessageResult>` | Anonymous | `X-Correlation-ID` |
| `/reset-password` | POST | Identity | `/v1/auth/reset-password` | `ApiResponse<ApiMessageResult>` | Anonymous | `X-Correlation-ID` |
| `/change-password` | POST | Identity | `/v1/auth/change-password` | `ApiResponse<ApiMessageResult>` | Bearer (Authenticated) | `X-Correlation-ID` |
| `/admin/ping` | GET | Gateway | In-memory | `ApiResponse<ApiMessageResult>` | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/roles` | GET | Identity | `/v1/internal/roles` | `ApiResponse<object>` | Bearer (`Permission:role.manage`) | `X-Correlation-ID` |
| `/admin/users` | GET | Identity | `/v1/internal/users` | `ApiResponse<PaginationResponse<UserSummaryDto>>` | Bearer (`Permission:user.manage`) | `X-Correlation-ID` |
| `/admin/users/all-brief` | GET | Identity | `/v1/internal/users` | `ApiResponse<List<UserBriefDto>>` | Bearer (`Permission:user.manage`) | `X-Correlation-ID` |
| `/admin/users/{userId}` | GET | Identity | `/v1/internal/users/{userId}` | `ApiResponse<UserDetailDto>` | Bearer (`Permission:user.manage`) | `X-Correlation-ID` |
| `/admin/doctors/brief` | GET | Scheduling | `/doctors/brief` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/appointments` | GET | Appointment | `/admin/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/appointments/summary` | GET | Appointment | `/admin/appointments/summary` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/appointments/{appointmentId}/history` | GET | Appointment | `/appointments/{appointmentId}/history` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/dashboard` | GET | Gateway | Aggregated (`/admin/dashboard-metrics` + `/internal/patients/count`) | `ApiResponse<DashboardDataDto>` | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/statistics` | GET | Appointment | `/admin/statistics` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/notifications/dead-lettered` | GET | Notification | `/internal/notifications/deliveries/dead-lettered` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/admin/notifications/deliveries/{deliveryId}/replay` | POST | Notification | `/internal/notifications/deliveries/{id}/replay` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/specialties` | GET | Scheduling | `/specialties` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/specialties` | POST | Scheduling | `/specialties` | Downstream JSON (Middleware Proxy) | Bearer (`Permission:specialty.manage`) | `X-Correlation-ID` |
| `/specialties/{id}` | GET | Scheduling | `/specialties/{id}` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/specialties/{id}` | PUT | Scheduling | `/specialties/{id}` | Downstream JSON (Middleware Proxy) | Bearer (`Permission:specialty.manage`) | `X-Correlation-ID` |
| `/specialties/{id}/status` | PATCH | Scheduling | `/specialties/{id}/status` | Downstream JSON (Middleware Proxy) | Bearer (`Permission:specialty.manage`) | `X-Correlation-ID` |
| `/employees` | GET | Scheduling | `/employees` | Enriched JSON (Middleware Proxy) | Bearer (`StaffOnly`) | `X-Correlation-ID` |
| `/employees/me` | GET | Scheduling | `/employees/me` | Enriched JSON (Middleware Proxy) | Bearer (`StaffOnly`) | `X-Correlation-ID` |
| `/employees/{id}` | GET | Scheduling | `/employees/{id}` | Enriched JSON (Middleware Proxy) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/employees` | POST | Scheduling | `/employees` | Downstream JSON (`ContentResult` / `ApiResponse` error) | Bearer (`Permission:employee.create`) | `X-Correlation-ID` |
| `/employees/from-user` | POST | Scheduling | `/employees/from-user` | Downstream JSON (`ContentResult` / `ApiResponse` error) | Bearer (`Permission:employee.create`) | `X-Correlation-ID` |
| `/employees/{employeeId}` | PUT | Identity | `/v1/internal/users/{userId}/profile` | `ApiResponse` (204) | Bearer (`Admin` + `Permission:employee.edit.any`) | `X-Correlation-ID` |
| `/employees/me` | PUT | Identity | `/v1/internal/users/{userId}/profile` | `ApiResponse` (204) | Bearer (`Permission:employee.edit.own`) | `X-Correlation-ID` |
| `/employees/{id}/status` | POST | Scheduling | `/employees/{id}/status` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors` | GET | Scheduling | `/doctors` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors/{id}` | GET | Scheduling | `/doctors/{id}` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors/available` | GET | Scheduling | `/doctors/available` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors/{id}/available` | GET | Scheduling | `/doctors/{id}/available` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors/me` | GET | Scheduling | `/doctors/me` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/doctors/shifts` | GET | Scheduling | `/doctors/shifts` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{doctorId}/shifts` | GET | Scheduling | `/doctors/{doctorId}/shifts` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors/{doctorId}/shifts` | POST | Scheduling | `/doctors/{doctorId}/shifts` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{doctorId}/suggestions` | GET | Scheduling | `/doctors/{doctorId}/suggestions` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/dashboard-stats` | GET | Scheduling | `/doctors/dashboard-stats` | Downstream JSON (Middleware Proxy) | Anonymous | `X-Correlation-ID` |
| `/doctors` | POST | Scheduling | `/doctors` | Downstream JSON (`ContentResult` / `ApiResponse` error) | Bearer (`Permission:doctor.create`) | `X-Correlation-ID` |
| `/doctors/from-user` | POST | Scheduling | `/doctors/from-user` | Downstream JSON (`ContentResult` / `ApiResponse` error) | Bearer (`Permission:doctor.create`) | `X-Correlation-ID` |
| `/doctors/{doctorId}` | PUT | Scheduling | `/doctors/{id}` | Downstream/Wrapped 204 | Bearer (`Permission:doctor.edit.any`) | `X-Correlation-ID` |
| `/doctors/me` | PUT | Scheduling | `/doctors/me` | Downstream/Wrapped 204 | Bearer (`Permission:doctor.edit.own`) | `X-Correlation-ID` |
| `/doctors/{id}/status` | PATCH | Scheduling | `/doctors/{id}/status` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{id}/status` | POST | Scheduling | `/doctors/{id}/status` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{id}/specialty` | PATCH | Scheduling | `/doctors/{id}/specialty` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{id}/specialty` | POST | Scheduling | `/doctors/{id}/specialty` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/doctors/{doctorId}/queue` | GET | Queue | `/doctors/{doctorId}/queue` | Downstream JSON (`ContentResult`) | Bearer (`Permission:doctor.queue.view,queue.view`) | `X-Correlation-ID` |
| `/doctors/{doctorId}/queue/next` | POST | Queue | `/doctors/{doctorId}/queue/next` | Downstream JSON (`ContentResult`) | Bearer (`Permission:queue.call-next`) | `X-Correlation-ID` |
| `/shifts` | GET | Scheduling | `/shifts` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions` | GET | Scheduling | `/shifts/suggestions` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions` | POST | Scheduling | `/shifts/suggestions` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions/{id}` | PATCH | Scheduling | `/shifts/suggestions/{id}` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions/{id}/cancel` | POST | Scheduling | `/shifts/suggestions/{id}/cancel` | Downstream JSON (Middleware Proxy) | Bearer (`DoctorOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions/{id}/approve` | POST | Scheduling | `/shifts/suggestions/{id}/approve` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/shifts/suggestions/{id}/reject` | POST | Scheduling | `/shifts/suggestions/{id}/reject` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/shifts/{id}` | PUT | Scheduling | `/shifts/{id}` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/shifts/{id}/cancel` | POST | Scheduling | `/shifts/{id}/cancel` | Downstream JSON (Middleware Proxy) | Bearer (`AdminOnly`) | `X-Correlation-ID` |
| `/schedules` | GET | Scheduling | `/schedules` | Downstream JSON (`Results.Content`) | Anonymous | `X-Correlation-ID` |
| `/schedules/{id}` | GET | Scheduling | `/schedules/{id}` | Downstream JSON (`Results.Content`) | Anonymous | `X-Correlation-ID` |
| `/schedules/availability` | GET | Scheduling | `/schedules/availability` | Downstream JSON (`ContentResult`) | Anonymous | `X-Correlation-ID` |
| `/slots` | GET | Scheduling | `/schedules/availability` | Downstream JSON (`ContentResult`) | Anonymous | `X-Correlation-ID` |
| `/schedules/{scheduleId}/reservations` | POST | Scheduling | `/schedules/{scheduleId}/reservations` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `Idempotency-Key`, `X-Correlation-ID` |
| `/schedules/{scheduleId}/reservations/{reservationId}/confirm` | POST | Scheduling | `/schedules/{scheduleId}/reservations/{reservationId}/confirm` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/schedules/{scheduleId}/reservations/{reservationId}/release` | POST | Scheduling | `/schedules/{scheduleId}/reservations/{reservationId}/release` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/appointments` | POST | Appointment | `/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.create`) | `Idempotency-Key`, `X-Correlation-ID` |
| `/appointments/{appointmentId}` | GET | Appointment | `/appointments/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.view`) | `X-Correlation-ID` |
| `/appointments/{appointmentId}` | PATCH | Appointment | `/appointments/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.edit.own,appointment.edit.any`) | `Idempotency-Key`, `X-Correlation-ID` |
| `/appointments/{appointmentId}/confirm` | POST | Appointment | `/appointments/{appointmentId}/confirm` | Downstream 204 | Bearer (`Permission:appointment.confirm`) | `X-Correlation-ID` |
| `/appointments/{appointmentId}/check-in` | POST | Appointment | `/appointments/{appointmentId}/check-in` | Downstream 204 / `ApiResponse` (400 if missing key) | Bearer (`Permission:queue.check-in`) | `Idempotency-Key`, `X-Correlation-ID` |
| `/appointments/{appointmentId}/cancel` | POST | Appointment | `/appointments/{appointmentId}/cancel` | Downstream 204 | Bearer (`Permission:appointment.edit.own,appointment.edit.any`) | `X-Correlation-ID` |
| `/me/appointments` | GET | Appointment | `/me/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.view`) | `X-Correlation-ID` |
| `/receptionist/working-doctors` | GET | Appointment | `/receptionist/working-doctors` | Downstream JSON (`ContentResult`) | Bearer (`Receptionist,Admin`) | `X-Correlation-ID` |
| `/receptionist/queue-doctors` | GET | Appointment | `/receptionist/queue-doctors` | Downstream JSON (`ContentResult`) | Bearer (`Receptionist,Admin`) | `X-Correlation-ID` |
| `/receptionist/appointments/walk-in` | POST | Appointment | `/receptionist/appointments/walk-in` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.create`) | `Idempotency-Key`, `X-Correlation-ID` |
| `/receptionist/appointments` | POST | Appointment | `/receptionist/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.create`) | `Idempotency-Key`, `X-Correlation-ID` |
| `/receptionist/appointments/pending` | GET | Appointment | `/receptionist/appointments/pending` | Downstream JSON (`ContentResult`) | Bearer (`Receptionist`) | `X-Correlation-ID` |
| `/receptionist/appointments/{appointmentId}` | PATCH | Appointment | `/receptionist/appointments/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:appointment.update`) | `X-Correlation-ID` |
| `/queue/call-next/{doctorId}` | POST | Queue | `/queue/call-next/{doctorId}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:queue.call-next`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/call` | POST | Queue | `/queue/{queueTicketId}/call` | Downstream JSON (`ContentResult`) | Bearer (`Permission:queue.call-next`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/start-exam` | POST | Queue | `/queue/{queueTicketId}/start-exam` | Downstream 204 | Bearer (`Permission:queue.start-exam.own,queue.start-exam.any`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/complete-exam` | POST | Queue | `/queue/{queueTicketId}/complete-exam` | Downstream 204 | Bearer (`Permission:queue.complete-exam.own,queue.complete-exam.any`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/skip` | POST | Queue | `/queue/{queueTicketId}/skip` | Downstream JSON (`ContentResult`) | Bearer (`Permission:queue.skip,queue.skip-waiting`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/retry` | POST | Queue | `/queue/{queueTicketId}/retry` | Downstream JSON (`ContentResult`) | Bearer (`Permission:queue.retry`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}/priority` | PATCH | Queue | `/queue/{queueTicketId}/priority` | Downstream 204 | Bearer (`Permission:queue.priority`) | `X-Correlation-ID` |
| `/queue/{queueTicketId}` | GET | Queue | `/queue/{queueTicketId}` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/queue/appointment/{appointmentId}` | GET | Queue | `/queue/appointment/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/me/queue-status` | GET | Queue | `/me/queue-status` | Downstream JSON (`ContentResult` / 204) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/patients` | GET | MedicalRecords | `/patients` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.view.any`) | `X-Correlation-ID` |
| `/patients/{patientId}` | GET | MedicalRecords | `/patients/{id}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.view.any,patient.view.own`) | `X-Correlation-ID` |
| `/patients/{patientId}/appointments` | GET | Appointment | `/patients/{patientId}/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.view.any`) | `X-Correlation-ID` |
| `/patients/{patientId}/appointments/upcoming` | GET | Appointment | `/patients/{patientId}/appointments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.view.any`) | `X-Correlation-ID` |
| `/patients/me` | GET | MedicalRecords | `/patients/me` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.view.own`) | `X-Correlation-ID` |
| `/patients/me` | PUT | MedicalRecords | `/patients/me` | Downstream 204 | Bearer (`Permission:patient.edit.own`) | `X-Correlation-ID` |
| `/patients` | POST | MedicalRecords | `/patients` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.create.any`) | `X-Correlation-ID` |
| `/patients/add` | POST | MedicalRecords | `/patients/add` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient.create.any`) | `X-Correlation-ID` |
| `/patients/{patientId}` | PUT | MedicalRecords | `/patients/{id}` | Downstream 204 | Bearer (`Permission:patient.edit.any`) | `X-Correlation-ID` |
| `/patients/{patientId}/medical-history` | GET | MedicalRecords | `/patients/{patientId}/medical-history` | Downstream JSON (`ContentResult`) | Bearer (`Permission:patient-history.view.related,patient-history.view.any`) | `X-Correlation-ID` |
| `/me/medical-history` | GET | MedicalRecords | `/me/medical-history` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/medical-reports` | GET | MedicalRecords | `/medical-records` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own`) | `X-Correlation-ID` |
| `/medical-reports` | POST | MedicalRecords | `/medical-records` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.create`) | `X-Correlation-ID` |
| `/medical-reports/{id}` | PUT | MedicalRecords | `/medical-records/{id}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.create`) | `X-Correlation-ID` |
| `/medical-reports/{id}` | GET | MedicalRecords | `/medical-records/{id}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own`) | `X-Correlation-ID` |
| `/medical-reports/appointment/{appointmentId}` | GET | MedicalRecords | `/medical-records/appointment/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.view.related,medical-report.view.any,medical-report.view.own`) | `X-Correlation-ID` |
| `/medical-reports/{id}/finalize` | POST | MedicalRecords | `/medical-records/{id}/finalize` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.create`) | `X-Correlation-ID` |
| `/medical-reports/appointments/{appointmentId}/amendments` | POST | MedicalRecords | `/medical-records/appointments/{appointmentId}/amendments` | Downstream JSON (`ContentResult`) | Bearer (`Permission:medical-report.create`) | `X-Correlation-ID` |
| `/medical-reports/appointments/{appointmentId}/audits` | GET | MedicalRecords | `/medical-records/appointments/{appointmentId}/audits` | Downstream JSON (`ContentResult`) | Bearer (`Admin`) | `X-Correlation-ID` |
| `/medical-records/encounters/{appointmentId}/draft` | POST | MedicalRecords | `/medical-records/encounters/{appointmentId}/draft` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/medical-records/encounters/{appointmentId}` | GET | MedicalRecords | `/medical-records/encounters/{appointmentId}` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/medical-records/reports/{reportId}/finalize` | POST | MedicalRecords | `/medical-records/{id}/finalize` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `Idempotency-Key`, `X-Correlation-ID` |
| `/medical-records/reports/{reportId}/amendments` | POST | MedicalRecords | `/medical-records/{appointmentId}/amendments` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/notifications` | GET | Notification | `/notifications` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/notifications/unread-count` | GET | Notification | `/notifications/unread-count` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/notifications/{id}/read` | POST | Notification | `/notifications/{id}/read` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/notifications/{id}/read` | PUT | Notification | `/notifications/{id}/read` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/notifications/read-all` | POST | Notification | `/notifications/read-all` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
| `/me/notifications` | GET | Notification | `/notifications/me` | Downstream JSON (`ContentResult`) | Bearer (Authenticated) | `X-Correlation-ID` |
<!-- GATEWAY_PUBLIC_ROUTES_END -->

> **Lưu ý về Realtime Hub & Operator OpenAPI Proxies trên Gateway:**
> - `/hubs/notifications` và `/hubs/notifications/{**subPath}` được đăng ký bằng `app.Map(...)` để proxy WebSocket / SSE / LongPolling SignalR sang `Clinic.Notification.Service` (yêu cầu Bearer token qua header hoặc query `access_token`).
> - 6 route `/identity/openapi.json`, `/scheduling/openapi.json`, `/appointment/openapi.json`, `/queue/openapi.json`, `/medical-records/openapi.json`, `/notification/openapi.json` được gắn `.RequireAuthorization(Roles = "Admin").ExcludeFromDescription()` để không lộ spec nội bộ cho người dùng ẩn danh hoặc bệnh nhân.

---

## 3. Danh mục Toàn bộ Endpoint trên 6 Microservices (Two-Way Reconciled Service Contracts)

Bảng dưới đây ghi nhận **đầy đủ 100% các HTTP route/method** xuất hiện trong OpenAPI specification của từng microservice (`Identity`, `Scheduling`, `Appointment`, `Queue`, `MedicalRecords`, `Notification`), bao gồm cả các route nghiệp vụ được Gateway proxy tới, các route nội bộ S2S (`/v1/internal/...`, `/internal/...`), và các route vận hành (`/`, `/me`, `/healthz`, `/readyz`). Bộ kiểm thử `ApiInventoryMarkdown_ReconcilesWith_RealGatewayAndServiceOpenApiSpecs` kiểm tra đối xứng hai chiều giữa bảng này và 6 tài liệu OpenAPI thật của từng service.

<!-- SERVICE_INTERNAL_ROUTES_START -->
| Service | Endpoint Path | Method | Visibility | Auth / Token |
|---|---|---|---|---|
| `Identity` | `/healthz` | GET | Operational | Anonymous |
| `Identity` | `/readyz` | GET | Operational | Anonymous |
| `Identity` | `/.well-known/jwks.json` | GET | Downstream / Public Proxy | Anonymous |
| `Identity` | `/.well-known/openid-configuration` | GET | Downstream / Public Proxy | Anonymous |
| `Identity` | `/v1/auth/register` | POST | Downstream S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/auth/forgot-password` | POST | Downstream S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/auth/reset-password` | POST | Downstream | Anonymous (via Gateway) |
| `Identity` | `/v1/auth/login` | POST | Downstream | Anonymous (via Gateway) |
| `Identity` | `/v1/auth/refresh` | POST | Downstream | Anonymous (via Gateway) |
| `Identity` | `/v1/auth/change-password` | POST | Downstream | Bearer (Authenticated) |
| `Identity` | `/v1/auth/revoke` | POST | Downstream | Anonymous (via Gateway) |
| `Identity` | `/v1/auth/permissions` | GET | Downstream | Bearer (Authenticated) |
| `Identity` | `/v1/auth/me` | GET | Downstream | Bearer (Authenticated) |
| `Identity` | `/v1/auth/users/{userId}` | GET | Internal | Bearer (Self / `Admin`) |
| `Identity` | `/v1/internal/users/provision` | POST | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/internal/users/{userId}/roles` | PUT | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/internal/users/{userId}/profile` | PUT | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/internal/users` | GET | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/internal/users/{userId}` | GET | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/v1/internal/roles` | GET | Internal S2S | `X-Identity-Service-Token` |
| `Identity` | `/internal/keys/rotate` | POST | Internal Admin | `X-Key-Rotation-Secret` |
| `Scheduling` | `/healthz` | GET | Operational | Anonymous |
| `Scheduling` | `/readyz` | GET | Operational | Anonymous |
| `Scheduling` | `/` | GET | Operational | Anonymous |
| `Scheduling` | `/me` | GET | Operational | Bearer (Authenticated) |
| `Scheduling` | `/employees` | GET | Downstream | Bearer (`StaffOnly`) |
| `Scheduling` | `/employees` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/employees/from-user` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/employees/me` | GET | Downstream | Bearer (`StaffOnly`) |
| `Scheduling` | `/employees/me` | PUT | Downstream | Bearer (Authenticated) |
| `Scheduling` | `/employees/{id}` | GET | Downstream | Bearer (Authenticated) |
| `Scheduling` | `/employees/{id}` | PUT | Downstream | Bearer (Authenticated) |
| `Scheduling` | `/employees/{id}/status` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/specialties` | GET | Downstream | Anonymous |
| `Scheduling` | `/specialties` | POST | Downstream | Bearer (`SpecialtyManage`) |
| `Scheduling` | `/specialties/{id}` | GET | Downstream | Anonymous |
| `Scheduling` | `/specialties/{id}` | PUT | Downstream | Bearer (`SpecialtyManage`) |
| `Scheduling` | `/specialties/{id}/status` | PATCH | Downstream | Bearer (`SpecialtyManage`) |
| `Scheduling` | `/doctors` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/from-user` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors` | GET | Downstream | Anonymous |
| `Scheduling` | `/doctors/{id}` | PUT | Downstream | Bearer (Authenticated) |
| `Scheduling` | `/doctors/{id}` | GET | Downstream | Anonymous |
| `Scheduling` | `/doctors/me` | PUT | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/doctors/me` | GET | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/doctors/{id}/status` | PATCH | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{id}/status` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{id}/specialty` | PATCH | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{id}/specialty` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{id}/available` | GET | Downstream | Anonymous |
| `Scheduling` | `/doctors/available` | GET | Downstream | Anonymous |
| `Scheduling` | `/doctors/{id}/staff-link` | GET | Internal S2S | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/brief` | GET | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/shifts` | GET | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{doctorId}/suggestions` | GET | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{doctorId}/shifts` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/doctors/{doctorId}/shifts` | GET | Downstream | Anonymous |
| `Scheduling` | `/shifts` | GET | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/shifts/suggestions` | POST | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/shifts/suggestions` | GET | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/shifts/{id}` | PUT | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/shifts/{id}/cancel` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/shifts/suggestions/{id}` | PATCH | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/doctors/dashboard-stats` | GET | Downstream | Anonymous |
| `Scheduling` | `/schedules/availability` | GET | Downstream | Anonymous |
| `Scheduling` | `/slots` | GET | Downstream | Anonymous |
| `Scheduling` | `/schedules/{scheduleId}/reservations` | POST | Downstream | Bearer / S2S |
| `Scheduling` | `/schedules/{scheduleId}/reservations/{reservationId}/confirm` | POST | Downstream | Bearer / S2S |
| `Scheduling` | `/schedules/{scheduleId}/reservations/{reservationId}/release` | POST | Downstream | Bearer / S2S |
| `Scheduling` | `/shifts/suggestions/{id}/approve` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/shifts/suggestions/{id}/reject` | POST | Downstream | Bearer (`AdminOnly`) |
| `Scheduling` | `/shifts/suggestions/{id}/cancel` | POST | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/schedules` | GET | Downstream | Bearer (`DoctorOnly`) |
| `Scheduling` | `/schedules/walk-in-candidates` | GET | Internal S2S | Internal S2S |
| `Scheduling` | `/schedules/{id}` | GET | Downstream | Anonymous / S2S |
| `Scheduling` | `/schedules/{id}` | PUT | Internal | Internal / Admin |
| `Scheduling` | `/schedules/{id}/cancel` | POST | Internal | Internal / Admin |
| `Scheduling` | `/internal/events/user-profile-updated` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `Scheduling` | `/internal/events/appointment-cancelled` | POST | Internal S2S | Internal S2S |
| `Appointment` | `/healthz` | GET | Operational | Anonymous |
| `Appointment` | `/readyz` | GET | Operational | Anonymous |
| `Appointment` | `/` | GET | Operational | Anonymous |
| `Appointment` | `/me` | GET | Operational | Bearer (Authenticated) |
| `Appointment` | `/appointments` | POST | Downstream | Bearer (Authenticated) |
| `Appointment` | `/appointments` | GET | Internal | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/appointments` | POST | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/appointments/walkin` | POST | Internal Alias | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/appointments/walk-in` | POST | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/working-doctors` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/queue-doctors` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}` | GET | Downstream | Bearer (Authenticated) |
| `Appointment` | `/appointments/{appointmentId}` | PATCH | Downstream | Bearer (Authenticated) |
| `Appointment` | `/me/appointments` | GET | Downstream | Bearer (Authenticated) |
| `Appointment` | `/patients/{patientId}/appointments` | GET | Downstream | Bearer (`PatientAppointmentsViewAny`) |
| `Appointment` | `/me/appointments/{appointmentId}` | GET | Internal | Bearer (Authenticated) |
| `Appointment` | `/receptionist/appointments/pending` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/confirm` | POST | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/appointments/{appointmentId}/confirm` | POST | Internal Alias | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/check-in` | POST | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/checkin` | POST | Internal Alias | Bearer (`StaffOnly`) |
| `Appointment` | `/receptionist/appointments/{appointmentId}/checkin` | POST | Internal Alias | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/cancel` | POST | Downstream | Bearer (Authenticated) |
| `Appointment` | `/appointments/{appointmentId}/reschedule` | POST | Internal Alias | Bearer (Authenticated) |
| `Appointment` | `/receptionist/appointments/{appointmentId}` | PATCH | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/admin/appointments` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/summary` | GET | Internal Alias | Bearer (`StaffOnly`) |
| `Appointment` | `/admin/appointments/summary` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/admin/statistics` | GET | Downstream | Bearer (`AdminOnly`) |
| `Appointment` | `/admin/dashboard-metrics` | GET | Internal S2S | Bearer (`AdminOnly`) |
| `Appointment` | `/appointments/{appointmentId}/history` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/audit-history` | GET | Internal | Bearer (`StaffOnly`) |
| `Appointment` | `/appointments/{appointmentId}/snapshots` | GET | Internal | Bearer (`StaffOnly`) |
| `Appointment` | `/admin/appointments/{appointmentId}/history` | GET | Downstream | Bearer (`StaffOnly`) |
| `Appointment` | `/internal/events/exam-completed` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `Appointment` | `/internal/events/user-profile-updated` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `Queue` | `/healthz` | GET | Operational | Anonymous |
| `Queue` | `/readyz` | GET | Operational | Anonymous |
| `Queue` | `/` | GET | Operational | Anonymous |
| `Queue` | `/me` | GET | Operational | Bearer (Authenticated) |
| `Queue` | `/admin/dashboard-metrics` | GET | Internal S2S | Bearer (`AdminOnly`) |
| `Queue` | `/doctors/{doctorId}/queue` | GET | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/doctors/{doctorId}/queue/next` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/call-next/{doctorId}` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/call` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/start-exam` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/complete-exam` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/skip` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/retry` | POST | Downstream | Bearer (`StaffOnly`) |
| `Queue` | `/queue/{queueTicketId}/priority` | PATCH | Downstream | Bearer (`Admin` / `Receptionist`) |
| `Queue` | `/queue/{queueTicketId}/priority` | POST | Internal Alias | Bearer (`Admin` / `Receptionist`) |
| `Queue` | `/me/queue-status` | GET | Downstream | Bearer (`PatientOnly`) |
| `Queue` | `/queue/{queueTicketId}` | GET | Downstream | Bearer (Authenticated) |
| `Queue` | `/queue/appointment/{appointmentId}` | GET | Downstream | Bearer (Authenticated) |
| `MedicalRecords` | `/healthz` | GET | Operational | Anonymous |
| `MedicalRecords` | `/readyz` | GET | Operational | Anonymous |
| `MedicalRecords` | `/` | GET | Operational | Anonymous |
| `MedicalRecords` | `/me` | GET | Operational | Bearer (Authenticated) |
| `MedicalRecords` | `/patients` | POST | Downstream | Bearer (`PatientCreateAny`) |
| `MedicalRecords` | `/patients` | GET | Downstream | Bearer (`PatientViewAny`) |
| `MedicalRecords` | `/patients/add` | POST | Downstream | Bearer (`PatientCreateAny`) |
| `MedicalRecords` | `/patients/walk-in` | POST | Internal | Bearer (`PatientCreateAny`) |
| `MedicalRecords` | `/patients/{id}` | GET | Downstream | Bearer (Authenticated) |
| `MedicalRecords` | `/patients/{id}` | PUT | Downstream | Bearer (`PatientEditAny`) |
| `MedicalRecords` | `/patients/me` | GET | Downstream | Bearer (`PatientViewOwn`) |
| `MedicalRecords` | `/patients/me` | PUT | Downstream | Bearer (`PatientEditOwn`) |
| `MedicalRecords` | `/medical-records` | POST | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records/encounters/{appointmentId}/draft` | POST | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records` | GET | Downstream | Bearer (`ReportView`) |
| `MedicalRecords` | `/medical-records/{id}` | PUT | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records/{id}` | GET | Downstream | Bearer (`ReportView`) |
| `MedicalRecords` | `/medical-records/{id}/finalize` | POST | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records/appointments/{appointmentId}/amendments` | POST | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records/{appointmentId}/amendments` | POST | Downstream | Bearer (`ReportCreate`) |
| `MedicalRecords` | `/medical-records/appointment/{appointmentId}` | GET | Downstream | Bearer (`ReportView`) |
| `MedicalRecords` | `/medical-records/encounters/{appointmentId}` | GET | Downstream | Bearer (`ReportView`) |
| `MedicalRecords` | `/patients/{patientId}/medical-history` | GET | Downstream | Bearer (`PatientHistoryView`) |
| `MedicalRecords` | `/me/medical-history` | GET | Downstream | Bearer (`PatientViewOwn`) |
| `MedicalRecords` | `/medical-records/appointments/{appointmentId}/audits` | GET | Downstream | Bearer (`AdminOnly`) |
| `MedicalRecords` | `/internal/events/user-profile-updated` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `MedicalRecords` | `/internal/patients/provision-identity` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `MedicalRecords` | `/internal/patients/resolve-registration` | GET | Internal S2S | `X-Clinic-Service-Token` |
| `MedicalRecords` | `/internal/patients/count` | GET | Internal S2S | `X-Clinic-Service-Token` |
| `Notification` | `/healthz` | GET | Operational | Anonymous |
| `Notification` | `/readyz` | GET | Operational | Anonymous |
| `Notification` | `/` | GET | Operational | Anonymous |
| `Notification` | `/me/notifications` | GET | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications` | GET | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications/me` | GET | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications/unread-count` | GET | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications/{id}/read` | POST | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications/{id}/read` | PUT | Downstream | Bearer (Authenticated) |
| `Notification` | `/notifications/read-all` | POST | Downstream | Bearer (Authenticated) |
| `Notification` | `/internal/notifications/password-reset` | POST | Internal S2S | `X-Clinic-Service-Token` |
| `Notification` | `/internal/notifications/deliveries/dead-lettered` | GET | Internal S2S | `X-Clinic-Service-Token` + `X-Clinic-Operator-Id` |
| `Notification` | `/internal/notifications/deliveries/{id}/replay` | POST | Internal S2S | `X-Clinic-Service-Token` + `X-Clinic-Operator-Id` |
<!-- SERVICE_INTERNAL_ROUTES_END -->

---

## 4. Đặc tả DTO, Wire Schema & Phạm vi Kiểm thử Wire Behavior

### 4.1. Phân nhóm Wire Behavior trên Clinic API Gateway (`Clinic.API`)
1. **Nhóm 1 — Wrapped Controller Endpoints (`ApiResponseWrapperFilter`):**
   - Bao gồm: `ApiCatalogController` (`GET /api/catalog`), `AuthController` (`/auth/login`, `/auth/register`, `/auth/logout`, `/auth/revoke`, `/auth/refresh`, `/auth/me`, `/auth/permissions`, `/forgot-password`, `/reset-password`, `/change-password`), `AdminController` (`/admin/ping`, `/admin/roles`, `/admin/users`, `/admin/users/all-brief`, `/admin/users/{userId}`, `/admin/dashboard`), `EmployeesController` (`PUT /employees/{employeeId}`, `PUT /employees/me`).
   - Mọi response JSON (`200`, `201`, `400`, `401`, `403`, `404`, `409`, `500`, `503`) đều dùng envelope `ApiResponse`:
     `{ statusCode: string, isSuccess: bool, errorMessages: string[], result: T? }`.
2. **Nhóm 2 — Raw Downstream Proxy Endpoints (`ContentResult` / `SchedulingProxyMiddleware`):**
   - Bao gồm: `SchedulingProxyMiddleware` (`/specialties`, `/doctors`, `/employees`, `/shifts`), `SlotsController` (`/slots`, `/schedules/availability`), `ReservationsController` (`/schedules/{scheduleId}/reservations/...`), `AppointmentsController`, `ReceptionistController`, `QueueController`, `PatientsController`, `MedicalReportsController`, `MedicalRecordsContractController`, `NotificationsController`, `MeController`, và các route proxy của `AdminController` (`/admin/doctors/brief`, `/admin/appointments/...`, `/admin/statistics`, `/admin/notifications/...`).
   - Trả về nguyên trạng JSON wire payload từ downstream service (`200 OK` / `201 Created`) hoặc body rỗng khi `204 No Content`.
   - Lưu ý quan trọng: Khi request bị từ chối **ngay tại controller của Gateway trước khi gọi downstream** (ví dụ: `POST /appointments/{appointmentId}/check-in` thiếu `Idempotency-Key`, hoặc lỗi ModelState validation `[ApiController]`), `ApiResponseWrapperFilter` bắt `BadRequestObjectResult` và trả về envelope `ApiResponse` (`400 BadRequest`).
3. **Nhóm 3 — Các endpoint không gọi trực tiếp bằng fuzzing trong in-memory contract test host (và lý do):**
   - `/hubs/notifications` & `/hubs/notifications/{**subPath}`: Yêu cầu nâng cấp kết nối SignalR WebSocket/LongPolling tới `Clinic.Notification.Service` (đã được kiểm thử trong bộ test Notification/SignalR riêng).
   - Các saga ghi liên kết đa dịch vụ có side-effect lên RabbitMQ/SQL (`POST /auth/register`, `POST /employees`, `POST /doctors`, `POST /admin/notifications/deliveries/{deliveryId}/replay`): Đã được kiểm chứng schema request/response tĩnh trong OpenAPI spec và được kiểm thử đầy đủ trong các bộ integration test nghiệp vụ (`AuthSplitIntegrationTests`, `StaffManagementSplitTests`, `NotificationRequirementsTests`).

### 4.2. Phạm vi Đối sánh Schema của `OpenApiContractDiffer`
- **Các cấu trúc và keyword OpenAPI 3.x được hỗ trợ và kiểm thử đầy đủ:**
  - `paths`, HTTP methods (`get`, `post`, `put`, `patch`, `delete`), `security` requirements, `parameters` (`in`, `name`, `required`).
  - `requestBody` (`required`, media types `content`) và `responses` (status codes, media types `content`).
  - Giải tham chiếu `$ref` (`#/components/schemas/...`), bao gồm cả wrapper nullable một nhánh và đa nhánh (`oneOf`, `anyOf`, `allOf`) — kiểm tra từng nhánh thành viên cũng như các ràng buộc đặt trực tiếp trên schema tổ hợp ngoài cùng.
  - Cấu trúc object/array: `type` (kể cả mảng type OpenAPI 3.1 như `["null", "string"]`), `required`, `properties` (thêm/xóa/đổi kiểu), `items` (đệ quy mảng), `additionalProperties` (cả dạng boolean `false` lẫn dạng schema object).
  - Ràng buộc scalar/validation: `enum` (thêm/bớt giá trị theo ngữ cảnh request/response), `format` (`uuid`, `date`, `date-time`, `time`, `int32`, `int64`, `double`), `pattern`, `minLength`, `maxLength`, `minimum`, `maximum`.
- **Các keyword JSON Schema nâng cao nằm ngoài phạm vi (do `Microsoft.AspNetCore.OpenApi` không sinh ra trong hệ thống hiện tại):**
  - `not`, `if`/`then`/`else`, `dependentSchemas`/`dependentRequired`, `prefixItems`, `unevaluatedProperties`, `discriminator.mapping`, ` multipleOf`, `exclusiveMinimum`/`exclusiveMaximum` dạng số (nếu dùng ngoài `minimum`/`maximum`), và `$ref` liên tài liệu từ URL bên ngoài.

---

## 5. Quản lý Baseline Snapshots (`tasks/contracts/`)

Tất cả các baseline contract đã được review và lưu cố định trong `tasks/contracts/`:
1. `tasks/contracts/api-catalog-baseline.json` — Snapshot của `GET /api/catalog`.
2. `tasks/contracts/gateway-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.API` (Public Gateway).
3. `tasks/contracts/identity-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.Identity.Service` (Internal).
4. `tasks/contracts/scheduling-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.StaffScheduling.Service` (Internal).
5. `tasks/contracts/appointment-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.Appointment.Service` (Internal).
6. `tasks/contracts/queue-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.Queue.Service` (Internal).
7. `tasks/contracts/medical-records-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.MedicalRecords.Service` (Internal).
8. `tasks/contracts/notification-openapi-v1.json` — Snapshot OpenAPI v1 của `Clinic.Notification.Service` (Internal).

- **Chạy kiểm thử contract & drift (chỉ đọc và đối sánh, tuyệt đối không ghi đè baseline):**
  - PowerShell: `./scripts/validate-api-contracts.ps1`
  - Bash: `./scripts/validate-api-contracts.sh`
- **Cập nhật snapshot có chủ đích (chỉ chạy khi có thay đổi contract đã được phê duyệt):**
  - PowerShell: `./scripts/validate-api-contracts.ps1 -UpdateSnapshots`
  - Bash: `./scripts/validate-api-contracts.sh --update-snapshots`

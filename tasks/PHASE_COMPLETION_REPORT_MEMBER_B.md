# Báo cáo bàn giao và đối chiếu nhánh Thành viên B

**Cập nhật:** 02/11/2026
**Trạng thái:** Đã đối chiếu thiết kế và resolve nội dung chồng lấn với implementation hiện hành trên `main`; chưa chạy lại build/test trên cây merge sau khi resolve, do đó tài liệu này không tuyên bố nghiệm thu toàn bộ nhánh.

## 1. Nguyên tắc hợp nhất

- `main` là nguồn chuẩn cho các implementation service/API/database/cutover do Thành viên A sở hữu, đặc biệt Medical Records và Notification.
- Giữ các đóng góp B còn phù hợp ở messaging reliability, broker/consumer tests, observability, frontend/Gateway integration, Compose/Prometheus, Queue regression tests và runbooks.
- Khi một đóng góp B tạo implementation thứ hai cho cùng bounded context, giữ implementation A đã nghiệm thu; chỉ giữ test/telemetry/operational contribution nếu chúng kiểm tra contract hiện hành.

## 2. Các đóng góp B được giữ

- Điều chỉnh Queue integration test cho hành vi bác sĩ có thể gọi lượt tiếp theo khi không còn ticket Waiting; cập nhật readiness test cho môi trường Testing.
- Nginx WebSocket upgrade/timeout phục vụ SignalR qua Gateway; giữ Prometheus scrape targets chỉ cho endpoint metrics hiện có. Bổ sung scrape target cho service khác sau khi endpoint/exporter được triển khai và xác minh trong Phase 8.
- Xóa credential SMTP khỏi `appsettings.Development.json`; các giá trị bí mật phải dùng `.env`/secret ngoài repository.
- Kế hoạch công việc riêng Member B theo phase 0–6, runbook messaging/operations, cùng nhiệm vụ phase 7–9 trong `TASK_MEMBER_B.md`.

## 3. Implementation B cũ bị thay thế hoặc loại bỏ

- Bản scaffold `MedicalRecord`/`MedicalHistoryProjector` và consumer cũ được thay bằng implementation Medical Records hiện hành từ `main`.
- Bản scaffold Notification gồm `DeliveryWorker`, generic realtime client và gateway SignalR Hub được thay bằng Notification Service/REST/SignalR proxy hiện hành từ `main`.
- Xóa generic `NotificationRequested` DTO mang title/body tự do vì không được implementation/policy hiện hành sử dụng; giữ event names do `main` định nghĩa.
- Xóa `ServiceProxyMiddleware` trùng với controller/service clients của Gateway. Middleware cũ còn chuyển nguyên `/medical-reports/**` trong khi downstream API hiện hành dùng `/medical-records/**`, dẫn tới route mismatch.
- Xóa Redis SignalR package khỏi Clinic API; Hub thuộc Notification Service và Redis chỉ cần khi chạy nhiều instance.

## 4. Các quyết định đã chuẩn hóa

- `ExamCompleted` chỉ kết thúc EncounterContext; không finalize MedicalReport. Report Draft được bác sĩ finalize qua Medical Records API.
- Không tạo MedicalHistory projection/rebuild endpoint riêng nếu truy vấn từ Medical Records DB đã đáp ứng.
- Notification DB là nguồn lịch sử; SignalR chỉ là hint. Single-instance Compose không bắt buộc Redis.
- Password reset dùng luồng bảo vệ riêng; không gửi token trong business notification event.
- `PatientCalled` tiếp tục mang recipient/contact snapshot cần thiết để Notification xử lý account/guest độc lập với thứ tự event.
- Medical Records consumer hiện NACK lỗi với `requeue: false` và chưa có bounded transient retry queue; đây là việc reliability B còn phải bổ sung/kiểm thử.

## 5. Việc còn cần làm sau khi resolve

- Chạy build/backend tests và frontend type-check/unit suite trên cây merge đã resolve; xử lý compile/test regressions nếu có.
- Đối chiếu README/runbook với route/config thật và kết quả runtime sau khi stack được build từ cây hợp nhất.
- B tiếp tục phần việc chung Phase 7–9: OpenAPI validation, metrics/SLO instrumentation và load test harness theo [`PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md).

Kết quả kiểm thử trước merge chỉ là bằng chứng lịch sử của từng nhánh; không thay thế việc kiểm tra lại cây hợp nhất.

## Addendum — Rà soát Phase 7–9 và quyết định thiết kế (04/11/2026)

Sau khi chạy lại trên cây hợp nhất, trạng thái được cập nhật như sau:

- Phase 7: các service đã expose OpenAPI, Gateway có proxy URL, và `ApiContractDriftTests` đã kiểm tra runtime spec/baseline. Báo cáo cũ cần được thay bằng evidence mới.
- Phase 8: Prometheus đã báo `up` cho Gateway, sáu service, SQL exporter và RabbitMQ exporter; alert rules được load hợp lệ. Vẫn cần evidence dashboard, alert firing/recovery và SLO report theo workload.
- Phase 9: runner đã có workload đọc và booking ghi, auth, idempotency và exit-code gate. Vẫn cần chạy tải thật, đối soát DB/broker và thu resource/dependency telemetry; invariant C# in-memory không được dùng làm bằng chứng hệ thống.
- Acceptance runtime ngày 04/11/2026 đã chạy: degradation RabbitMQ và soak 60 giây đạt, cleanup đạt; business benchmark có 0 lỗi 5xx nhưng không đạt latency SLO (read p95 752.76 ms, write p95 9396.39 ms). Phase 9 chưa được đóng.
- Booking Saga đã được sửa theo commit-first design: Appointment được commit ở trạng thái `Pending` trước khi `ConfirmReservation`; `AppointmentConfirmationWorker` xác nhận sau commit với retry 10s/1m/5m và bồi hoàn sau 3 lần thất bại. Benchmark trước thay đổi không còn đại diện cho thiết kế mới; cần chạy lại workload và đo confirmation lag.
- Reliability: Medical Records consumer đã có retry hữu hạn 10s/1m/5m trước khi DLQ; cần chạy integration fault test trên RabbitMQ để nghiệm thu hành vi runtime.

Các checklist còn mở chỉ được đóng sau khi có evidence runtime tương ứng; số lượng unit test pass không tự động chứng minh SLO/load acceptance.

Trong quá trình hợp nhất, các lựa chọn A đã chốt cho Phase 5–6 tiếp tục là nguồn chuẩn: `ExamCompleted` chỉ kết thúc encounter, không tự finalize report; không thêm MedicalHistory projection/rebuild khi cùng Medical Records DB đáp ứng; Notification history là nguồn dữ liệu chính, SignalR chỉ là hint và Redis chỉ cần khi scale nhiều instance. Implementation trùng bounded context, generic `NotificationRequested` title/body tự do, `ServiceProxyMiddleware` route lệch và Redis package của Clinic API đã được loại/khôi phục theo implementation A.

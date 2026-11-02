# Phase 5 — Phối hợp Messaging và nghiệm thu của Thành viên B

**Vai trò:** Thành viên A sở hữu Medical Records Service, database, Patient/MedicalReport, API, authorization, audit, migration và cutover. Thành viên B phụ trách phối hợp event contract, broker reliability, consumer failure behavior, contract/failure tests, telemetry và runtime integration. Tài liệu này không giao B xây lại service/domain/API đã được A triển khai.

## Quyết định thiết kế phải tuân theo

- Dùng `AppointmentId` làm định danh ổn định của encounter; không thêm `VisitId` hoặc `EncounterId` thay thế.
- `ExamStarted` mở hoặc cập nhật `EncounterContext` idempotently. Không tự tạo một MedicalReport Draft rỗng.
- `ExamCompleted` chỉ đánh dấu encounter đã kết thúc; không finalize MedicalReport. Bác sĩ finalize qua luồng Medical Records riêng.
- `Patient` và `MedicalReport` cùng thuộc Medical Records Service. Không thêm MedicalHistory projection riêng nếu truy vấn từ DB sở hữu đã đáp ứng; mọi projection mới cần lý do đo được và được A duyệt.
- Event chỉ chứa ID, thời điểm và snapshot cần cho context. Không đưa nội dung bệnh án hoặc dữ liệu cá nhân không cần thiết vào event/log/DLQ.

## Các bước B phụ trách

1. **Contract và producer compatibility:** cùng A xác nhận schema version, `EventId`, correlation/causation, routing key và payload `ExamStarted`/`ExamCompleted`. B kiểm tra Queue producer phát đúng ID/snapshot theo hợp đồng đã chốt; thay đổi contract phải tương thích hoặc tăng schema version.
2. **Consumer reliability:** kiểm tra consumer của Medical Records dùng Inbox/idempotency và local transaction đúng thứ tự; xử lý duplicate, retry hữu hạn, poison message, DLQ, broker restart và graceful shutdown. `ExamCompleted` đến trước `ExamStarted` không được làm thay đổi lifecycle của MedicalReport.
3. **Kiểm thử:** bổ sung contract/failure tests cho duplicate, event đảo thứ tự, consumer crash trước/sau commit, broker restart và retry exhaustion. Phân biệt test component với test dùng SQL Server/RabbitMQ thật.
4. **Telemetry và vận hành:** đo consumer processed/failure/retry/DLQ/lag và correlation; không ghi PHI hoặc access token. Ghi cách chẩn đoán event bằng ID và cách replay có kiểm soát.
5. **E2E chung với A:** từ Queue `ExamStarted` đến EncounterContext, sau đó tạo/lưu Draft, phát `ExamCompleted` và xác nhận context thành Completed trong khi report vẫn Draft cho tới khi bác sĩ finalize.

## Điều kiện hoàn tất phần B

- Duplicate event không tạo tác dụng phụ trùng; event đảo thứ tự không làm report bị finalize hoặc lùi trạng thái.
- Broker/consumer restart không làm mất business transaction đã commit; lỗi hữu hạn đi qua retry/DLQ theo cấu hình hiện hành.
- Contract và telemetry có test/evidence; log và DLQ không chứa nội dung bệnh án.
- E2E xác nhận lifecycle Queue/encounter/report là các trạng thái thuộc owner tương ứng.

**Khoảng trống cần B xử lý sau khi merge:** consumer hiện tại NACK lỗi với `requeue: false`, khiến RabbitMQ đưa event thẳng vào dead-letter queue mà chưa có bounded transient retry queue. B cần bổ sung retry/backoff giới hạn và kiểm thử trên RabbitMQ thật trước khi đánh dấu consumer reliability đạt.

## Ngoài phạm vi của B

Không thiết kế lại entity/API/database, không triển khai audit nghiệp vụ thay A, không tạo `MedicalHistory` projection hoặc endpoint rebuild khi chưa có quyết định, và không finalize hồ sơ từ `ExamCompleted`.

Chi tiết sở hữu nghiệp vụ, API, migration và acceptance của A: [`PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_5_MEDICAL_RECORDS_SERVICE_EXECUTION_PLAN_MEMBER_A.md).

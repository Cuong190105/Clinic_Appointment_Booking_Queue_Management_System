# Phase 6 — Phối hợp Messaging, Gateway và nghiệm thu của Thành viên B

**Vai trò:** Thành viên A sở hữu Notification Service, Notification DB, chính sách/template, delivery, REST/SignalR API, migration và cutover. Thành viên B phối hợp event contract/producer compatibility, broker reliability, Gateway/frontend integration checks, telemetry, failure tests và runbook. B không dựng một Notification Service hoặc Gateway implementation song song với A.

## Quyết định thiết kế phải tuân theo

- `Clinic.API` tiếp tục là Gateway/BFF; mỗi service vẫn tự xác thực JWT.
- Notification Service sở hữu notification persistence, processing, delivery attempts và SignalR Hub.
- Notification DB là nguồn lịch sử in-app chuẩn. SignalR chỉ gửi hint tối thiểu; frontend tải nội dung và phục hồi qua REST Gateway.
- Account/guest policy, contact snapshot, event template và password-reset handling theo contract đã chốt với A. Reset password dùng luồng/contract hạn chế riêng; không đưa reset token vào business event, log hay DLQ.
- Compose hiện chạy một Notification Service instance. Redis backplane chỉ thêm khi triển khai nhiều instance; không yêu cầu Redis cho cấu hình hiện tại.
- Không tuyên bố exactly-once delivery với email/SMS provider. Dùng idempotency, bounded retry, attempt audit và xử lý lỗi theo chính sách do service owner đặt.

## Các bước B phụ trách

1. **Event contract và producer checks:** xác nhận Appointment/Queue producers phát event type/schema version, `EventId`, correlation/causation và contact snapshot cần thiết. Kiểm thử guest/account, walk-in và event đến trước/sau nhau; không tự mở rộng payload sang nội dung tự do không được policy duyệt.
2. **Broker reliability:** xác minh Notification consumer dùng Inbox/idempotency; retry/DLQ hữu hạn; restart/reconnect; poison message được làm sạch theo quy tắc hiện hành. Replay notification delivery chỉ qua operator endpoint có audit; event poison được khôi phục từ source outbox sau khi sửa producer.
3. **Gateway/frontend:** xác nhận REST, WebSocket negotiate/upgrade và authenticated SignalR đi qua `Clinic.API`; quyền user/group không thể bị client giả mạo. Frontend dùng SignalR làm hint và REST history làm recovery path. Không thêm `MapHub` trong Gateway hoặc generic proxy trùng với controller/client proxy đã có.
4. **Telemetry/failure tests:** kiểm thử provider timeout/failure/retry exhaustion, broker restart, event duplicate/out-of-order và signal reconnect. Theo dõi consumer lag, failures, retry, dead-letter và delivery attempt; tránh PHI, reset token và provider secrets.
5. **Vận hành/E2E:** cập nhật runbook theo endpoint hiện hành; chạy acceptance trên Compose với dữ liệu tổng hợp và provider MOCK; ghi rõ những kiểm thử không thể chạy trong môi trường hiện có.

## Điều kiện hoàn tất phần B

- Event từ producer thật tạo delivery đúng policy và không gửi trùng ngoài chính sách.
- Broker/provider failure có bounded retry, DLQ/quarantine hoặc recovery path; restart không làm mất dữ liệu đã commit.
- Gateway REST/SignalR authenticated, user isolation và history recovery được kiểm tra.
- Operator replay có actor audit; runbook khớp endpoint/architecture hiện hành.
- Email/SMS acceptance dùng mock/fake provider, không gửi cho người thật.

## Ngoài phạm vi của B

Không sở hữu Notification entity/DB/API/policy, không map Hub trong Clinic.API, không bắt buộc Redis trong single-instance Compose, không thay REST history bằng SignalR payload, và không tự tạo generic event mang title/body tự do.

Chi tiết implementation và acceptance của A: [`PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md`](PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md). Phase tiếp theo để hoàn thiện OpenAPI/SLO/load test: [`PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md).

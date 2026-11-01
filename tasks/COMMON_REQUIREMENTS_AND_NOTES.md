# Yêu cầu và lưu ý chung khi triển khai Microservices

Tài liệu này áp dụng cho cả Thành viên A và Thành viên B trong quá trình chuyển đổi hệ thống theo kiến trúc được mô tả trong `microservices_analysis.md`.

## 1. Nguyên tắc kiến trúc bắt buộc

- Mỗi service sở hữu database, migration và repository riêng.
- Không dùng foreign key xuyên service.
- Không truy cập trực tiếp database của service khác.
- Không chia sẻ EF entity, domain entity hoặc navigation property giữa các service.
- Chỉ trao đổi bằng API DTO, ID, snapshot cần thiết hoặc versioned event.
- Mỗi aggregate chỉ có một service sở hữu quyền ghi.
- Giao dịch ACID chỉ nằm trong một service.
- Cross-service workflow phải dùng reservation, saga, outbox và compensation.
- Không dùng database dùng chung làm kiến trúc cuối cùng.

## 2. Yêu cầu đối với API

Mỗi API cần có:

- Request và response DTO rõ ràng.
- Version API.
- Error code và error message nhất quán.
- Validation đầu vào.
- Authorization theo permission.
- Correlation ID.
- Timeout và retry policy phù hợp.
- Idempotency key cho command có thể retry.
- Contract test.

Các command bắt buộc hỗ trợ idempotency gồm:

- Reserve slot.
- Release slot.
- Create appointment.
- Check-in.
- Create hoặc finalize medical report.
- Send notification.

## 3. Yêu cầu đối với event và message broker

Mỗi event phải có tối thiểu:

- `eventId`.
- `eventType`.
- `schemaVersion`.
- `occurredAt`.
- `correlationId`.
- `causationId`.
- `producer` hoặc source service.
- Payload chỉ chứa dữ liệu cần thiết.

Message broker phải hỗ trợ hoặc được cấu hình cho:

- Retry có giới hạn.
- Exponential backoff.
- Dead-letter queue.
- Consumer idempotency.
- Theo dõi queue depth và consumer lag.
- Replay hoặc xử lý lại message có kiểm soát.
- Graceful shutdown.

Không được:

- Gửi domain entity vào message.
- Gửi `Person` hoặc navigation object giữa service.
- Đưa dữ liệu bệnh án hoặc contact không cần thiết vào event.
- Retry vô hạn lỗi nghiệp vụ.
- Coi việc publish message trực tiếp trước khi commit là an toàn.

## 4. Outbox và Inbox

### Outbox

- Outbox record phải được ghi cùng local transaction với dữ liệu nghiệp vụ.
- Publisher chỉ gửi message sau khi transaction commit.
- Outbox record phải có trạng thái pending, sent hoặc failed.
- Có retry và số lần retry tối đa.
- Có cơ chế dọn dẹp hoặc lưu trữ theo retention policy.

### Inbox/idempotency

- Consumer lưu `eventId` hoặc idempotency key đã xử lý.
- Kiểm tra event đã xử lý trước khi thực hiện side effect.
- Có unique constraint cho event key.
- Có thể xử lý lại message an toàn sau consumer crash.

## 5. Yêu cầu dữ liệu và consistency

Chấp nhận eventual consistency cho:

- Notification.
- Dashboard.
- Medical history projection.
- Trạng thái Appointment sau `ExamCompleted`.

Không chấp nhận eventual consistency gây ra:

- Overbooking.
- Trùng queue number.
- Trùng appointment do retry.
- Ghi đè report sai version.

Mọi eventual consistency phải có:

- Retry.
- Dead-letter queue.
- Idempotency.
- Reconciliation report hoặc reconciliation job.
- Log và metric để phát hiện trạng thái lệch.

## 6. Quy tắc cho các workflow quan trọng

### Booking

Luồng chuẩn:

```text
ReserveSlot
    -> CreateAppointment
        -> ConfirmReservation
```

Nếu tạo appointment thất bại:

```text
ReleaseReservation
```

Yêu cầu:

- Không dùng `CheckAvailability` rồi mới tạo appointment.
- Reservation phải atomic trong Scheduling Service.
- Saga phải retry được và resume được sau restart.
- Có timeout cho reservation chưa hoàn tất.

### Check-in và Queue

- `AppointmentCheckedIn` phải chứa dữ liệu cần thiết để Queue cấp số.
- Queue Service không gọi Appointment Service để dựng graph dữ liệu.
- Queue number được cấp trong local transaction.
- Có unique constraint chống trùng queue number.
- Duplicate check-in event không tạo ticket thứ hai.

### Exam completion

- Queue Service sở hữu trạng thái queue.
- Appointment Service sở hữu trạng thái appointment.
- Medical Records Service sở hữu trạng thái report.
- Không để một service sửa aggregate của service khác.
- `ExamCompleted` được phát qua broker và xử lý bằng consumer idempotent.

### Medical Records

- Dùng `AppointmentId` làm định danh ổn định của lượt khám; không tạo `VisitId`/`EncounterId` riêng.
- `Patient` và `MedicalReport` thuộc cùng Medical Records Service và database; không tạo Patient Service riêng.
- Không dùng `QueueTicketId` làm liên kết lâu dài.
- `ExamStarted` idempotently mở encounter context theo `AppointmentId`; Draft nội dung được tạo khi bác sĩ bắt đầu ghi.
- `ExamCompleted` chỉ đánh dấu lượt khám kết thúc; không tự finalize report. Draft/Finalized là lifecycle riêng của hồ sơ.
- Hồ sơ Finalized không sửa trực tiếp; mọi amendment là bản ghi append-only có tác giả, thời điểm, lý do và liên kết phiên bản trước.
- Mọi truy cập hồ sơ bệnh án phải được audit.

## 7. Yêu cầu bảo mật

- Mỗi service tự validate JWT bằng public key/JWKS.
- Gateway không phải điểm tin cậy duy nhất.
- Access token nên có thời gian sống ngắn.
- Có refresh token rotation và revoke policy.
- Có key rotation.
- Không log access token, refresh token hoặc credential.
- Không đưa dữ liệu bệnh án vào log, event hoặc notification nếu không cần thiết.
- Permission của Medical Records phải được kiểm tra riêng.
- Audit mọi truy cập và thay đổi hồ sơ bệnh án.

## 8. Yêu cầu kiểm thử

Mỗi service cần có:

- Unit test cho domain invariant.
- Integration test với database thật.
- Contract test cho API.
- Contract test cho event.
- Test transaction và constraint.
- Concurrency test cho reservation và queue number.
- Test idempotency.
- Test duplicate event.
- Test out-of-order event.
- Test broker restart.
- Test consumer crash sau khi commit.
- Test timeout và retry.
- End-to-end test cho booking, check-in, exam completion và notification.

Các lỗi không được bỏ qua:

- Overbooking.
- Trùng queue number.
- Tạo appointment trùng.
- Mất notification sau restart.
- Ghi đè medical report do event đến trễ.
- Consumer xử lý duplicate gây side effect trùng.

## 9. Yêu cầu observability và vận hành

Mỗi service phải có:

- Health endpoint.
- Readiness endpoint.
- Liveness endpoint nếu cần.
- Structured logging.
- Correlation ID.
- Causation ID cho message.
- Distributed tracing.
- Metrics latency.
- Metrics error rate.
- Metrics retry và dead-letter.
- Metrics consumer lag.
- Graceful shutdown.

Docker Compose cần có:

- Health check.
- Persistent volume cho database và broker.
- Resource limit.
- Dependency ordering ở mức phù hợp.
- Retry kết nối.

`depends_on` không thay thế health check và retry kết nối.

## 10. Migration và rollback

Mỗi bounded context nên chuyển theo Strangler Fig:

```text
contract
    -> shadow read
        -> dual write có kiểm soát
            -> chuyển read
                -> chuyển write
                    -> tắt đường cũ
```

Mỗi lần migration phải có:

- Backward-compatible schema change.
- Feature flag.
- Dual-read hoặc dual-write có kiểm soát.
- Data reconciliation.
- Rollback plan.
- Regression test.
- Người chịu trách nhiệm bật/tắt feature.

Không được xóa đường cũ ngay sau khi service mới chạy thành công lần đầu.

## 11. Quy trình làm việc nhóm 2 người

### Trước khi code

- Hai thành viên thống nhất contract.
- Xác định owner của từng entity và trạng thái.
- Chốt cách đặt tên API, event và database.
- Chốt Definition of Done.

### Trong khi code

- Thành viên A phụ trách business logic, API và database.
- Thành viên B phụ trách broker, consumer, test tích hợp và observability.
- Thay đổi contract phải được cả hai review.
- Không merge khi chưa có test tương ứng.
- Mỗi pull request chỉ nên tập trung vào một bounded context hoặc một workflow.

### Trước khi hoàn thành giai đoạn

- Chạy unit, integration, contract và failure test.
- Chạy migration trên database mới.
- Kiểm tra không có truy cập database xuyên service.
- Kiểm tra duplicate/out-of-order event.
- Kiểm tra log, metric, tracing và health check.
- Cập nhật tài liệu vận hành và rollback.

## 12. Tiêu chí nghiệm thu toàn bộ hệ thống

Dự án được xem là đạt khi:

- Mỗi service có database riêng.
- Không có service nào truy cập database của service khác.
- Booking không overbook khi concurrent.
- Queue number không bị trùng.
- Các command quan trọng đều idempotent.
- Saga có compensation và resume sau restart.
- Event có retry, dead-letter và reconciliation.
- Notification không mất khi process restart.
- Medical Records có phân quyền và audit.
- Frontend chỉ giao tiếp qua API Gateway.
- Có tài liệu triển khai, rollback và xử lý sự cố.

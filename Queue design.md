# Message Queue Design

## 1. Mục tiêu

Message Queue dùng RabbitMQ làm hạ tầng giao tiếp bất đồng bộ giữa các service. Thiết kế phải bảo đảm:

- Message không bị mất sau khi business transaction đã commit.
- Consumer xử lý được duplicate message và retry an toàn.
- Message lỗi được retry hữu hạn rồi chuyển vào Dead-Letter Queue (DLQ).
- Có thể truy vết một request từ API qua service đến RabbitMQ.
- Có thể mở rộng consumer mà không chia sẻ database hoặc domain entity.

RabbitMQ chỉ vận chuyển integration event. Business data vẫn thuộc database của service phát sinh hoặc service tiêu thụ.

## 2. Công nghệ và nguyên tắc

- Broker: RabbitMQ 3.x, bật Management UI.
- Protocol: AMQP 0-9-1.
- Client: `RabbitMQ.Client` hoặc một abstraction nội bộ dùng chung.
- Exchange và queue đều durable.
- Message persistent (`DeliveryMode = 2`).
- Bật publisher confirms.
- Consumer dùng manual acknowledgement.
- Không publish trực tiếp từ controller hoặc handler sau `SaveChangesAsync`.
- Không gửi EF entity, domain entity, navigation property hoặc dữ liệu bệnh án không cần thiết.

## 3. Topology chính xác cần tạo

Phần này là danh sách chuẩn để tạo trong RabbitMQ. Không tạo ba retry queue cho từng event. Mỗi consumer/service có một main queue, ba retry queue theo delay và một DLQ.

### 3.1. Exchange bắt buộc

| Name | Type | Durable | Auto-delete | Mục đích |
|---|---|---:|---:|---|
| `clinic.events` | `topic` | Yes | No | Event sau khi business transaction commit |
| `clinic.retry.10s` | `topic` | Yes | No | Retry lần 1 |
| `clinic.retry.1m` | `topic` | Yes | No | Retry lần 2 |
| `clinic.retry.5m` | `topic` | Yes | No | Retry lần 3 |
| `clinic.dead-letter` | `direct` | Yes | No | Message không thể xử lý |

`clinic.commands` không nằm trong topology Phase 0 hiện tại vì các workflow đang dùng integration event. Chỉ tạo exchange này khi có command bất đồng bộ cụ thể.

Routing key chuẩn trên `clinic.events`:

```text
appointment.created
appointment.confirmed
appointment.cancelled
appointment.checked-in
queue.patient-checked-in
queue.patient-called
queue.exam-started
queue.exam-completed
queue.patient-skipped
schedule.created
schedule.updated
schedule.cancelled
```

### 3.2. Main queue và binding

| Queue | Binding exchange | Binding key |
|---|---|---|
| `q.appointment-service.events` | `clinic.events` | `queue.exam-completed` |
| `q.queue-service.events` | `clinic.events` | `appointment.checked-in` |
| `q.medical-records-service.events` | `clinic.events` | `queue.exam-started` |
| `q.medical-records-service.events` | `clinic.events` | `queue.exam-completed` |
| `q.notification-service.events` | `clinic.events` | `appointment.*` |
| `q.notification-service.events` | `clinic.events` | `queue.*` |
| `q.projection-service.events` | `clinic.events` | `appointment.*` |
| `q.projection-service.events` | `clinic.events` | `queue.*` |
| `q.projection-service.events` | `clinic.events` | `schedule.*` |
| `q.appointment-service.events` | `clinic.events` | `appointment-service.#` |
| `q.queue-service.events` | `clinic.events` | `queue-service.#` |
| `q.medical-records-service.events` | `clinic.events` | `medical-records-service.#` |
| `q.notification-service.events` | `clinic.events` | `notification-service.#` |
| `q.projection-service.events` | `clinic.events` | `projection-service.#` |

Arguments của mọi main queue:

```text
durable: true
auto_delete: false
exclusive: false
x-dead-letter-exchange: clinic.dead-letter
x-dead-letter-routing-key: <dlq-routing-key-của-queue>
```

Ví dụ với Queue Service:

```text
Queue: q.queue-service.events
x-dead-letter-exchange: clinic.dead-letter
x-dead-letter-routing-key: q.queue-service.dlq
```

### 3.3. Retry queue

Tạo ba retry queue cho từng main queue ở trên. Danh sách đầy đủ:

```text
q.appointment-service.retry.10s
q.appointment-service.retry.1m
q.appointment-service.retry.5m

q.queue-service.retry.10s
q.queue-service.retry.1m
q.queue-service.retry.5m

q.medical-records-service.retry.10s
q.medical-records-service.retry.1m
q.medical-records-service.retry.5m

q.notification-service.retry.10s
q.notification-service.retry.1m
q.notification-service.retry.5m

q.projection-service.retry.10s
q.projection-service.retry.1m
q.projection-service.retry.5m
```

Arguments cụ thể:

| Queue suffix | `x-message-ttl` | `x-dead-letter-exchange` | `x-dead-letter-routing-key` |
|---|---:|---|---|
| `.retry.10s` | `10000` | `clinic.events` | Không đặt |
| `.retry.1m` | `60000` | `clinic.events` | Không đặt |
| `.retry.5m` | `300000` | `clinic.events` | Không đặt |

Các retry queue đều có:

```text
durable: true
auto_delete: false
exclusive: false
```

Mỗi retry queue bind vào exchange tương ứng với routing key riêng của consumer:

```text
clinic.retry.10s -> q.appointment-service.retry.10s       key: appointment-service.#
clinic.retry.1m  -> q.appointment-service.retry.1m        key: appointment-service.#
clinic.retry.5m  -> q.appointment-service.retry.5m         key: appointment-service.#

clinic.retry.10s -> q.queue-service.retry.10s              key: queue-service.#
clinic.retry.1m  -> q.queue-service.retry.1m               key: queue-service.#
clinic.retry.5m  -> q.queue-service.retry.5m               key: queue-service.#
```

Lặp cùng quy tắc cho ba consumer còn lại. Danh sách binding retry đầy đủ:

| Exchange | Queue | Binding key |
|---|---|---|
| `clinic.retry.10s` | `q.appointment-service.retry.10s` | `appointment-service.#` |
| `clinic.retry.1m` | `q.appointment-service.retry.1m` | `appointment-service.#` |
| `clinic.retry.5m` | `q.appointment-service.retry.5m` | `appointment-service.#` |
| `clinic.retry.10s` | `q.queue-service.retry.10s` | `queue-service.#` |
| `clinic.retry.1m` | `q.queue-service.retry.1m` | `queue-service.#` |
| `clinic.retry.5m` | `q.queue-service.retry.5m` | `queue-service.#` |
| `clinic.retry.10s` | `q.medical-records-service.retry.10s` | `medical-records-service.#` |
| `clinic.retry.1m` | `q.medical-records-service.retry.1m` | `medical-records-service.#` |
| `clinic.retry.5m` | `q.medical-records-service.retry.5m` | `medical-records-service.#` |
| `clinic.retry.10s` | `q.notification-service.retry.10s` | `notification-service.#` |
| `clinic.retry.1m` | `q.notification-service.retry.1m` | `notification-service.#` |
| `clinic.retry.5m` | `q.notification-service.retry.5m` | `notification-service.#` |
| `clinic.retry.10s` | `q.projection-service.retry.10s` | `projection-service.#` |
| `clinic.retry.1m` | `q.projection-service.retry.1m` | `projection-service.#` |
| `clinic.retry.5m` | `q.projection-service.retry.5m` | `projection-service.#` |

Tổng số queue cần tạo:

```text
5 main queue + 15 retry queue + 5 DLQ = 25 queue
```

Tổng số exchange bắt buộc:

```text
1 event exchange + 3 retry exchange + 1 dead-letter exchange = 5 exchange
```

Không đặt `x-dead-letter-routing-key` trên retry queue. Application phải publish retry với routing key có prefix consumer. Ví dụ:

```text
Main event:       appointment.checked-in
Retry route:      queue-service.appointment.checked-in
Retry queue bind: queue-service.#
After TTL:        clinic.events, route queue-service.appointment.checked-in
Main queue bind:  queue-service.#
```

Nhờ đó message retry chỉ quay lại `q.queue-service.events`, không bị fan-out sang retry queue hoặc main queue của consumer khác. Message retry không còn dùng routing key event gốc trực tiếp.

### 3.4. Dead-Letter Queue

Tạo năm DLQ:

```text
q.appointment-service.dlq
q.queue-service.dlq
q.medical-records-service.dlq
q.notification-service.dlq
q.projection-service.dlq
```

Arguments của mỗi DLQ:

```text
durable: true
auto_delete: false
exclusive: false
```

Binding vào `clinic.dead-letter`:

```text
clinic.dead-letter -> q.appointment-service.dlq          key: q.appointment-service.dlq
clinic.dead-letter -> q.queue-service.dlq                 key: q.queue-service.dlq
clinic.dead-letter -> q.medical-records-service.dlq       key: q.medical-records-service.dlq
clinic.dead-letter -> q.notification-service.dlq           key: q.notification-service.dlq
clinic.dead-letter -> q.projection-service.dlq             key: q.projection-service.dlq
```

Mapping `x-dead-letter-routing-key` của main queue phải khớp chính xác với bảng trên:

| Main queue | `x-dead-letter-exchange` | `x-dead-letter-routing-key` |
|---|---|---|
| `q.appointment-service.events` | `clinic.dead-letter` | `q.appointment-service.dlq` |
| `q.queue-service.events` | `clinic.dead-letter` | `q.queue-service.dlq` |
| `q.medical-records-service.events` | `clinic.dead-letter` | `q.medical-records-service.dlq` |
| `q.notification-service.events` | `clinic.dead-letter` | `q.notification-service.dlq` |
| `q.projection-service.events` | `clinic.dead-letter` | `q.projection-service.dlq` |

DLQ không có TTL và không có DLX tiếp theo. Message trong DLQ chỉ được replay thủ công sau khi đã xử lý nguyên nhân lỗi.

### 3.5. Sơ đồ cho một consumer

```text
clinic.events
    |
    v
q.queue-service.events
    |
    +-- transient error #1 --> clinic.retry.10s
    |                         key: queue-service.appointment.checked-in
    +-- transient error #2 --> clinic.retry.1m
    |                         key: queue-service.appointment.checked-in
    +-- transient error #3 --> clinic.retry.5m
                              key: queue-service.appointment.checked-in
    +-- permanent error ----> publish clinic.dead-letter

clinic.retry.10s -- TTL 10s --> clinic.events
                              key: queue-service.appointment.checked-in
clinic.retry.1m  -- TTL 1m  --> clinic.events
                              key: queue-service.appointment.checked-in
clinic.retry.5m  -- TTL 5m  --> clinic.events
                              key: queue-service.appointment.checked-in
```

## 4. Chính sách retry và ACK

Consumer không dùng `requeue=true` vô hạn. Khi xử lý lỗi:

1. Publish message vào retry exchange hoặc DLX với publisher confirm. Retry routing key phải có prefix consumer, ví dụ `queue-service.appointment.checked-in`.
2. Chỉ `ACK` message gốc sau khi publish thành công.
3. Nếu publish retry/DLQ thất bại thì không ACK message gốc.
4. RabbitMQ sẽ redeliver message gốc.

| Lỗi | Hành động |
|---|---|
| RabbitMQ mất kết nối | Không ACK, redeliver |
| Database tạm thời unavailable | Retry 10s/1m/5m |
| Timeout dependency | Retry hữu hạn |
| Payload sai schema | Publish DLQ rồi ACK |
| Business rule không hợp lệ | Publish DLQ/rejected rồi ACK |
| Duplicate event | ACK, không tạo side effect |

Application phải ghi các headers khi publish retry/DLQ:

```text
x-retry-count
x-original-routing-key
x-last-error
x-failed-at
```

`eventId` trong envelope vẫn giữ nguyên trong quá trình retry để Inbox nhận diện duplicate.

## 5. Event envelope

Tất cả message dùng cùng một envelope:

```json
{
  "eventId": "9e4c3e1e-1f6e-4a44-9b9f-2a9f2f5d7f10",
  "eventType": "AppointmentCheckedIn",
  "schemaVersion": 1,
  "occurredAt": "2026-10-16T10:00:00Z",
  "correlationId": "c8a1b2c3-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
  "causationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "producer": "appointment-service",
  "payload": {}
}
```

Ý nghĩa:

- `eventId`: định danh duy nhất, dùng cho Inbox/idempotency.
- `eventType`: tên event ổn định, không phụ thuộc tên class C#.
- `schemaVersion`: tăng khi thay đổi payload không tương thích.
- `occurredAt`: thời điểm event xảy ra ở producer.
- `correlationId`: liên kết toàn bộ workflow/request.
- `causationId`: event hoặc command trực tiếp gây ra event hiện tại.
- `producer`: service phát event.
- `payload`: DTO tối thiểu cần cho consumer.

AMQP headers nên có:

```text
message-id       = eventId
type              = eventType
correlation-id    = correlationId
causation-id      = causationId
schema-version    = schemaVersion
content-type      = application/json
```

## 6. Outbox Publisher

### 6.1. Bảng Outbox

Mỗi service có bảng Outbox riêng:

```text
OutboxMessage
-------------
Id, EventId, EventType, SchemaVersion, Payload
OccurredAt, CorrelationId, CausationId, Producer
Status              -- Pending, Publishing, Sent, Failed
RetryCount, NextAttemptAt, LastError
CreatedAt, PublishedAt
```

### 6.2. Luồng publish

```text
Business handler
    |
    +-- cập nhật business data
    +-- ghi OutboxMessage = Pending
    |
Local DB transaction commit
    |
Outbox Publisher đọc Pending
    |
Publish vào clinic.events
    |
RabbitMQ publisher confirm
    |
Đánh dấu OutboxMessage = Sent
```

Nếu transaction rollback, không có event được publish. Nếu RabbitMQ lỗi sau khi database commit, message vẫn còn ở Outbox và sẽ được publish lại.

Publisher phải claim message bằng lock hoặc trạng thái `Publishing` có timeout, publish persistent, chờ publisher confirm, chỉ đánh dấu `Sent` sau confirm và áp dụng retention policy cho message đã gửi.

### 6.3. Những điểm phải ghi Outbox trong ứng dụng

Handler chỉ được ghi Outbox; handler không được publish trực tiếp lên RabbitMQ. Outbox record phải được thêm vào cùng `DbContext` với business entity và được commit trong cùng local transaction.

| Handler/use case | Event | Routing key | Ghi chú |
|---|---|---|---|
| `CreateAppointment` hoặc bước hoàn tất saga booking | `AppointmentCreated` | `appointment.created` | Phát khi appointment đã đạt trạng thái được các service khác công nhận |
| `ConfirmAppointment` | `AppointmentConfirmed` | `appointment.confirmed` | Chỉ phát nếu event này có consumer riêng |
| `CancelAppointment` | `AppointmentCancelled` | `appointment.cancelled` | Compensation/reservation và notification xử lý bất đồng bộ |
| `CheckIn` | `AppointmentCheckedIn` | `appointment.checked-in` | Bắt buộc; Queue Service dùng để cấp số |
| `CallNextQueue` | `PatientCalled` | `queue.patient-called` | Notification/projection/dashboard |
| `StartExam` | `ExamStarted` | `queue.exam-started` | Medical Records có thể mở encounter |
| `CompleteExam` | `ExamCompleted` | `queue.exam-completed` | Bắt buộc; Appointment Service cập nhật trạng thái riêng |
| `SkipQueue` | `PatientSkipped` | `queue.patient-skipped` | Phát nếu có consumer cần biết bệnh nhân bị bỏ qua |
| `AddDoctorSchedule` | `WorkScheduleCreated` | `schedule.created` | Projection/availability |
| `UpdateWorkSchedule` | `WorkScheduleUpdated` | `schedule.updated` | Projection/availability |
| `CancelWorkSchedule` | `WorkScheduleCancelled` | `schedule.cancelled` | Có thể kích hoạt compensation cho appointment |

Không ghi Outbox cho query, login/refresh token, thao tác CRUD nội bộ không có consumer và các thao tác chỉ đọc.

Thứ tự bắt buộc trong handler:

```text
1. Đọc aggregate và kiểm tra business rule
2. Thay đổi business entity
3. Tạo EventEnvelope với eventId mới
4. Thêm OutboxMessage = Pending vào cùng DbContext
5. SaveChanges/Commit local transaction
6. Outbox Publisher đọc Pending và publish sau commit
```

`eventId` được tạo một lần khi ghi Outbox và phải giữ nguyên qua mọi lần retry. `correlationId` giữ nguyên trong toàn bộ workflow. `causationId` trỏ đến command/event trực tiếp gây ra event hiện tại.

Trong giai đoạn monolith chuyển tiếp, `AppointmentId` tạm thời được dùng làm khóa ổn định cho `visitId`. Khi Encounter/Visit aggregate được tạo, phải thay bằng `VisitId` thực sự và tăng `schemaVersion` nếu payload thay đổi không tương thích.

## 7. Consumer và Inbox

### 7.1. Inbox store

Mỗi service tiêu thụ event phải có Inbox store:

```text
InboxMessage
------------
EventId, ConsumerName, EventType, ReceivedAt
ProcessedAt, Status, RetryCount, LastError
```

Unique constraint:

```text
UNIQUE(EventId, ConsumerName)
```

### 7.2. Luồng consumer

```text
RabbitMQ delivers message
    |
    +-- deserialize và validate envelope
    +-- kiểm tra Inbox(EventId, ConsumerName)
    |
    +-- nếu đã Processed: ACK, không làm side effect
    |
    +-- local DB transaction
    +-- ghi Inbox = Processing
    +-- cập nhật projection/business data
    +-- ghi Outbox event tiếp theo nếu cần
    +-- ghi Inbox = Processed
    +-- commit
    |
ACK message
```

Nếu consumer crash trước commit, message không được ACK và sẽ redeliver. Nếu crash sau commit nhưng trước ACK, Inbox nhận diện duplicate và consumer chỉ ACK message.

### 7.3. Quy tắc triển khai Inbox/Idempotency Store

Mỗi consumer phải có một `consumerName` ổn định, ví dụ:

```text
queue-service.appointment-checked-in
appointment-service.exam-completed
notification-service.appointment-cancelled
```

Inbox phải lưu tối thiểu:

```text
InboxMessage
------------
Id
EventId
ConsumerName
EventType
SchemaVersion
CorrelationId
Status                 -- Processing, Processed, Failed
ReceivedAt
ProcessingStartedAt
ProcessedAt
RetryCount
LastError
```

Khóa duy nhất bắt buộc:

```text
UNIQUE(EventId, ConsumerName)
```

Không dùng `EventId` đơn lẻ làm khóa duy nhất vì nhiều consumer khác nhau được phép xử lý cùng một event.

Consumer phải thực hiện các bước sau:

```text
1. Deserialize và validate EventEnvelope
2. Tìm Inbox(EventId, ConsumerName)
3. Nếu Processed: ACK ngay, không tạo side effect
4. Nếu Processing chưa hết lease: không xử lý song song
5. Nếu chưa có hoặc lease đã hết hạn: claim thành Processing
6. Cập nhật business data trong local transaction
7. Ghi Inbox = Processed trong cùng transaction
8. Commit transaction
9. ACK RabbitMQ
```

`InboxMessage` và business side effect phải commit cùng transaction của service tiêu thụ. Không đánh dấu `Processed` trước khi side effect commit thành công.

Lease mặc định là 5 phút. Nếu consumer crash sau khi ghi `Processing`, message được phép claim lại sau khi lease hết hạn. Nếu consumer crash sau khi transaction commit nhưng trước ACK, RabbitMQ redeliver message; Inbox thấy `Processed` và ACK mà không chạy side effect lần hai.

Nếu lỗi tạm thời, consumer ghi `Failed`, publish retry có giới hạn rồi ACK message gốc sau khi publish retry thành công. Nếu lỗi schema hoặc lỗi nghiệp vụ không thể retry, publish DLQ rồi ACK message gốc. Không dùng `requeue=true` vô hạn.

`Idempotency-Key` của HTTP command và cặp `(EventId, ConsumerName)` của Inbox là hai cơ chế riêng biệt; command có side effect cần hỗ trợ cả hai khi đi qua broker.

### 7.4. Consumer AppointmentCheckedIn trong monolith

Consumer hiện tại được đăng ký tại `Clinic.API` với queue `q.queue-service.events` và consumer name `queue-service.appointment-checked-in`.

`CheckInHandler` trong monolith đã tạo `QueueTicket` trước khi ghi `AppointmentCheckedIn` vào Outbox. Vì vậy consumer không tạo ticket lại; nó validate `AppointmentId`, `PatientId`, `DoctorId`, kiểm tra ticket đã tồn tại, ghi Inbox = `Processed` trong transaction và ACK sau commit.

Khi tách Queue Service thành service riêng, thay logic transitional này bằng use case sinh `QueueTicket` trong database của Queue Service. Inbox và ticket phải vẫn commit trong cùng local transaction.

Consumer tự động khai báo exchange, main queue, retry queue và DLQ khi khởi động. Nếu không thể publish retry/DLQ, consumer không ACK message gốc để RabbitMQ redeliver.

## 8. Ordering và concurrency

RabbitMQ chỉ bảo đảm thứ tự tương đối trong cùng một queue khi có một consumer tuần tự. Với nhiều consumer, không được giả định thứ tự toàn hệ thống.

Đối với aggregate cần thứ tự:

- Gửi `aggregateId` và `aggregateVersion` hoặc `sequence` trong event.
- Consumer từ chối hoặc trì hoãn event có version cũ.
- Chỉ scale nhiều consumer khi các message khác aggregate có thể xử lý song song.

Ví dụ thứ tự của một lượt khám:

```text
PatientCheckedIn -> PatientCalled -> ExamStarted -> ExamCompleted
```

Event đến trễ không được đưa aggregate về trạng thái cũ.

## 9. Các event chính

```text
AppointmentCreated
AppointmentConfirmed
AppointmentCancelled
AppointmentCheckedIn

PatientCheckedIn
PatientCalled
ExamStarted
ExamCompleted
PatientSkipped

WorkScheduleCreated
WorkScheduleUpdated
WorkScheduleCancelled
```

`AppointmentCheckedIn` phải chứa đủ snapshot để Queue Service cấp số mà không gọi ngược Appointment Service:

```json
{
  "appointmentId": "guid",
  "patientId": "guid",
  "doctorId": "guid",
  "scheduleDate": "2026-10-16",
  "visitId": "guid",
  "patientName": "Nguyen Van A",
  "roomNumber": "P101"
}
```

`ExamCompleted` chỉ chứa ID và thời điểm cần thiết:

```json
{
  "visitId": "guid",
  "ticketId": "guid",
  "appointmentId": "guid",
  "doctorId": "guid",
  "completedAt": "2026-10-16T10:30:00Z"
}
```

Không đưa chẩn đoán, toa thuốc hoặc nội dung medical report vào event thông báo thông thường.

## 10. Correlation và tracing

`X-Correlation-Id` từ request HTTP phải được giữ trong toàn bộ luồng:

```text
HTTP request -> application log -> OutboxMessage
             -> RabbitMQ headers -> consumer log -> event tiếp theo
```

Nếu request không có correlation ID, Gateway hoặc service đầu tiên phải tạo mới. `causationId` bằng `eventId` của message trực tiếp gây ra event tiếp theo.

Không ghi access token, refresh token, password, thông tin thanh toán hoặc nội dung bệnh án vào log.

## 11. Health check và vận hành

RabbitMQ cần health check:

```text
GET /health/live       -- process đang chạy
GET /health/ready      -- RabbitMQ và dependency sẵn sàng
```

Docker Compose tối thiểu:

```yaml
rabbitmq:
  image: rabbitmq:3.13-management
  hostname: clinic-rabbitmq
  ports:
    - "5672:5672"
    - "15672:15672"
  environment:
    RABBITMQ_DEFAULT_USER: clinic
    RABBITMQ_DEFAULT_PASS: clinic-dev-password
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
```

Metrics tối thiểu:

- Queue depth và consumer lag.
- Publish success/failure.
- Outbox pending count.
- Inbox duplicate count.
- Retry count và DLQ message count.
- Message processing duration.
- Consumer error rate.

DLQ phải có quy trình replay có kiểm soát, không tự động replay vô hạn.

## 12. Failure test bắt buộc

- Business transaction rollback không tạo message trong RabbitMQ.
- Business transaction commit nhưng broker restart: Outbox publish lại thành công.
- Publisher crash trước publisher confirm.
- Consumer crash trước khi commit local transaction.
- Consumer crash sau commit nhưng trước ACK.
- Duplicate event không tạo side effect thứ hai.
- Event out-of-order không làm trạng thái quay lùi.
- Message lỗi retry đúng số lần rồi vào DLQ.
- Queue được khôi phục sau process restart.
- Correlation ID được giữ từ HTTP request đến consumer.
- Hai consumer nhận cùng event nhưng mỗi consumer xử lý đúng một lần.

## 13. Tiêu chí hoàn thành Phase 0

- RabbitMQ chạy ổn định trong local Docker Compose.
- Exchange, queue, routing key và DLQ được khai báo tự động khi service khởi động.
- Event envelope có đủ metadata và schema version.
- Outbox chỉ publish sau local transaction commit.
- Consumer có Inbox và unique idempotency key.
- Retry có giới hạn và exponential backoff.
- Message lỗi được chuyển DLQ.
- Có structured logging, correlation ID và metrics cơ bản.
- Có test broker restart, consumer crash, duplicate event và out-of-order event.

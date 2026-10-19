# Phase 0: vận hành và nghiệm thu

Tài liệu này hướng dẫn chạy stack local, quan sát messaging telemetry, kiểm tra recovery khi broker lỗi và nghiệm thu các luồng đã triển khai. Các broker failure tests dùng RabbitMQ thật cùng queue có tên riêng cho từng test; chúng không xóa hoặc purge queue của ứng dụng.

## Khởi động và kiểm tra dịch vụ

Tại thư mục gốc repository:

```powershell
docker compose up -d --build
docker compose ps
```

Chờ SQL Server, RabbitMQ, backend, Prometheus và Jaeger báo healthy/running. Các địa chỉ local:

- API health: `http://localhost:5000/healthz`
- Metrics scrape endpoint: `http://localhost:5000/metrics`
- RabbitMQ Management: `http://localhost:15672` (tài khoản local được khai báo trong `docker-compose.yml`)
- Prometheus: `http://localhost:9090`
- Jaeger UI: `http://localhost:16686`

Prometheus scrape backend trên mạng Compose ở `backend:8080/metrics`, mỗi 15 giây. Jaeger nhận OTLP/gRPC tại `jaeger:4317`; API gửi ASP.NET request spans và spans từ outbox publisher/consumer. Backend có thể khởi động trước Jaeger; OTLP exporter sẽ thử gửi lại theo chính sách của exporter.

## Quan sát metrics và trace

Trong Prometheus, mở **Graph** và tìm các metric bắt đầu bằng `clinic_`. Các metric messaging chính:

- `clinic_outbox_published_total`, `clinic_outbox_publish_failures_total`, `clinic_outbox_publish_retries_total`, `clinic_outbox_dead_lettered_total`
- `clinic_consumer_processed_total`, `clinic_consumer_duplicates_total`, `clinic_consumer_failures_total`, `clinic_consumer_retries_total`, `clinic_consumer_dead_lettered_total`
- `clinic_consumer_lag_seconds` histogram (Prometheus cũng cung cấp `_bucket`, `_sum`, `_count`)
- HTTP request duration và request count do ASP.NET Core instrumentation cung cấp.

Các counter nghiệp vụ chỉ xuất hiện sau khi có lần publish/consume tương ứng; metrics endpoint luôn có sẵn runtime/HTTP metrics và metadata của service.

Một số truy vấn dùng để bắt đầu:

```promql
rate(clinic_outbox_publish_failures_total[5m])
rate(clinic_consumer_failures_total[5m])
rate(clinic_consumer_dead_lettered_total[15m])
```

Counter của ứng dụng đo lượng event đã xử lý, lỗi, retry và dead-letter. Consumer lag là thời gian từ `occurredAt` đến khi xử lý. RabbitMQ queue depth có thể xem trực tiếp trong Management UI bằng các queue `q.queue-service.events`, `q.queue-service.retry.*` và `q.queue-service.dlq`.

Trong Jaeger, chọn service `clinic-api`, đặt lookback phù hợp và tìm trace. Request middleware giữ `X-Correlation-ID`; outbox ghi trace context vào message headers để span publish/consume nối với trace gốc. Có thể gửi một request với correlation ID riêng để dễ tìm:

```powershell
$correlationId = [guid]::NewGuid().ToString()
Invoke-WebRequest -Uri http://localhost:5000/swagger/index.html -Headers @{ "X-Correlation-ID" = $correlationId }
```

Health endpoint được loại khỏi ASP.NET trace để tránh tạo nhiễu liên tục. Truy cập `/metrics` cũng được loại khỏi trace.

## Xử lý broker restart hoặc mất kết nối

Đây là thao tác gây gián đoạn ngắn cho publisher/consumer; chỉ thực hiện trong stack local/dev đã thống nhất thời điểm bảo trì:

1. Ghi nhận trạng thái ban đầu bằng `docker compose ps`, RabbitMQ queue depth và số outbox `Pending`/`Failed`.
2. Restart riêng broker:

   ```powershell
   docker compose restart rabbitmq
   ```

3. Chờ RabbitMQ healthy:

   ```powershell
   docker compose ps rabbitmq
   ```

4. Xem backend tự kết nối lại:

   ```powershell
   docker compose logs --since 10m backend
   ```

   Log `Outbox publisher iteration failed` trong thời gian broker dừng là dấu hiệu mất kết nối; sau khi broker healthy, publisher/consumer phải tạo lại connection và tiếp tục.

5. Xác nhận outbox đã gửi lại hoặc đang được retry, consumer tiếp tục xử lý, và queue depth không tăng liên tục. Có thể kiểm tra `OutboxMessages` theo trạng thái `Pending`, `Failed`, `Sent`, `DeadLetterPending`, `DeadLettered`.
6. Tạo một check-in mới hoặc publish một business event hợp lệ và xác nhận event xuất hiện trong queue/được consumer xử lý. Không purge queue để “làm sạch” trạng thái.

## Điều tra retry và dead-letter

- Xem `clinic_consumer_failures_total`, `clinic_consumer_retries_total`, `clinic_consumer_dead_lettered_total`, `clinic_outbox_publish_failures_total` và `clinic_outbox_dead_lettered_total` trên Prometheus.
- Xem payload, retry headers, error và correlation ID trong RabbitMQ Management UI. Payload ở DLQ phải được xử lý như dữ liệu vận hành nhạy cảm; không chép thông tin bệnh nhân vào ticket/log.
- Sửa nguyên nhân trước khi phát lại. Khi replay, giữ nguyên `eventId` để inbox nhận diện duplicate và không lặp side effect.
- Chỉ xóa/replay message sau khi ghi lại message ID, nguyên nhân và người thực hiện. Không retry lỗi nghiệp vụ vĩnh viễn vô hạn.
- Nếu outbox còn `Publishing` quá 5 phút, publisher sẽ xem claim đó là hết hạn và thử claim lại. Nếu message vào DLQ, kiểm tra trạng thái/error trong SQL và queue trước khi replay.

## Kiểm thử và nghiệm thu

Yêu cầu SQL Server và RabbitMQ trong Compose đang chạy. Từ root:

```powershell
dotnet test backend/Clinic.Application.UnitTests/Clinic.Application.UnitTests.csproj --no-restore --filter FullyQualifiedName~CheckInTests
dotnet test backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj --no-restore
```

Nghiệm thu đạt khi:

- Unit check-in tests pass; cùng idempotency key không tạo ticket lần hai và key rỗng bị từ chối.
- Baseline workflow integration tests pass trên SQL Server.
- `RabbitMqFailureIntegrationTests` pass trên RabbitMQ thật: consumer crash sau SQL commit dẫn tới broker redelivery nhưng inbox deduplicate; event đến trước appointment được retry mà không bị ghi nhận đã xử lý.
- `TelemetryIntegrationTests` xác nhận `/metrics` trả scrape payload; Prometheus scrape target `clinic-api` phải `UP` và HTTP/runtime metrics phải có dữ liệu.
- Prometheus hiển thị dữ liệu mới sau một chu kỳ scrape và Jaeger tìm thấy trace của request có correlation ID.
- Thao tác restart RabbitMQ ở runbook hoàn tất: broker healthy trở lại, backend tự kết nối lại, outbox và consumer tiếp tục.
- Message lỗi đi qua retry policy và cuối cùng vào DLQ theo cấu hình; queue depth, failure, retry và lag có thể được quan sát.
- `docker compose config` hợp lệ và health checks SQL Server/RabbitMQ/backend không báo unhealthy.

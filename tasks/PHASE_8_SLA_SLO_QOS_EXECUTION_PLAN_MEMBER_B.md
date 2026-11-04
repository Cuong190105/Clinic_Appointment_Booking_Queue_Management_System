# Phase 8 — SLA/SLO, QoS và observability (Member B)

## Trạng thái rà soát: Implementation đã có; runtime acceptance đã xác minh một phần

### Runtime evidence — 04/11/2026

- Prometheus API báo `9/9` target trong scope Compose đang `up`: Gateway, sáu service nghiệp vụ, SQL exporter và RabbitMQ exporter.
- Prometheus có dữ liệu request rate và latency histogram tại thời điểm kiểm tra (`http_server_request_duration_seconds_count` và bucket).
- Alert groups được load với `health: ok`; chưa có controlled firing/recovery evidence cho từng alert.
- Phase 8 vì vậy đã xác minh scrape/configuration runtime, nhưng chưa đóng đầy đủ dashboard/alert firing và SLO report.
- Booking confirmation hiện là bất đồng bộ sau commit; SLO cần đo riêng thời gian HTTP tạo Appointment và confirmation lag từ `Pending` đến `Confirmed`/`Cancelled`, không gộp hai đại lượng này.

### Đã có

- Gateway có rate limiter fixed-window theo subject/IP, mặc định 100 request/phút và trả 429 khi vượt giới hạn.
- Prometheus hiện cấu hình cho Gateway, sáu service nghiệp vụ, SQL exporter và RabbitMQ exporter.
- Có rule alerts và Grafana dashboard làm khung triển khai.
- Có unit-test khung cho limiter, timeout, correlation ID và sanitization.

### Chưa đạt hoặc chưa có bằng chứng

- Cần lưu evidence runtime theo commit/thời gian; không chỉ dựa vào file cấu hình.
- Alert/dashboard phải được đối chiếu tên metric, label, histogram bucket và RabbitMQ exporter thực sự có trong Compose. Chưa chứng minh các query đang trả dữ liệu đúng.
- `QoSFailurePolicyTests` hiện kiểm tra limiter độc lập hoặc tạo ProblemDetails/chuỗi log mẫu; không gọi Gateway, không gây timeout downstream thật và không quan sát logger/exporter thật.
- SLO cần định nghĩa denominator, cửa sổ đo, loại trừ 4xx nghiệp vụ và đo riêng các luồng đa service. Chưa có báo cáo đo đủ các SLI này.
- Rate limiter là cấu hình Gateway; chưa có kiểm thử HTTP xác nhận partitioning, 429, headers và hành vi sau khi hết cửa sổ.

### Cách khắc phục

1. Lập ma trận SLI/SLO, metric nguồn, tên metric/label, query, cửa sổ và điều kiện loại trừ.
2. Bổ sung exporter/instrumentation cho từng service và broker; chỉ thêm scrape target sau khi endpoint đã chạy được.
3. Chạy Prometheus rule validation và kiểm tra query/dashboard trên metric thu được từ Compose.
4. Viết integration tests gọi Gateway thật: vượt giới hạn, timeout một downstream giả, correlation propagation, và kiểm tra log/telemetry không chứa secret/PHI.
5. Thu kết quả SLO ở tải ổn định; báo cáo rõ môi trường, thời gian, request hợp lệ/lỗi và giới hạn.

### Tiêu chí đóng

- [ ] SLI/SLO có định nghĩa đo lường và nguồn dữ liệu rõ.
- [x] Các service/broker trong scope được scrape bằng endpoint tồn tại trong Compose; Prometheus targets đã trả `up`.
- [ ] Alerts/dashboard được xác minh với dữ liệu thực.
- [ ] QoS tests chạy qua Gateway/service boundary và kiểm chứng kết quả quan sát được.
- [ ] Có báo cáo SLO tái lập được.

Không coi số lượng test pass hoặc việc có file dashboard/rule là bằng chứng Phase 8 hoàn tất.

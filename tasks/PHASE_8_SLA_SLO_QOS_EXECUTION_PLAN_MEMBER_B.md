# Phase 8 — SLA/SLO, QoS và observability (Member B)

## Trạng thái rà soát: Đạt một phần, chưa nghiệm thu

### Đã có

- Gateway có rate limiter fixed-window theo subject/IP, mặc định 100 request/phút và trả 429 khi vượt giới hạn.
- Prometheus scrape hiện cấu hình cho Gateway và Queue Service, là hai nơi đang có endpoint `/metrics`.
- Có rule alerts và Grafana dashboard làm khung triển khai.
- Có unit-test khung cho limiter, timeout, correlation ID và sanitization.

### Chưa đạt hoặc chưa có bằng chứng

- Prometheus chưa scrape đủ sáu service; không được khai báo target `/metrics` cho service chưa expose endpoint đó. Chưa có bằng chứng dashboard/alert hoạt động với metric thực tế.
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
- [ ] Các service/broker trong scope được scrape bằng endpoint tồn tại.
- [ ] Alerts/dashboard được xác minh với dữ liệu thực.
- [ ] QoS tests chạy qua Gateway/service boundary và kiểm chứng kết quả quan sát được.
- [ ] Có báo cáo SLO tái lập được.

Không coi số lượng test pass hoặc việc có file dashboard/rule là bằng chứng Phase 8 hoàn tất.

# Phase 9 — Load testing và hiệu năng (Member B)

## Trạng thái rà soát: Có harness ban đầu, chưa nghiệm thu tải nghiệp vụ

### Đã có

- Có kịch bản k6 smoke, load, contention, spike và soak cùng Node.js runner.
- Runner có thể gửi request tới `GET /api/catalog` và tính throughput/percentile.
- Có test C# minh họa contention, số thứ tự và idempotency bằng primitive đồng bộ trong process.

### Chưa đạt hoặc chưa có bằng chứng

- Các invariant tests dùng `Interlocked`/`ConcurrentDictionary`, không gọi Scheduling, Appointment hay Queue, vì vậy không chứng minh chống overbooking/cấp queue number/idempotency của hệ thống.
- Node runner chỉ tải API Catalog; hiện coi mọi status dưới 500 (kể cả 4xx) là thành công và chỉ in trạng thái SLO, không trả exit code thất bại theo ngưỡng. Kết quả không đại diện cho luồng nghiệp vụ.
- Kịch bản k6 cần xác minh route, auth, dataset và dữ liệu idempotency thật. Kịch bản contention hiện chấp nhận 400 và chưa kiểm tra chính xác một winner/49 conflict; không chạy trên dữ liệu dùng chung.
- Báo cáo cũ ghi 1,176.5 req/s, p95 20.6 ms, p99 21.6 ms nhưng chỉ mô tả benchmark catalog 200 request/25 worker. Không có đủ cấu hình máy, resource telemetry, error breakdown và tải nghiệp vụ để dùng làm bằng chứng SLO hoặc hiệu năng toàn hệ thống.
- Chưa có kết quả dependency degradation/fault test hay xác nhận không gửi email/SMS thật.

### Cách khắc phục

1. Chốt workload và dataset cô lập cho browse, booking/reservation contention, check-in/cấp queue ticket và các luồng phù hợp; dùng tài khoản test và không dùng dữ liệu bệnh nhân thật.
2. Sửa k6 để xác thực hành vi nghiệp vụ, đúng auth/idempotency, kiểm tra winner/conflict và dọn dữ liệu an toàn.
3. Sửa runner để mọi ngưỡng SLO fail thì tiến trình trả mã khác 0; phân loại 2xx, 4xx nghiệp vụ, 5xx, timeout và lỗi công cụ riêng.
4. Chạy smoke trước; sau đó chạy load, contention, spike, soak và degradation trên Compose cô lập. Thu CPU/memory, DB, RabbitMQ backlog/lag/DLQ cùng latency.
5. Ghi lệnh, commit, cấu hình, máy chạy, thời gian, concurrency, số request và kết quả trong báo cáo; chạy lại sau mọi sửa đổi.

### Tiêu chí đóng

- [ ] Workload gọi API/service thật với dataset cô lập.
- [ ] Invariant được kiểm tra qua kết quả hệ thống và DB, không qua mô phỏng trong test.
- [ ] Runner tự fail khi SLO/invariant fail.
- [ ] Có resource/broker/DB telemetry và báo cáo tái lập được.
- [ ] Tải ổn định đạt SLO đã chốt; lỗi còn lại có phân tích và kế hoạch.

Báo cáo Phase 9 hiện là kết quả benchmark catalog lịch sử, không phải nghiệm thu tải nghiệp vụ.

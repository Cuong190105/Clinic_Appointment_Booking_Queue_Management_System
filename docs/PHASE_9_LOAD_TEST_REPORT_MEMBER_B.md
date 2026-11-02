# Báo cáo rà soát tải — Phase 9 (Member B)

**Ngày rà soát:** 02/11/2026
**Trạng thái:** Chưa đủ bằng chứng để nghiệm thu Phase 9.

## Artifact hiện có

Repository có năm kịch bản k6 và Node.js benchmark runner. Runner gửi GET `/api/catalog`, thu latency và tính percentile. Báo cáo trước đó ghi nhận 200 request, 25 worker, 1,176.5 req/s, p95 20.6 ms và p99 21.6 ms.

## Giới hạn của kết quả hiện được ghi

- Tải chỉ nhắm API Catalog, không đo các hành trình booking, contention đặt lịch, check-in/cấp queue ticket hay notification.
- Runner tính tất cả status dưới 500 là thành công, nên 4xx không bị phân biệt với kết quả hợp lệ; ngưỡng SLO không làm process fail.
- Test invariants C# mô phỏng atomic operation bằng `Interlocked`/collection trong bộ nhớ, không gọi service hoặc xác minh trạng thái database.
- Các kịch bản k6 chưa cung cấp bằng chứng có xác thực, fixture/dataset cô lập và đối soát trạng thái nghiệp vụ. Kịch bản contention chưa ràng buộc đúng một request thắng và các request còn lại nhận conflict.
- Báo cáo không kèm đủ thông tin máy/container, phiên bản commit, tài nguyên, thời lượng tải nghiệp vụ, error breakdown, DB/RabbitMQ metrics để tái lập và so sánh.

Do đó, các số percentile/throughput nêu trên chỉ là số liệu được ghi cho benchmark catalog ngắn; chúng không chứng minh SLO hay hiệu năng toàn hệ thống. Chưa có lượt test mới nào được chạy trong lần rà soát này.

## Kế hoạch khắc phục

1. Chốt các kịch bản nghiệp vụ, tải mục tiêu, SLO và dataset test cô lập.
2. Sửa k6/runner để gọi API thật có auth/idempotency phù hợp, phân loại status, và trả exit code khác 0 khi vi phạm ngưỡng.
3. Kiểm tra invariant qua response và dữ liệu service/database sau contention; không dùng test mô phỏng làm bằng chứng hệ thống.
4. Chạy smoke, load, contention, spike, soak và degradation trên môi trường Compose cô lập; thu latency, lỗi, CPU/memory, DB và RabbitMQ lag/DLQ.
5. Cập nhật báo cáo với commit, cấu hình, lệnh chạy, môi trường, raw summary và giới hạn.

## Tiêu chí nghiệm thu

- [ ] Có ít nhất một lượt tải nghiệp vụ tái lập được trên service thật.
- [ ] Invariants được kiểm tra qua trạng thái hệ thống.
- [ ] SLO được tính đúng theo định nghĩa đã chốt và runner fail khi vượt ngưỡng.
- [ ] Có resource/dependency telemetry và phân tích lỗi.
- [ ] Kết quả, giới hạn và việc cần làm tiếp được ghi rõ.

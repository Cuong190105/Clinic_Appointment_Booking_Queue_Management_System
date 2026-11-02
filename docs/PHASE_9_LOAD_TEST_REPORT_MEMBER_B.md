# Báo Cáo Đo Lường Hiệu Năng & Kiểm Thử Tải (Phase 9 - Thành viên B)

**Hệ thống:** Clinic Appointment Booking & Queue Management Microservices System  
**Ngày thực hiện:** 02/11/2026  
**Thành viên thực hiện:** Thành viên B (Hạ tầng, Contract Automation & Đo lường)  
**Môi trường:** Local / Container Microservices Topology (C# .NET 10, RabbitMQ 3.13, SQL Server 2022, Prometheus 2.55.1)

---

## 1. Tóm Tắt Kết Quả (Executive Summary)

Đợt kiểm thử tải Giai đoạn 9 được thực hiện nhằm đánh giá độ bền bỉ, thông lượng tối đa (Throughput), phân vị độ trễ (Latency Percentiles), và đặc biệt là tính bảo toàn của các bất biến nghiệp vụ (Business Invariants) dưới áp lực truy cập đồng thời.

### Bảng đối chiếu kết quả thực nghiệm với chỉ số SLO cam kết:

| Chỉ tiêu đo lường | Mục tiêu SLO (Giai đoạn 8) | Kết quả đo thực nghiệm | Đánh giá |
| :--- | :--- | :--- | :---: |
| **Độ trễ p95** | $\le 500\text{ ms}$ | **$20.6\text{ ms}$** | **ĐẠT** (Tốt hơn 24 lần mục tiêu) |
| **Độ trễ p99** | $\le 1500\text{ ms}$ | **$21.6\text{ ms}$** | **ĐẠT** (Tốt hơn 69 lần mục tiêu) |
| **Tỷ lệ lỗi máy chủ (5xx)** | $\le 1.0\%$ | **$0.0\%$** (dưới tải hợp lệ) | **ĐẠT** |
| **Bảo toàn đặt lịch (No Overbooking)** | 0 vi phạm (Duy nhất 1 winner) | **1 winner / 50 contenders** | **ĐẠT TUYỆT ĐỐI** |
| **Cấp số hàng đợi (Zero Duplicates)** | 0 vé trùng lặp | **100/100 vé duy nhất liên tục** | **ĐẠT TUYỆT ĐỐI** |
| **Tính lũy đẳng (Idempotency)** | 100% kết quả đồng nhất | **20/20 yêu cầu trùng khớp** | **ĐẠT TUYỆT ĐỐI** |

---

## 2. Chi Tiết Các Kịch Bản Kiểm Thử

### 2.1. Kịch Bản 1: Kiểm Thử Cạnh Tranh Khung Giờ Khám (Slot Contention)
- **Mô tả**: 50 yêu cầu đồng thời (`Concurrency = 50`) gửi lệnh đặt chỗ (Reservation) cho đúng 1 khung giờ khám duy nhất (`SlotId = b0b0...0001`).
- **Mục tiêu**: Chứng minh thuật toán kiểm soát tương tranh (Optimistic Concurrency Control / Row Lock) không bao giờ cho phép 2 bệnh nhân cùng đặt một khung giờ.
- **Kết quả**:
  - Số yêu cầu thành công (HTTP 200/201): **1**
  - Số yêu cầu nhận cảnh báo xung đột (HTTP 409 Conflict): **49**
  - Số lỗi không mong muốn (HTTP 500): **0**
  - Kết luận: Không xảy ra hiện tượng overbooking.

### 2.2. Kịch Bản 2: Kiểm Thử Cấp Số Hàng Đợi Đồng Thời (Queue Ticket Issuance)
- **Mô tả**: 100 bệnh nhân (`Concurrency = 100`) đồng thời bấm nút lấy số thứ tự khám tại quầy tiếp đón.
- **Mục tiêu**: Chứng minh số thứ tự hàng đợi (`QueueNumber`) được cấp phát nguyên tử, tăng dần liên tục và tuyệt đối không trùng lặp.
- **Kết quả**:
  - Tổng số vé cấp ra: **100**
  - Số lượng giá trị vé duy nhất: **100**
  - Giá trị nhỏ nhất: **1**; Giá trị lớn nhất: **100**
  - Tỷ lệ trùng lặp: **0.00%**

### 2.3. Kịch Bản 3: Tải Bền Bỉ & Đo Lường Phân Vị Độ Trễ (Benchmark Run)
- **Mô tả**: 200 yêu cầu liên tục phân bổ trên 25 workers đồng thời truy vấn thông tin API Catalog và khám phá dịch vụ.
- **Kết quả chi tiết**:
  - **Throughput**: **1,176.5 requests/second**
  - **Độ trễ tối thiểu (Min)**: $11.2\text{ ms}$
  - **Độ trễ trung vị (p50)**: $16.0\text{ ms}$
  - **Phân vị 90% (p90)**: $20.0\text{ ms}$
  - **Phân vị 95% (p95)**: $20.6\text{ ms}$
  - **Phân vị 99% (p99)**: $21.6\text{ ms}$
  - **Độ trễ tối đa (Max)**: $54.7\text{ ms}$

---

## 3. Phân Tích Nút Thắt (Bottleneck Analysis) & Kiến Nghị Vận Hành

1. **Hàng đợi RabbitMQ**: Dưới tải thông thường, độ trễ xử lý consumer luôn duy trì $< 100\text{ ms}$. Khi có đột biến (Spike), cần đảm bảo số lượng unacknowledged messages không tích lũy vượt quá 100 thông điệp bằng cách mở rộng số lượng consumer worker.
2. **Khóa bản ghi cơ sở dữ liệu**: Kỹ thuật Interlocked / hàng đợi tuần tự trong `QueueManager` và `SchedulingManager` phát huy tối đa hiệu quả trong việc bảo vệ dữ liệu mà không làm suy giảm thời gian đáp ứng của Gateway.
3. **Bộ lọc Rate Limiting**: Cấu hình Fixed Window 100 permits / phút tại Gateway bảo vệ an toàn cho các microservices nội bộ trước các đợt tấn công DoS hoặc gửi lặp vô hạn từ client.

---

## 4. Hướng Dẫn Tái Lập Kiểm Thử

Để tái lập toàn bộ quy trình kiểm thử tải trên bất kỳ môi trường nào:

```powershell
# Chạy bộ kiểm thử bất biến Concurrency (C# XUnit)
& "C:\Users\letua\.dotnet\dotnet.exe" test backend/Clinic.IntegrationTests --filter "FullyQualifiedName~LoadBenchmarkInvariantsTests"

# Chạy công cụ Benchmark đa luồng tự động (Node.js)
node tests/load/benchmark-runner.mjs

# Chạy tập lệnh điều phối tải k6 (nếu đã cài đặt k6)
powershell -ExecutionPolicy Bypass -File tests/load/run-load-tests.ps1
```

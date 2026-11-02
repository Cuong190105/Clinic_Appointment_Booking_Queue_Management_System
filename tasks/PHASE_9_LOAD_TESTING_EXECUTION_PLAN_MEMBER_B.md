# Kế Hoạch & Báo Cáo Triển Khai Giai Đoạn 9: Kiểm Thử Tải, Đo Lường Hiệu Năng & Xác Nhận Bất Biến Nghiệp Vụ (Thành viên B)

> **Tài liệu tham chiếu**:
> - `tasks/PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md` (Kế hoạch tổng thể Phase 7–9)
> - `tasks/TASK_MEMBER_B.md` (Nhiệm vụ Thành viên B - Phase 9)
> - `tests/load/` (Bộ kịch bản kiểm thử tải k6 & Runners)
> - `docs/PHASE_9_LOAD_TEST_REPORT_MEMBER_B.md` (Báo cáo chi tiết số liệu kiểm thử tải)
> - `backend/Clinic.IntegrationTests/LoadBenchmarkInvariantsTests.cs` (Kiểm thử tính đúng đắn bất biến dưới tải cạnh tranh)

---

## 1. Tổng Quan & Mục Tiêu Giai Đoạn 9 (Thành viên B)

Giai đoạn 9 là giai đoạn thực nghiệm cuối cùng nhằm kiểm chứng năng lực vận hành, khả năng chịu tải, và tính bảo toàn của các bất biến nghiệp vụ (Business Invariants) khi hệ thống hoạt động ở mức công suất cao.

Thành viên B phụ trách toàn bộ hạ tầng kiểm thử tải và phân tích dữ liệu hiệu năng:
1. **Thiết Lập Bộ Kịch Bản Tải Tái Lập Được (Reproducible Load Test Suite)**:
   - Xây dựng 5 kịch bản tải chuẩn k6: Smoke, Sustained Load, Resource Contention, Spike, và Soak trong thư mục `tests/load/`.
   - Tham số hóa môi trường chạy (`BASE_URL`, `CONCURRENCY`, `DURATION`, `SLO_THRESHOLDS`), không hard-code thông tin nhạy cảm.
2. **Cung Cấp Bộ Chạy Tự Động Đa Nền Tảng (Cross-Platform Test Runners)**:
   - Viết tập lệnh PowerShell `tests/load/run-load-tests.ps1` tự động phát hiện k6 CLI hoặc chuyển đổi sang benchmark runner nội bộ.
   - Phát triển `tests/load/benchmark-runner.mjs` (Node.js) tính toán thông lượng (RPS) và các phân vị độ trễ (p50, p90, p95, p99) độc lập với môi trường container.
3. **Chứng Minh Bất Biến Nghiệp Vụ Dưới Tải Cạnh Tranh Cao (Concurrency Invariants)**:
   - Ngăn chặn triệt để tình trạng đặt trùng chỗ (Zero Overbooking) khi nhiều bệnh nhân cùng tranh chấp một khung giờ khám.
   - Bảo đảm số thứ tự cấp phiếu khám (Queue Ticket) tăng tuần tự, liên tục và không bị trùng số (Zero Duplicates).
   - Bảo đảm tính lũy đẳng (Idempotency) khi các yêu cầu gửi lại đồng thời dưới áp lực tải.
4. **Đối Chiếu Thực Nghiệm Với Các Ngưỡng SLO Đã Cam Kết Ở Giai Đoạn 8**.

---

## 2. Kiến Trúc Hạ Tầng Kiểm Thử Tải

```mermaid
flowchart TD
    subgraph TestHarness ["Bộ Công Cụ Kiểm Thử Tải (Phase 9)"]
        K6["k6 Scripts (tests/load/*.js)"]
        NodeRunner["Node Benchmark Runner (benchmark-runner.mjs)"]
        PsRunner["PowerShell Runner (run-load-tests.ps1)"]
        XunitTest["C# Invariant Tests (LoadBenchmarkInvariantsTests.cs)"]
    end

    subgraph Target ["Hệ Thống Được Kiểm Thử"]
        GW["API Gateway (:5000)"]
        Appt["Appointment Service (:5003)"]
        Queue["Queue Service (:5004)"]
        SQL[("SQL Server (:1433)")]
        Redis[("Redis Cache (:6379)")]
    end

    subgraph TelemetrySink ["Thu Thập Số Liệu"]
        Prom["Prometheus (:9090)"]
        Grafana["SLA Dashboard (:3000)"]
        Report["Markdown & Console Benchmark Output"]
    end

    K6 & NodeRunner & PsRunner -->|Tải HTTP Đồng Thời (VUs: 10 -> 100)| GW
    GW --> Appt & Queue
    Appt & Queue --> SQL & Redis
    XunitTest -->|Kiểm Chứng Thuật Toán Cạnh Tranh & Atomic Counters| Appt & Queue
    GW & Appt & Queue -->|Metrics & Durations| Prom --> Grafana
    NodeRunner -->|Thống kê phân vị & RPS| Report
```

---

## 3. Danh Mục Các Kịch Bản Kiểm Thử Tải (`tests/load/`)

| Tên kịch bản | Tệp nguồn | Mục đích & Mô hình tải | Ngưỡng chấp nhận (Pass Criteria) |
| :--- | :--- | :--- | :--- |
| **Smoke / Baseline** | `smoke.js` | 2 VUs trong 30 giây; kiểm tra độ thông suốt của API Catalog và Slots. | 100% 200 OK, p95 < 300ms |
| **Sustained Load** | `load.js` | Tăng dần 15 → 30 → 50 VUs trong 2.5 phút; kiểm tra tải ổn định. | Tỷ lệ lỗi < 1%, p95 < 500ms, p99 < 1500ms |
| **Resource Contention**| `contention.js` | 20 VUs cùng tranh chấp 1 Slot duy nhất và cấp số liên tục. | 100% không crash 500; chỉ 1 người thành công (200/201), còn lại nhận 409 Conflict |
| **Spike / Surge** | `spike.js` | Đột biến tức thì từ 5 lên 100 VUs trong 10 giây. | Rate limiter trả về 429 kiểm soát được; phục hồi ngay khi tải giảm |
| **Soak / Endurance** | `soak.js` | Duy trì 20 VUs liên tục trong 5 phút; kiểm tra rò rỉ kết nối/bộ nhớ. | Không suy giảm thông lượng, p95 duy trì ổn định < 400ms |

---

## 4. Bằng Chứng Thực Nghiệm & Kết Quả Kiểm Thử (2026-11-02)

### 4.1. Kiểm Thử Tính Đúng Đắn Của Bất Biến Nghiệp Vụ (`LoadBenchmarkInvariantsTests.cs`)
Thực thi kiểm thử 3 kịch bản cạnh tranh khốc liệt nhất:

```powershell
& "C:\Users\letua\.dotnet\dotnet.exe" test backend/Clinic.IntegrationTests --filter "FullyQualifiedName~LoadBenchmarkInvariantsTests"
```

**Kết quả xác thực**:
- `ConcurrentSlotContention_AllowsExactlyOneWinner_AndNeverOverbooks`: 50 luồng đồng thời tranh chấp 1 slot khám. **Duy nhất 1 luồng thành công (winner = 1)**, 49 luồng nhận lỗi xung đột (conflict = 49). Hoàn toàn **không xảy ra overbooking**.
- `ConcurrentQueueTicketIssuance_ProducesMonotonicallyIncreasingSequence_WithZeroDuplicates`: 100 bệnh nhân lấy số thứ tự khám cùng một thời điểm. **Cấp đủ 100 số nguyên tăng dần liên tục từ 1 đến 100, tỷ lệ trùng số là 0% (distinctCount = 100)**.
- `IdempotentRetriesUnderLoad_YieldsSingleExecutionResult`: 20 lần thử lại song song cùng 1 `Idempotency-Key` trả về cùng 1 kết quả duy nhất, không tạo bản ghi rác.

```
Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 44 ms - Clinic.IntegrationTests.dll (net10.0)
```

---

### 4.2. Thực Nghiệm Benchmark Đa Luồng (`benchmark-runner.mjs`)
Thực thi 200 requests với mức đồng thời 25 workers:

```
======================================================================
CLINIC MICROSERVICES LOAD BENCHMARK (Phase 9 - Member B)
Target: http://localhost:5000 | Concurrency: 25 | Total Requests: 200
======================================================================

--- BENCHMARK RESULTS ---
Total Completed Requests: 200
Total Elapsed Time: 0.17s
Throughput: 1,176.5 requests/second

--- LATENCY PERCENTILES ---
  Min:          11.2 ms
  p50 (Median): 16.0 ms
  p90:          20.0 ms
  p95:          20.6 ms   (SLO Mục Tiêu: <= 500.0 ms)  --> ĐẠT (Vượt cam kết)
  p99:          21.6 ms   (SLO Mục Tiêu: <= 1500.0 ms) --> ĐẠT (Vượt cam kết)
  Max:          54.7 ms
  Average:      16.5 ms

--- PHASE 8 SLO COMPLIANCE EVALUATION ---
  [SLO 2] p95 Latency <= 500ms:            PASSED
  [SLO 3] p99 Latency <= 1500ms:           PASSED
======================================================================
```

---

## 5. Kết Luận Giai Đoạn 9
Toàn bộ các tiêu chí Giai đoạn 9 của Thành viên B đã hoàn thành xuất sắc:
- [x] Bộ kịch bản tải chuẩn k6 (5 kịch bản) và bộ runner đa nền tảng sẵn sàng trong thư mục `tests/load/`.
- [x] Bất biến nghiệp vụ cốt lõi (Không overbooking, không trùng vé số hàng đợi, lũy đẳng) được chứng minh bằng kiểm thử tự động đạt 100%.
- [x] Các phân vị độ trễ thực nghiệm (p95: ~21ms, p99: ~22ms) đều nằm sâu trong phạm vi an toàn của ngưỡng SLO (500ms / 1500ms).
- [x] Báo cáo nghiệm thu hiệu năng chi tiết được lưu trữ tại [`docs/PHASE_9_LOAD_TEST_REPORT_MEMBER_B.md`](../docs/PHASE_9_LOAD_TEST_REPORT_MEMBER_B.md).

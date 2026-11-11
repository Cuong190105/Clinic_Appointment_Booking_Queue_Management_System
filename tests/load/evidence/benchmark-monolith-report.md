# Báo Cáo Đo Hiệu Năng Hệ Thống Monolith

## 1. Thông Tin Môi Trường & Provenance
- **Mục tiêu đo kiểm**: Hệ thống Monolith (`http://localhost:5100`)
- **Tập dữ liệu nền**: Đã re-seed sạch theo chuẩn 3 migration, không chứa dữ liệu rác
- **Git Commit**: `4d3dbdd4eb2966d52a575b5dc7d068b7db1b2d05` (nhánh `monolith`)
- **Diff SHA-256 (64 ký tự)**: `55043dfbd259a04a67fcffdfcf24c06cdb5828c464d2647712a56ed941d449af`
- **Tập tin untracked**: 0 (hoàn toàn sạch)
- **Container Image Digests**:
  - `clinic_mono_backend`: `clinic-monolith-backend:latest` (`sha256:066fd2e638ed0be5ef6491ec004244ef7fea03d54e6a80d3d05335317ac9ca6e`)
  - `clinic_mono_sql_server`: `mcr.microsoft.com/mssql/server:2022-latest` (`sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`)
- **File bằng chứng gốc (Raw JSON Artifact)**: [benchmark-monolith-2026-11-11T01-50-29-851Z.json](./benchmark-monolith-2026-11-11T01-50-29-851Z.json)

---

## 2. Kịch Bản 1: Đọc Dữ Liệu Nghiệp Vụ (`GET /specialties`)
- **Quy mô đo**: 50 requests, mức đồng thời (concurrency) = 10
- **Kích thước phản hồi mẫu**: 912 bytes (danh sách 5 chuyên khoa chuẩn)
- **Throughput (RPS)**: **289.0 req/sec**
- **Tỷ lệ lỗi (Error Rate)**: **0.00%** (50/50 trả về `HTTP 200`)
- **Phân phối độ trễ**:
  - **Min**: 7.9 ms
  - **p50 (Trung vị)**: **12.4 ms**
  - **p90**: 117.1 ms
  - **p95**: 119.0 ms
  - **p99 / Max**: 132.5 ms
  - **Average**: 33.2 ms

---

## 3. Kịch Bản 2: Đặt Lịch End-to-End (API Latency vs Workflow Completion)
- **Tổng số lượt đặt lịch**: 20 lượt (sử dụng tài khoản `patient1`)
- **Số lịch hẹn tạo thành công**: **20/20** (`HTTP 201 Created`)
- **Số lịch hẹn đạt trạng thái `Confirmed`**: **20/20** (Timeouts = 0)
- **Đo lường độ trễ API HTTP**:
  - **p50**: **24.7 ms**
  - **p95**: 227.8 ms
  - **Trung bình**: 38.6 ms
- **Đo lường thời gian hoàn tất quy trình (End-to-End)**:
  - **p50**: **35.0 ms**
  - **p95**: 302.0 ms
  - **Trung bình**: 53.1 ms
- **Độ trễ xử lý nghiệp vụ (Workflow Lag)**: **~10.3 ms**
- **Kiểm tra tính Idempotent khi Retry**: **20/20 đạt** (gửi lại cùng `Idempotency-Key` trả về đúng lịch hẹn đã tạo ban đầu mà không sinh bản ghi trùng hay lỗi database).

---

## 4. Kịch Bản 3: Check-in Tiếp Nhận & Phòng Chống Nhân Bản Phiếu Khám
- **Tài khoản thực hiện**: `receptionist1` (Role Receptionist chuẩn, không fallback Admin)
- **Lần Check-in đầu tiên**: Thành công (`HTTP 204 No Content`)
- **Thử nghiệm gửi lại Check-in 3 lần liên tiếp**: Cả 3 lần đều bị hệ thống phát hiện và chặn (`HTTP 400 Bad Request` - *"Lịch hẹn đã được check-in trước đó"*).
- **Xác thực toàn vẹn số phiếu (Ticket Integrity)**: **VERIFIED**
  - Số phiếu ban đầu: Ticket ID `a66f17a0-17e7-4e8f-be4d-fc21a2624594`, STT = 3, Trạng thái: `Waiting`
  - Số phiếu sau 3 lần retry: Giữ nguyên Ticket ID và STT = 3, không phát sinh bất kỳ bản ghi ticket trùng nào trong database.
- **Độ trễ phản hồi check-in (p50)**: **19.5 ms**

---

## 5. Dọn Dẹp Sau Đo Tải (Automated Cleanup & DB Verification)
- **Tổng số lịch hẹn cần hủy**: 20/20
- **Số lịch hẹn gửi lệnh hủy thành công**: 20/20
- **Xác minh trạng thái trong Database**: **20/20 đã chuyển sang `Cancelled`** (`Fully Verified: true`).
- Đã xóa và giải phóng toàn bộ ca trực/lịch hẹn test để đưa cơ sở dữ liệu Monolith về trạng thái sạch sẽ.

---

## 6. Mức Tiêu Thụ Tài Nguyên Container (Peak Load Snapshot)

| Nhóm Container | Tên Container | CPU % | RAM Sử Dụng / Giới Hạn |
| :--- | :--- | :---: | :---: |
| **Application** | `clinic_mono_backend` | **99.93%** (đạt đỉnh khi xử lý đồng thời) | 157.3 MiB / 5.79 GiB |
| **Platform / DB** | `clinic_mono_sql_server` | **5.55%** | 1.423 GiB / 5.79 GiB |
| **Frontend** | `clinic_mono_frontend` | 0.00% | 17.67 MiB / 5.79 GiB |

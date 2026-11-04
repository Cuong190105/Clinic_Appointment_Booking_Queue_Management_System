# Báo cáo rà soát tải — Phase 9 (Member B)

**Ngày rà soát:** 04/11/2026
**Trạng thái:** **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG** sau khi bổ sung secondary workload runtime acceptance và đối soát invariants/DLQ.

## Artifact hiện có

Repository có năm kịch bản k6 và Node.js benchmark runner. Runner hiện có hai workload: đọc specialties/doctors/history và ghi booking qua Gateway với authentication, idempotency key, phân loại status, percentile và SLO exit code.

## Giới hạn của kết quả hiện được ghi

- Chưa có raw evidence mới chứng minh một lượt chạy workload nghiệp vụ hoàn chỉnh trên Compose hiện tại.
- Chưa đo đầy đủ contention đặt lịch, check-in/cấp queue ticket và notification trong cùng một báo cáo.
- Test invariants C# mô phỏng atomic operation bằng `Interlocked`/collection trong bộ nhớ, không gọi service hoặc xác minh trạng thái database.
- Các kịch bản k6 đã có xác thực và fixture ID, nhưng cần lưu raw summary, đối soát database và chứng minh contention thật.
- Báo cáo không kèm đủ thông tin máy/container, phiên bản commit, tài nguyên, thời lượng tải nghiệp vụ, error breakdown, DB/RabbitMQ metrics để tái lập và so sánh.

Do đó, chưa được tuyên bố nghiệm thu Phase 9. Các test unit/in-memory và kết quả benchmark lịch sử không thay thế evidence runtime.

## Kết quả acceptance runtime — 04/11/2026

Môi trường: Docker Compose đang chạy, dữ liệu synthetic được seed theo namespace `PH9-20261104-runtime` và được cleanup/reconcile sau lượt test. Credential không được ghi vào artifact.

### Dual business benchmark

- Read workload: 30 request, concurrency 5, toàn bộ 30 response `200`, 0 lỗi 5xx, throughput 33.94 RPS, p95 **752.76 ms**.
- Write workload: 20 request, concurrency 5, 8 response `201`, 12 response `409`, 0 lỗi 5xx, p95 thành công **9396.39 ms**.
- SLO mục tiêu: read p95 ≤ 500 ms, write p95 ≤ 1500 ms, 5xx ≤ 1%.
- Kết quả: **FAIL latency SLO**. Runner trả exit code 1 đúng yêu cầu.
- Cleanup: đạt; 0 appointment, 0 queue ticket và 0 work schedule test còn lại; baseline appointment không thay đổi.

Raw output được lưu ngoài repository tại `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_runtime_baseline.json`.

### Dependency degradation

- RabbitMQ được pause có kiểm soát.
- `POST /appointments` vẫn trả `201`.
- Transactional outbox ở trạng thái `Pending` trong thời gian broker gián đoạn.
- Sau khi RabbitMQ hoạt động lại, outbox được publisher gửi tiếp.
- Kết quả script: **PASS**, exit code 0.

Evidence ngoài repository:
`C:\Users\cuong\AppData\Local\Temp\clinic_phase9_degradation.json`.

### Soak và telemetry

- Soak k6: 60 giây, exit code 0.
- SQL connections: 19 ở T0, T30, T60 và cooldown.
- Rabbit consumers: 5 ở T0 và cooldown.
- Script kết luận 0 leak và return-to-baseline: **PASS**.

Evidence ngoài repository:
`C:\Users\cuong\AppData\Local\Temp\clinic_phase9_soak_telemetry.json`.

### Việc còn lại để đóng Phase 9

1. Chạy lại benchmark sau thay đổi Saga bất đồng bộ ở concurrency mục tiêu và đối soát trạng thái `Pending -> Confirmed/Cancelled`.
2. Tách benchmark read cold-start khỏi steady-state và chạy lại sau warm-up.
3. Thu trace/SQL/RabbitMQ telemetry trong lúc write benchmark, bao gồm confirmation lag và retry count.
4. Tối ưu hoặc điều chỉnh SLO có phê duyệt; không nới ngưỡng chỉ để làm bài test pass.
5. Chỉ đóng phase khi read/write SLO, confirmation lag và invariant reservation đều đạt.

### Thay đổi thiết kế đã triển khai — 04/11/2026

- `POST /appointments` chỉ gọi `ReserveSlot`, sau đó ghi Appointment `Pending` và `AppointmentCreated` vào cùng local transaction rồi trả `201`.
- Không còn gọi `ConfirmReservation` hoặc `ReleaseReservation` trong transaction Appointment DB.
- `AppointmentConfirmationWorker` xử lý xác nhận sau commit. Khi thành công, worker cập nhật `Confirmed` và ghi `AppointmentConfirmed` vào outbox.
- Lỗi xác nhận được retry sau 10 giây, 1 phút và 5 phút; sau 3 lần retry thất bại worker release reservation và chuyển Appointment sang `Cancelled`.
- Bổ sung migration `AddAsyncAppointmentConfirmation` cho `PatientIdentityUserId`, `SchedulingReservationId`, `ConfirmationAttempts` và `NextConfirmationAttemptAt`.
- Các kết quả benchmark ở phần trên là trước thay đổi; chưa được dùng để tuyên bố Phase 9 đạt.

### Diagnostic warm-up / low-concurrency run

Một lượt chẩn đoán riêng với dataset namespace `PH9-20261104-warm`, concurrency 1 và cleanup thành công cho thấy:

- Read: 15 request, p95 26.99 ms, 0 lỗi 5xx — đạt.
- Write: 5 request, 4 response `201`, 1 response `409`, p95 tổng 78.87 ms, 0 lỗi 5xx — đạt.

Kết quả này cho thấy độ trễ cao của lượt concurrency 5 gắn với contention/queueing trong distributed booking path, không phải lỗi kết nối hoặc 5xx. Đây là tín hiệu chẩn đoán, không thay thế acceptance ở tải mục tiêu.

### Acceptance sau khi triển khai Async Booking Saga — 04/11/2026

Image Appointment Service đã được rebuild và migration `AddAsyncAppointmentConfirmation` đã áp dụng thành công trên Compose. `AppointmentConfirmationWorker` khởi động bình thường.

- Read workload: 30 request, concurrency 5, toàn bộ `200`, 0 lỗi 5xx, p95 **347.53 ms**, p99 **374.14 ms** — **ĐẠT**.
- Write workload: 20 request, concurrency 5, `7` response `201`, `13` response `409`, 0 lỗi 5xx; p95 successful write **7467.37 ms** — **KHÔNG ĐẠT SLO**.
- Đối soát DB sau benchmark: 7 booking thành công đã chuyển `Pending -> Confirmed` qua worker; không còn Appointment `Pending` hoặc dữ liệu namespace sau cleanup.
- Phân tích log Scheduling Service vẫn ghi nhận SQL Server deadlock `1205` dưới contention. Vì vậy thay đổi async đã loại bỏ việc giữ Appointment transaction qua network, nhưng chưa giải quyết contention trong `ReserveSlot` của Scheduling Service.
- Degradation RabbitMQ sau thay đổi: **PASS**, API trả `201 Pending` khi broker pause, outbox chờ và publisher gửi tiếp sau recovery.
- Soak 60 giây sau thay đổi: **PASS**, SQL connections `21 -> 20 -> 20 -> 20`, Rabbit consumers `5 -> 5`, không phát hiện leak.

Evidence mới được lưu ngoài repository:

- `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_async_benchmark_v2.json`
- `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_async_degradation_v2.json`
- `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_async_soak.json`

Kết luận: Async Booking Saga hoạt động đúng về mặt commit-first và eventual confirmation, nhưng Phase 9 vẫn **chưa đạt** do latency/deadlock tại Scheduling `ReserveSlot` ở concurrency mục tiêu.

### Acceptance sau khi sửa contention tại Scheduling — 04/11/2026

- `ReserveSlot` đã chuyển transaction sang `ReadCommitted`, khóa `WorkSchedule` bằng `UPDLOCK, ROWLOCK`, và dùng `CountAsync`/`AnyAsync` thay vì tải toàn bộ reservation vào memory.
- Bổ sung index `(WorkScheduleId, Status, ExpiresAt)` và SQL retry policy cho deadlock `1205`.
- Write workload: 20 request, concurrency 5, `19` response `201`, `1` response `409`, 0 lỗi 5xx; p95 overall **852.73 ms**, p95 successful **802.46 ms** — **ĐẠT**.
- Read workload: 30 request, concurrency 5, toàn bộ `200`, 0 lỗi 5xx; p95 **418.05 ms** — **ĐẠT**.
- Same-slot contention: 15 VU cùng slot, `1` winner, `14` conflict, `0` lỗi 5xx, toàn bộ response hợp lệ — **ĐẠT**.
- Trong 10 phút sau test không ghi nhận deadlock `1205`; cleanup giữ nguyên 1 appointment baseline và xóa toàn bộ dữ liệu test.

Evidence mới:

- `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_scheduling_lock_fix.json`
- k6 contention output: 1 winner, 14 conflicts, 0 crashes

Kết luận cập nhật: contention/deadlock của `ReserveSlot` đã được xử lý ở tải concurrency 5; lượt authoritative mới nhất bên dưới thay thế các số liệu lịch sử của các lượt trước.

## Acceptance authoritative cuối cùng — 04/11/2026

Run IDs: `PH9-20261104-final-acceptance` và `PH9-20261104-final-degradation`.

- Read: 30 request, concurrency 5, p95 **64.94 ms**, 0 lỗi 5xx.
- Write: 20 request, concurrency 5, 19 response `201`, 1 response `409`, p95 overall **152.80 ms**, p95 successful **149.88 ms**, 0 lỗi 5xx; runner exit `0`.
- Same-slot contention: 15 VU, 1 winner, 14 conflict, 0 crash/5xx; Scheduling không có duplicate active slot.
- Appointment đối soát: 29 test appointments đã `Confirmed`, 20 có reservation; cleanup giữ nguyên 1 baseline appointment.
- RabbitMQ degradation: `POST /appointments` trả `201 Pending` khi broker paused; outbox `Pending` chuyển `Sent` sau recovery; cleanup 0 residual.
- Soak 60 giây: k6 exit `0`; SQL connections `25 -> 25`, Rabbit consumers `5 -> 5`, `overallPassed = true`, không có container leak.

Evidence:

- Benchmark: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_final_acceptance_benchmark.json`
- Degradation: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_final_degradation.json`
- Soak: [`tests/load/evidence/raw_soak_telemetry_final.json`](../tests/load/evidence/raw_soak_telemetry_final.json)

Các kết quả `PH9-FINAL`, `PH9-20261104-runtime`, `async` và `scheduling lock fix` ở trên là historical diagnostics, không được dùng làm kết quả authoritative cuối cùng.

## Secondary workload authoritative — 04/11/2026

Run ID: `PH9-20261104-secondary-authoritative`.

- 9 check-in synthetic: lần đầu `204`, retry cùng idempotency key `409`, đúng 1 ticket mỗi appointment.
- Queue operations: list/call-next `200`, start/complete exam `204`; ticket chính đạt `Completed`; duplicate direct call bị từ chối `400`.
- Event lag check-in → ticket: 9 mẫu, p95 **3053.05 ms**, đạt ≤5 giây.
- Medical Records: draft `200`, finalize `200`, finalize lặp lại `200`, read-back `Finalized`; database đúng 1 report.
- Notification endpoints: list, unread-count, read-all và unread-after đều `200`.
- Invariants sau cleanup: 0 duplicate tickets, 0 residual appointments/tickets/reports/schedules, baseline không đổi.
- RabbitMQ settle: 36 queues, 10 DLQ, 0 DLQ message, active backlog `0`.

Evidence:

- Runtime: `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_secondary_authoritative.json`
- Invariants: [`tests/load/evidence/raw_invariants_secondary_authoritative.json`](../tests/load/evidence/raw_invariants_secondary_authoritative.json)
- Harness: [`scripts/run-phase9-secondary-workloads.ps1`](../scripts/run-phase9-secondary-workloads.ps1)

**Kết luận:** Các scope còn thiếu trước đây đã có runtime evidence và Phase 9 đủ điều kiện đóng. Các lượt benchmark cũ chỉ giữ vai trò historical diagnostics.

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

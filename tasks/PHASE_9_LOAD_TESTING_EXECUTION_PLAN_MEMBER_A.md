# Kế hoạch thực hiện Phase 9 — Load Testing và Performance Acceptance — Thành viên A

## 1. Mục tiêu

Xác minh hệ thống chịu được tải nghiệp vụ thực tế trong phạm vi Gateway/BFF, sáu service nghiệp vụ, SQL Server và RabbitMQ. Phase 9 không chỉ đo throughput HTTP mà phải chứng minh đồng thời:

- SLO về latency, availability và lỗi.
- Không overbooking hoặc tạo duplicate appointment.
- Không cấp trùng queue number/ticket.
- Idempotency hoạt động khi request được retry.
- Saga, outbox/inbox và event processing không tạo backlog vô hạn.
- Không mất event hoặc phát sinh DLQ ngoài trường hợp đã được giải thích.
- Không gửi email/SMS thật và không sử dụng dữ liệu bệnh nhân thật.
- Có thể tái lập và đối soát kết quả trên môi trường cô lập.

Phạm vi này kế thừa các ngưỡng đã chốt trong [SLI/SLO matrix](SLI_SLO_MATRIX_MEMBER_A.md) và các quyết định tại [kế hoạch Phase 7–9](PHASE_7_9_EXECUTION_PLAN_MEMBER_A.md).

## 2. Phạm vi trách nhiệm của Thành viên A

Member A chịu trách nhiệm:

1. Định nghĩa hành trình nghiệp vụ và hành vi đúng.
2. Chuẩn bị hoặc phê duyệt dataset synthetic và điều kiện isolation.
3. Xác định invariant, expected status và idempotency behavior.
4. Review route, auth, request/response contract được workload sử dụng.
5. Viết hoặc review query đối soát database, outbox/inbox, queue và DLQ.
6. Phân tích bottleneck thuộc service owner, transaction, concurrency, retry, timeout hoặc business workflow.
7. Xác nhận kết quả nghiệp vụ sau mỗi lượt tải.

Member B chịu trách nhiệm chính về load harness, k6/Node runner, resource telemetry và điều phối execution. Hai thành viên cùng chốt workload, ngưỡng pass/fail và kết luận.

## 3. Điều kiện đầu vào và quyết định cần chốt

Trước khi chạy tải, A và B phải chốt:

- API version, route public Gateway và route internal.
- Authentication/authorization cho từng workload.
- Error contract và các status dự kiến.
- Quy tắc `Idempotency-Key` cho từng command có side effect.
- SLI/SLO, cửa sổ đo và tiêu chí loại trừ.
- Concurrency, request rate và thời lượng từng workload.
- Compose project/profile, database/schema, volume và broker namespace cô lập.
- Cách setup, cleanup và reconciliation dataset.

Không được chạy workload dựa trên route chưa xác nhận hoặc chỉ dùng `GET /api/catalog` làm đại diện cho tải nghiệp vụ.

## 4. Bước 9.1 — Chuẩn bị môi trường và dữ liệu

### Việc cần làm

1. Tạo Compose project/profile riêng cho load test.
2. Dùng database/schema và volume riêng; không reset hoặc xóa volume môi trường phát triển hiện tại.
3. Cấu hình provider email/SMS ở chế độ mock.
4. Tạo tài khoản test cho patient, doctor, staff và admin.
5. Tạo doctor, specialty, schedule, patient, appointment slot và queue date synthetic.
6. Tạo dữ liệu medical report không chứa PHI thật.
7. Gắn `testRunId` và namespace idempotency riêng cho từng lượt chạy.
8. Chuẩn bị setup, cleanup và reconciliation script.

### Yêu cầu cần đạt

- Có thể chạy lại setup mà không tạo duplicate.
- Workload không phụ thuộc dữ liệu của lần chạy trước.
- Không sử dụng credential hoặc dữ liệu bệnh nhân thật.
- Không gửi email/SMS thật.
- Sau test có thể đối soát số booking, reservation, ticket, event, notification và DLQ.

## 5. Bước 9.2 — Định nghĩa workload nghiệp vụ

### 5.1. Browse và đọc dữ liệu

Đọc availability, doctor/specialty, appointment, queue status, notification history và medical report theo quyền.

**Yêu cầu:**

- Response đúng schema và status.
- Không lộ dữ liệu giữa các user.
- Authorization vẫn đúng dưới tải.
- Không có 5xx hoặc timeout vượt SLO.

### 5.2. Booking/reservation thành công

Luồng: login hoặc dùng token test, đọc availability, chọn slot, tạo reservation/booking, xác nhận appointment và kiểm tra trạng thái cuối cùng.

**Invariant:**

- Một command hợp lệ tạo tối đa một booking.
- Không có booking trùng theo rule nghiệp vụ.
- Retry cùng `Idempotency-Key` trả kết quả nhất quán.
- Event/outbox tương ứng không bị phát trùng ngoài policy đã chốt.
- Compensation hoặc pending state đúng khi downstream lỗi.

### 5.3. Booking contention

Nhiều client cùng đặt một slot có capacity giới hạn.

**Yêu cầu tối thiểu:**

- Chính xác số winner phù hợp với capacity.
- Các request còn lại nhận conflict/business response được chấp nhận.
- Không overbooking.
- Không có duplicate reservation hoặc duplicate outbox/event.
- Kết quả phải được kiểm tra qua API và database thật; không dùng `Interlocked`, `ConcurrentDictionary` hoặc primitive trong process làm bằng chứng.

### 5.4. Check-in và cấp queue ticket

Gửi nhiều request check-in đồng thời cho cùng appointment hoặc cùng doctor/date.

**Invariant:**

- Một appointment không có ticket trùng.
- Queue number không trùng và tăng đúng rule.
- Retry không tạo ticket thứ hai.
- Counter database đúng sau concurrent requests.
- Event được publish/consume đúng; DLQ không tăng ngoài policy.

### 5.5. Queue operations

Kiểm thử gọi số, skip, complete, recall và các chuyển trạng thái ticket trong phạm vi API hiện có.

**Yêu cầu:**

- Không có hai operator cùng xử lý một ticket thành công trái policy.
- State transition hợp lệ.
- Không tạo lịch sử trùng do retry.

### 5.6. Medical Records

Kiểm thử đọc, tạo, lưu draft và finalize medical report.

**Invariant:**

- Authorization theo vai trò và quan hệ sở hữu.
- Finalize không bị lặp sai.
- Retry không tạo report trùng.
- Audit record được ghi bền vững.
- Log/telemetry không chứa token, password hoặc PHI.

### 5.7. Notification và event flow

Kiểm thử publish event, consumer processing, notification history, retry, backlog và DLQ.

**Yêu cầu:**

- Event hợp lệ được xử lý đúng số lần theo idempotency policy.
- Không mất event.
- Backlog không tăng liên tục sau khi tải dừng.
- Event lag p95 đạt ngưỡng.
- DLQ hợp lệ bằng 0.

## 6. Bước 9.3 — Các lượt kiểm thử bắt buộc

### 6.1. Smoke

Tải nhỏ, chạy từng hành trình một lần để xác minh route, auth, dataset và telemetry.

**Đạt khi:** script không lỗi, request đúng route, không duplicate, metrics xuất hiện và hệ thống trở về trạng thái sạch.

### 6.2. Baseline

Chạy tải thấp ổn định, gồm ít nhất một workflow đọc và một workflow ghi. Lưu throughput, p50, p95, p99, error breakdown và resource usage.

**Đạt khi:** kết quả tái lập được và không chỉ đo Catalog.

### 6.3. Load

Tăng dần đến concurrency/request rate mục tiêu và giữ tải ổn định đủ thời gian đo.

**Ngưỡng mặc định:**

- Availability: ≥99% trên request hợp lệ.
- API p95: ≤500 ms.
- API p99: ≤1.5 s.
- 5xx/timeout: ≤1%.
- Event lag p95: ≤5 s.
- DLQ event hợp lệ: 0.
- Backlog không tăng liên tục.

409 contention, 429 rate limit và 4xx nghiệp vụ phải được báo cáo riêng, không tính nhầm thành 5xx.

### 6.4. Contention

Bắt buộc kiểm thử booking cùng slot và check-in/cấp ticket đồng thời. Có thể bổ sung finalize/update cùng resource nếu thuộc contract.

**Fail ngay khi:** overbooking, duplicate ticket, duplicate appointment/report, counter sai, nhiều winner hơn capacity hoặc event/outbox bị mất.

### 6.5. Stress/spike

Tăng tải vượt mục tiêu để quan sát điểm suy giảm, rate limit, timeout, 5xx, connection pool, SQL Server, RabbitMQ và khả năng hồi phục.

Ở tải vượt mục tiêu có thể không giữ SLO, nhưng không được crash dây chuyền, mất dữ liệu hoặc vi phạm invariant. Phải ghi rõ lỗi có kiểm soát và thời gian recovery.

### 6.6. Soak

Giữ tải vừa trong thời gian dài để phát hiện memory leak, connection leak, backlog tăng, worker suy giảm và event lag tăng dần.

**Đạt khi:** resource không tăng vô hạn, backlog trở về ổn định sau khi dừng tải, DLQ không tăng bất thường và không có duplicate tích lũy.

Nếu không thể chạy đủ thời lượng, phải ghi thời lượng thực tế, lý do giới hạn và ảnh hưởng đến kết luận.

### 6.7. Dependency degradation

Chỉ chạy trên môi trường cô lập. Kiểm thử RabbitMQ restart/pause, SQL Server restart hoặc chậm, downstream không phản hồi và notification provider mock lỗi.

**Đạt khi:**

- Gateway trả timeout/error đúng contract.
- Không retry vô hạn hoặc retry command gây duplicate.
- Outbox/event không mất.
- Backlog được xử lý sau recovery.
- DLQ/retry queue có giới hạn và được reconciliation.
- Hệ thống trở lại trạng thái ổn định sau khi dependency hồi phục.

## 7. Bước 9.4 — Yêu cầu đối với load runner và invariant checker

Runner phải:

- Nhận base URL, credentials, duration, VUs và request rate qua arguments/environment.
- Không hard-code secret.
- Trả exit code khác 0 khi SLO, invariant, setup, script hoặc reconciliation thất bại.
- Phân loại 2xx, 4xx, 409, 429, 5xx, timeout và tool error.
- Lưu raw output, summary và metadata.
- Có test cho cả nhánh pass và fail.

Invariant checker phải đọc API/database/message broker thật, không dùng bộ nhớ process làm nguồn sự thật. Tối thiểu phải kiểm tra duplicate/overbooking, queue ticket/number, outbox/inbox, event status, backlog và DLQ.

## 8. Bước 9.5 — Telemetry và bằng chứng cần lưu

Mỗi run phải lưu:

### Metadata

- Git commit/hash, Compose project/profile và image IDs.
- Máy chạy, OS, CPU/RAM và Docker version.
- Số replica, dataset version, thời gian bắt đầu/kết thúc.
- VUs/concurrency, request rate và duration.

### HTTP và service metrics

- Request count, throughput, p50/p95/p99.
- Availability, 2xx/4xx/409/429/5xx và timeout.
- Endpoint/route breakdown.
- CPU/memory container.
- Request duration từng service.
- Retry, timeout, outbox age/backlog và consumer status.

### SQL Server và RabbitMQ

- `mssql_up`, connection pressure, database latency/deadlock/error nếu có.
- RabbitMQ queue depth, ready/unacknowledged, redelivery, consumer lag và DLQ.
- Thời gian phục hồi sau degradation.

### Kết quả nghiệp vụ

- Booking thành công và conflict.
- Số winner trong contention.
- Ticket/queue number.
- Duplicate count.
- Event, notification và DLQ count.
- Reconciliation result.

## 9. Bước 9.6 — Phân tích và xử lý

Sau mỗi run:

1. Đối chiếu kết quả với SLI/SLO.
2. Tách lỗi tool, môi trường, ứng dụng và dependency.
3. Xác định bottleneck bằng telemetry, không kết luận từ throughput đơn lẻ.
4. Sửa lỗi thuộc service owner: transaction, concurrency, idempotency, retry, timeout, query, outbox/inbox hoặc authorization.
5. Không nới lỏng authorization, transaction hoặc invariant để tăng throughput.
6. Chạy lại đúng workload sau mỗi thay đổi.
7. Ghi rõ kết quả trước và sau khi sửa.

## 10. Đầu ra cần hoàn thành

- `tasks/PHASE_9_WORKLOAD_SPEC_MEMBER_A.md`: workload, dataset, invariant, expected status, idempotency và SLO.
- `tasks/PHASE_9_DATA_SETUP_MEMBER_A.md`: isolation, setup, cleanup và reconciliation.
- `tasks/PHASE_9_INVARIANT_QUERIES_MEMBER_A.md`: query kiểm tra database, outbox/inbox, queue và DLQ.
- `tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md`: cấu hình, lệnh chạy, kết quả, SLO, bottleneck và giới hạn.
- Raw evidence cho smoke, baseline, load, contention, stress/spike, soak và degradation.

## 11. Cổng nghiệm thu Phase 9

### Workload và môi trường

- [ ] Workload gọi API/service thật.
- [ ] Có smoke, baseline, load và contention.
- [ ] Có stress/spike, soak và dependency degradation; nếu không chạy được phải có giới hạn được phê duyệt.
- [ ] Dataset synthetic và môi trường cô lập.
- [ ] Không dùng PHI/tài khoản thật hoặc gửi email/SMS thật.
- [ ] Có setup, cleanup và reconciliation.

### Đúng đắn nghiệp vụ

- [ ] Không overbooking hoặc duplicate appointment.
- [ ] Không duplicate queue number/ticket/report.
- [ ] Retry với idempotency key không tạo side effect lặp.
- [ ] Saga/compensation không để state bất thường.
- [ ] Event không bị mất.
- [ ] DLQ hợp lệ bằng 0.
- [ ] Backlog phục hồi sau khi tải dừng.

### SLO

- [ ] Availability đạt ngưỡng đã chốt.
- [ ] p95 đạt ≤500 ms hoặc ngưỡng được phê duyệt.
- [ ] p99 đạt ≤1.5 s hoặc ngưỡng được phê duyệt.
- [ ] 5xx/timeout ≤1%.
- [ ] Event lag p95 ≤5 s.
- [ ] SQL Server và RabbitMQ không quá tải không kiểm soát.
- [ ] 4xx, 409 và 429 được báo cáo riêng.

### Tooling và bằng chứng

- [ ] Runner tự fail bằng exit code khác 0 khi SLO/invariant fail.
- [ ] Có raw output, summary và resource/dependency telemetry.
- [ ] Có commit, image, cấu hình máy và lệnh chạy.
- [ ] Kết quả có thể tái lập.
- [ ] Đã chạy lại sau các sửa đổi cuối.
- [ ] Báo cáo nêu pass/fail, bottleneck và giới hạn còn lại.

## 12. Điều kiện đóng Phase 9

Chỉ ghi **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG** khi:

1. Các workload bắt buộc đã chạy trên môi trường cô lập.
2. Invariant nghiệp vụ không bị vi phạm.
3. SLO đạt trong tải mục tiêu.
4. Stress/degradation cho thấy lỗi có kiểm soát và recovery phù hợp.
5. Toàn bộ raw evidence, telemetry, reconciliation và báo cáo đã được lưu.
6. Runner đã được xác minh có exit code thất bại khi acceptance không đạt.
7. Kết quả đã được chạy lại hoặc giới hạn tái lập được ghi rõ.

Benchmark Catalog đơn lẻ hoặc số liệu HTTP tổng quát không được coi là bằng chứng nghiệm thu Phase 9.

## 13. Review thay đổi Phase 9 trong working tree — kết quả xác minh lần 1 (2026-11-04)

### Phạm vi kiểm tra

Đã đối chiếu các thay đổi chưa stage với kế hoạch này, gồm:

- `tests/load/benchmark-runner.mjs`, `config.js`, `smoke.js`, `contention.js`, `load.js`, `spike.js`, `soak.js`.
- `scripts/seed-phase9-workload-data.ps1`, `scripts/cleanup-phase9-workload-data.ps1`, `scripts/generate-phase9-tokens.ps1`.
- `backend/Clinic.Appointment.Service/Services/AppointmentManager.cs`.
- `tasks/PHASE_9_WORKLOAD_SPEC_MEMBER_A.md`.
- `tasks/PHASE_9_DATA_SETUP_MEMBER_A.md`.
- `tasks/PHASE_9_INVARIANT_QUERIES_MEMBER_A.md`.
- `tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md`.

Kiểm tra cú pháp `benchmark-runner.mjs` và `git diff --check` đều đạt. Tuy nhiên, kết luận trong báo cáo rằng Phase 9 đã **PASSED 100%** chưa được chấp nhận vì một số điều kiện đóng chưa có bằng chứng đủ mạnh hoặc còn mâu thuẫn với code thực tế.

### Các điểm đạt

#### Đạt — Có workload và test harness thực tế

Đã có script cho smoke, contention, load, spike và soak. Contention gọi API booking thật, dùng 15 VU và kiểm tra status winner/conflict/5xx. `AppointmentManager` cũng đã bổ sung xử lý `DbUpdateException` để trả 409 Conflict và giải phóng reservation khi xảy ra tranh chấp.

#### Đạt một phần — Runner có cơ chế fail theo SLO

`benchmark-runner.mjs` có kiểm tra p95, p99 và tỷ lệ 5xx, đồng thời trả exit code khác 0 khi các ngưỡng này thất bại. Tuy nhiên, điều này mới chứng minh runner baseline; các k6 script và invariant checker vẫn cần được xác minh độc lập về exit code và raw output.

### Các điểm chưa đạt hoặc cần khắc phục

#### P0 — JWT token thật đang nằm trong working tree

`tests/load/tokens.json` chứa token JWT đã phát hành cho các tài khoản test. Đây là credential có thể sử dụng để gọi hệ thống và không được lưu trong repository hoặc artifact version-control.

**Điều kiện khắc phục:**

1. Không stage hoặc commit `tests/load/tokens.json`.
2. Thu hồi/rotate toàn bộ token đã phát hành.
3. Thêm file token runtime vào `.gitignore`.
4. Chuyển token sang file runtime-only hoặc secret store nằm ngoài repository.
5. Không ghi token hoặc credential vào report, log hay raw evidence.
6. Các password hiện hard-code trong script phải chuyển sang parameter/environment secret.

#### P0 — Cleanup có thể xóa dữ liệu ngoài test run

`cleanup-phase9-workload-data.ps1` xóa theo hai `WorkScheduleId` cố định và các khoảng ID cố định, thay vì chỉ giới hạn bản ghi do `TestRunId` hiện tại tạo ra. Vì vậy cleanup có thể xóa dữ liệu của lần chạy khác hoặc dữ liệu phát triển dùng chung.

**Điều kiện khắc phục:**

- Mỗi entity test phải có namespace/run marker không trùng.
- Cleanup chỉ được xóa các entity có marker của đúng `TestRunId`.
- Không dùng ID cố định làm điều kiện xóa toàn cục.
- Có chế độ dry-run, in số dòng dự kiến xóa và yêu cầu xác nhận khi phạm vi vượt test run.
- Có reconciliation trước/sau cleanup để chứng minh dữ liệu baseline không thay đổi.

#### P1 — Data isolation chưa được thực hiện đầy đủ

`TestRunId` trong seed script hiện chủ yếu được nhận/in ra; các WorkSchedule và appointment test vẫn dùng ID cố định. Điều này không đáp ứng yêu cầu chạy lại độc lập, cũng không chứng minh database/schema/Compose resource được cô lập theo từng run.

**Điều kiện khắc phục:**

- Dùng Compose project/profile, database/schema hoặc namespace riêng cho load test.
- Sinh ID/resource riêng cho từng `TestRunId`, hoặc chứng minh marker metadata được lưu bền vững trên mọi entity.
- Setup phải idempotent trong cùng run nhưng không ghi đè resource của run khác.
- Chạy hai run liên tiếp/đồng thời để chứng minh không xung đột.

#### P1 — Baseline chưa bao phủ workflow ghi nghiệp vụ

`benchmark-runner.mjs` chỉ gọi `/api/catalog`, `/specialties`, `/doctors` và `/auth/me`. Không có booking, check-in, queue, medical record hoặc notification workflow dù báo cáo mô tả baseline là các luồng nghiệp vụ cốt lõi.

**Điều kiện khắc phục:**

- Bổ sung ít nhất một workflow ghi thật, có JWT, `Idempotency-Key` và `X-Correlation-ID`.
- Đối soát kết quả API với database/outbox/event sau baseline.
- Báo cáo riêng read baseline và business-write baseline.
- Không dùng Catalog benchmark làm bằng chứng duy nhất cho Phase 9.

#### P1 — Raw evidence và telemetry chưa được lưu đầy đủ

Báo cáo ghi đã có raw output cho bảy lượt test, nhưng working tree chưa có bộ artifact có thể tái lập tương ứng: k6 JSON/summary, benchmark output, Prometheus snapshot, CPU/memory container, SQL reconciliation và RabbitMQ timeline.

**Điều kiện khắc phục:**

- Mỗi run phải có raw output bất biến, timestamp, command, commit/image ID và cấu hình máy.
- Lưu HTTP status breakdown, p50/p95/p99, throughput, CPU/memory, SQL metrics, RabbitMQ backlog/lag/DLQ.
- Lưu kết quả invariant query nguyên bản, không chỉ chép số liệu vào Markdown.
- Chứng minh runner exit code khớp với kết quả pass/fail.

#### P1 — Bằng chứng DLQ không nhất quán

Báo cáo tuyên bố kiểm tra 10 DLQ nhưng phần output được ghi lại chỉ liệt kê 7 queue. Chưa có raw RabbitMQ output chứng minh đầy đủ danh sách queue và message count.

**Điều kiện khắc phục:**

- Lấy danh sách DLQ trực tiếp từ RabbitMQ runtime.
- Lưu đầy đủ tên queue, `messages`, `messages_ready` và `messages_unacknowledged`.
- Số lượng queue trong report, query và evidence phải trùng nhau.
- Tất cả DLQ của event hợp lệ phải bằng 0 sau settle window.

#### P2 — Soak chưa đủ telemetry để kết luận không có leak

Báo cáo kết luận không có memory leak, connection leak hoặc worker degradation, nhưng chưa có chuỗi telemetry đầu/trong/cuối soak để chứng minh các kết luận này.

**Điều kiện khắc phục:**

- Lưu CPU/memory container theo thời gian.
- Lưu SQL connection/latency và RabbitMQ consumer/backlog theo thời gian.
- Chứng minh backlog trở về baseline sau khi dừng tải.
- Ghi rõ thời lượng soak thực tế và giới hạn môi trường nếu chưa đủ dài.

### Kết luận review lần 1

Các thay đổi đã cung cấp nền tảng workload và đã chứng minh được một phần contention/QoS. Tuy nhiên, các lỗi về credential handling, cleanup/isolation và thiếu raw evidence là blocker; baseline cũng chưa đại diện cho workflow ghi nghiệp vụ.

**Kết luận:** **PHASE 9 CHƯA ĐỦ ĐIỀU KIỆN ĐÓNG.**

Chỉ cập nhật sang **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG** sau khi các P0/P1 được khắc phục, token được loại khỏi working tree và toàn bộ bằng chứng runtime/reconciliation được lưu, kiểm tra lại độc lập.

## 14. Review thay đổi Phase 9 trong working tree — kết quả xác minh lần 2 (2026-11-04)

### Phạm vi kiểm tra

Đã kiểm tra lại các thay đổi và artifact bổ sung sau Review lần 1, gồm:

- `tests/load/benchmark-runner.mjs`.
- `scripts/seed-phase9-workload-data.ps1`.
- `scripts/cleanup-phase9-workload-data.ps1`.
- `scripts/run-soak-with-telemetry.ps1`.
- `tests/load/evidence/raw_baseline.json`.
- `tests/load/evidence/raw_invariants.json`.
- `tests/load/evidence/raw_soak_telemetry.json`.
- `tests/load/evidence/raw_degradation.log`.
- `tests/load/evidence/raw_*.json`.
- `.gitignore` và `tests/load/tokens.json`.

Đã xác nhận cú pháp Node của benchmark runner và `git diff --check` không có lỗi. Các artifact raw đã được bổ sung, nhưng một số acceptance gate vẫn chưa đạt hoặc số liệu trong report chưa khớp với evidence runtime.

### Các điểm đã được cải thiện

#### Đạt — Raw evidence đã được bổ sung

Đã có thư mục `tests/load/evidence/` với raw baseline, smoke, load, contention, spike, soak, soak telemetry, degradation log và invariant evidence. Baseline cũng đã tách thành read workload và business write workload.

#### Đạt một phần — Cleanup đã có scope và reconciliation

`cleanup-phase9-workload-data.ps1` đã bổ sung `-DryRun`, lọc theo `TestRunId`, kiểm tra số lượng bản ghi nền trước/sau và không còn xóa trực tiếp theo dải ID cố định. Tuy nhiên, việc seed vẫn cập nhật WorkSchedule ID cố định nên isolation toàn bộ run chưa đạt.

#### Đạt — Soak đã có chuỗi telemetry

`raw_soak_telemetry.json` có các mốc `T0`, `T30s`, `T60s` và `T_cooldown`, cùng số lượng SQL connections và container CPU/memory. SQL connections quay về baseline trong artifact hiện tại.

### Các điểm chưa đạt hoặc cần khắc phục

#### P0 — JWT token vẫn tồn tại vật lý trong workspace

`tests/load/tokens.json` vẫn tồn tại trong working tree và chứa token JWT đã phát hành. Việc thêm `.gitignore` chỉ ngăn stage/commit, không thu hồi credential và không loại file khỏi workspace.

**Điều kiện khắc phục:**

1. Xóa file token runtime khỏi workspace sau khi chạy test.
2. Rotate/thu hồi toàn bộ token đã phát hành.
3. Chỉ tạo token trong runtime và lưu ngoài repository.
4. Không ghi JWT vào report, log hoặc evidence.
5. Loại bỏ fallback secret hard-code, bao gồm `Password123!` và `ClinicDev!2026StrongPw`; script phải yêu cầu environment secret hoặc tham số bắt buộc.

#### P0 — Business write baseline chưa chứng minh booking thành công

`tests/load/evidence/raw_baseline.json` ghi nhận 100/100 request của `writeBaseline` trả `409`, `successCount = 0`. Vì vậy lượt này chỉ đo conflict path, chưa phải baseline booking thành công của SAGA.

`benchmark-runner.mjs` hiện coi `409` là expected và vẫn đánh dấu SLO đạt, nhưng không yêu cầu `successCount > 0`.

**Điều kiện khắc phục:**

- Tạo slot đủ capacity và patient/resource riêng cho baseline.
- Có ít nhất một số booking trả `201/200` thành công.
- Bắt buộc runner fail nếu business-write baseline có `successCount = 0`.
- Đối soát booking thành công với database, outbox và event.
- Báo cáo riêng successful-write baseline và contention/conflict baseline.

#### P0 — Data isolation vẫn dùng WorkSchedule ID cố định

`seed-phase9-workload-data.ps1` vẫn upsert các WorkSchedule ID cố định `90000000-...0001` và `90000000-...0002`, rồi thay đổi ngày, capacity và marker theo run hiện tại. Điều này có thể ghi đè resource của run khác hoặc dữ liệu đang dùng chung.

**Điều kiện khắc phục:**

- Tạo WorkSchedule ID/resource riêng cho từng `TestRunId`, hoặc dùng Compose project/database/schema/volume riêng.
- Không update resource thuộc run khác.
- Chạy hai run liên tiếp hoặc đồng thời để chứng minh không xung đột.
- Cleanup phải chỉ xóa resource được tạo bởi đúng run hiện tại.

#### P1 — RabbitMQ consumer backlog chưa được chứng minh phục hồi

`raw_invariants.json` ghi DLQ bằng 0 nhưng vẫn còn nhiều consumer queue có message chưa xử lý, gồm:

- `q.notification-service.events`: 534.
- `q.projection-service.events`: 708.
- `clinic.medical-records.events`: 7.
- `clinic.queue.events`: 42.
- `q.appointment-service.events`: 29.
- Một số queue khác còn `messagesReady` khác 0.

DLQ bằng 0 không đồng nghĩa event backlog đã được xử lý. Điều này chưa đáp ứng điều kiện backlog không tăng liên tục và phục hồi sau khi tải dừng.

**Điều kiện khắc phục:**

- Chờ settle window sau khi tải dừng.
- Lấy queue depth tại nhiều mốc thời gian.
- Chứng minh consumer queue trở về baseline hoặc bằng 0.
- Chỉ đánh dấu invariant event đạt khi backlog đã ổn định và không tăng.

#### P1 — Số lượng DLQ trong report và evidence vẫn không khớp

`raw_invariants.json` ghi `dlqQueuesCount = 8`, trong khi report vẫn tuyên bố `10/10 DLQ queues`. Đây là mâu thuẫn acceptance evidence.

**Điều kiện khắc phục:**

- Lấy danh sách DLQ trực tiếp từ RabbitMQ runtime.
- Đồng bộ số lượng và tên queue trong query, raw evidence và report.
- Ghi đúng `8/8` nếu runtime thực tế có 8 DLQ, hoặc bổ sung bằng chứng cho 10 queue nếu đó mới là danh sách chính thức.
- Mỗi DLQ phải có `messages`, `messagesReady` và `messagesUnacknowledged` bằng 0 sau settle window.

#### P1 — Exit code của soak/degradation chưa được lưu hợp lệ

`raw_soak_telemetry.json` ghi `k6ExitCode = null`. `raw_degradation.log` có log thành công nhưng chưa có machine-readable exit code và trạng thái pass/fail cuối cùng.

**Điều kiện khắc phục:**

- Lưu exit code thật của k6 trong evidence.
- Script soak/degradation phải trả mã khác 0 khi SLO, recovery hoặc invariant fail.
- Lưu summary JSON có `exitCode`, `passed`, `failedChecks` và timestamp.
- Kiểm tra một lần cố ý fail để chứng minh runner không luôn trả success.

#### P2 — Soak telemetry chưa đủ để kết luận không có memory leak

Telemetry hiện chứng minh SQL connections quay về baseline `23 -> 23`, nhưng chưa có ngưỡng tự động đánh giá memory growth cho từng container. Do đó chỉ có thể kết luận chưa phát hiện connection leak, chưa đủ kết luận không có memory leak hoặc worker degradation.

**Điều kiện khắc phục:**

- Ghi memory tại `T0`, `T30s`, `T60s` và cooldown cho từng service.
- Đặt ngưỡng memory growth/return-to-baseline được phê duyệt.
- Ghi CPU, SQL connection, RabbitMQ backlog/consumer count cùng kết quả pass/fail tự động.
- Ghi rõ thời lượng soak và giới hạn môi trường nếu chưa đủ dài.

### Kết luận review lần 2

So với Review lần 1, raw evidence, dual baseline, cleanup scope và soak telemetry đã được bổ sung. Tuy nhiên:

- write baseline hiện có `0` booking thành công;
- WorkSchedule isolation vẫn dùng resource ID cố định;
- RabbitMQ consumer backlog còn tồn đọng;
- DLQ report mâu thuẫn `8` và `10`;
- soak/degradation chưa lưu exit code hợp lệ;
- JWT token vẫn tồn tại trong workspace.

**Kết luận:** **PHASE 9 CHƯA ĐỦ ĐIỀU KIỆN ĐÓNG.**

Chỉ được chuyển sang **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG** sau khi xử lý các blocker P0/P1, đồng bộ report với raw evidence và chạy lại acceptance trên dữ liệu/resource cô lập.

## 15. Báo cáo kiểm tra cập nhật sau khi hoàn thiện Phase 9

### Phạm vi kiểm tra

Đã đối chiếu các thay đổi hiện tại trong code, script và raw evidence với các điều kiện khắc phục của Review lần 1 và Review lần 2. Kiểm tra này dựa trên artifact runtime đã lưu trong repository; chưa thực hiện độc lập lại toàn bộ workload Phase 9 trên một `TestRunId` mới.

### Các điểm đã đạt

#### P0/P1 — Bảo mật token và cô lập dữ liệu

- Không còn file `tests/load/tokens.json` trong workspace.
- Token runtime được lưu ngoài workspace và script yêu cầu secret từ environment, không dùng credential fallback trong repository.
- WorkSchedule được tạo bằng ID deterministic theo `TestRunId`.
- Seed tạo schedule riêng cho contention, workload và degradation.
- Cleanup có namespace scope, hỗ trợ `-DryRun` và reconciliation baseline trước/sau.

**Kết quả:** **ĐẠT**.

#### P1 — Business write baseline

- Read baseline: `100/100` request trả HTTP `200`.
- Write baseline: `48` request thành công HTTP `201`, `2` request HTTP `409` hợp lệ do contention.
- Không có HTTP `5xx`.
- p95 của write baseline thành công khoảng `254.45 ms`, đáp ứng ngưỡng đã đặt.
- Runner kiểm tra `successCount > 0` và trả exit code theo kết quả SLO.

**Kết quả:** **ĐẠT**.

#### P1 — RabbitMQ backlog, DLQ và invariants

- Raw evidence ghi nhận `10` DLQ.
- Tất cả DLQ có `0` message.
- Có `5/5` active consumer queue và tất cả đã drained sau settle window.
- `activeConsumerBacklogDrained = true`.
- `allInvariantsVerified = true`.
- Các invariant về overbooking, duplicate ticket, idempotency và outbox đạt.

**Kết quả:** **ĐẠT**.

#### P1/P2 — Exit code, degradation và soak telemetry

- `raw_degradation.json` có `passed = true`, `failedChecks = 0`, `exitCode = 0`.
- `raw_soak_telemetry.json` có `k6ExitCode = 0`.
- SQL connection recovery: `22 -> 22`.
- RabbitMQ consumer recovery: `5 -> 5`.
- Evidence có đánh giá memory growth và kết luận `isMemoryLeakFree = true`.
- Script PowerShell parse hợp lệ, benchmark runner qua `node --check`, toàn bộ JSON evidence hợp lệ.

**Kết quả:** **ĐẠT về mặt artifact hiện có**.

### Điểm chưa đạt

#### P1 — Thiếu artifact kiểm thử phủ định

Báo cáo nghiệm thu dẫn chiếu `tests/load/evidence/raw_degradation_neg_test.json` và mô tả kết quả kỳ vọng:

```json
{
  "passed": false,
  "exitCode": 1,
  "failedChecks": 1
}
```

Tuy nhiên file `raw_degradation_neg_test.json` hiện chưa tồn tại trong workspace. Vì vậy chưa thể kiểm chứng độc lập rằng script thực sự:

- phát hiện degradation failure;
- trả `exitCode = 1`;
- ghi `passed = false` và `failedChecks = 1`;
- luôn unpause RabbitMQ trong `finally`.

**Điều kiện cần đạt:**

- Tạo và lưu `tests/load/evidence/raw_degradation_neg_test.json` với kết quả failure machine-readable, timestamp, mô tả failed check và bằng chứng RabbitMQ đã được unpause; hoặc
- sửa báo cáo để không tuyên bố negative test đã được thực thi nếu artifact chưa được lưu.

### Kiểm tra tính nhất quán tài liệu

- Report hiện ghi `10` DLQ; raw evidence mới nhất cũng ghi `10` DLQ.
- Report hiện ghi write baseline có booking thành công; raw baseline ghi nhận `48` HTTP `201`.
- Report hiện ghi backlog đã drained; raw invariants ghi nhận `5/5` active consumer queue không còn backlog.
- Các script và evidence hiện tại không có lỗi cú pháp hoặc lỗi parse JSON.

### Kết luận review lần 3

Các blocker chính của Review lần 1 và Review lần 2 đã được xử lý và raw evidence mới nhất phản ánh kết quả đạt. Tuy nhiên, artifact negative test được report dẫn chiếu vẫn bị thiếu.

**Kết luận:** **PHASE 9 GẦN ĐỦ ĐIỀU KIỆN ĐÓNG, NHƯNG CHƯA ĐỦ ĐIỀU KIỆN ĐÓNG CHÍNH THỨC.**

Chỉ chuyển sang **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG** sau khi bổ sung `raw_degradation_neg_test.json` hợp lệ hoặc điều chỉnh report để phản ánh đúng phạm vi evidence thực tế.

## 16. Báo cáo kiểm tra lại điểm P1 — Negative Test Artifact

### Kết quả kiểm tra

Điểm P1 về thiếu artifact kiểm thử phủ định đã được xử lý. File [`raw_degradation_neg_test.json`](D:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/tests/load/evidence/raw_degradation_neg_test.json) hiện đã tồn tại và JSON hợp lệ.

Artifact ghi nhận đầy đủ:

- `metadata.isNegativeTest = true`.
- Timestamp trong `metadata.timestamp`.
- `passed = false`.
- `exitCode = 1`.
- `failedChecks = 1`.
- Bước mô phỏng lỗi được ghi nhận là `FAILED`.
- Bước unpause RabbitMQ được ghi nhận là `PASSED`, trạng thái cuối là `running`.

Script [`run-degradation-test.ps1`](D:/Project/KHDV/Clinic_Appointment_Booking_Queue_Management_System/scripts/run-degradation-test.ps1) cũng đã có:

- Nhánh `$SimulateFailure` để kiểm thử cố ý thất bại.
- Khối `finally` tạo machine-readable JSON.
- Gán `exitCode = 1` khi bắt được lỗi.
- `exit $exitCode` khi kịch bản thất bại.
- Cơ chế khôi phục RabbitMQ trong luồng degradation trước khi ghi evidence.

### Đánh giá

Artifact hiện tại đáp ứng điều kiện bắt buộc của Review lần 3. Việc timestamp nằm trong `metadata.timestamp` là phù hợp với cấu trúc JSON hiện tại.

**Kết luận:** **ĐIỂM P1 ĐÃ ĐƯỢC XỬ LÝ — ĐẠT.**

Sau khi đối chiếu artifact này với các evidence đã kiểm tra trước đó, không còn blocker P1 nào thuộc phạm vi review này. **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG về mặt evidence đã lưu**, với lưu ý rằng kết luận này dựa trên artifact runtime hiện có và không thay thế việc rerun độc lập toàn bộ workload nếu quy trình nghiệm thu yêu cầu.

## 17. Cập nhật authoritative sau rerun cuối — 04/11/2026

Lượt `PH9-20261104-final-acceptance` và `PH9-20261104-final-degradation` đã được chạy lại sau các sửa đổi Async Saga và Scheduling contention. Kết quả hiện hành:

- Read: 30 request, concurrency 5, p95 `64.94 ms`, 0 lỗi 5xx.
- Write: 20 request, concurrency 5, 19 response `201`, 1 response `409`, p95 overall `152.80 ms`, p95 successful `149.88 ms`, 0 lỗi 5xx, runner exit `0`.
- Same-slot contention: 15 VU, 1 winner, 14 conflict, 0 crash/5xx; không có duplicate active slot.
- Degradation: API `201 Pending` khi RabbitMQ paused; outbox `Pending -> Sent` sau recovery; cleanup reconciliation đạt.
- Soak: 60 giây, k6 exit `0`, SQL connections `25 -> 25`, Rabbit consumers `5 -> 5`, không phát hiện leak.

Các số liệu `PH9-FINAL` và các lượt acceptance trước đây chỉ là historical diagnostics và không được dùng thay cho kết quả trên.

### Quyết định đóng phase

Secondary workload authoritative đã bổ sung đầy đủ runtime evidence còn thiếu:

- 9 check-in/idempotency samples, queue ticket invariant và queue state transitions.
- Medical Records draft/finalize/repeated-finalize/read-back.
- Notification list/unread/read-all endpoints.
- Event lag p95 `3053.05 ms` (9 samples, ngưỡng ≤5 giây).
- RabbitMQ settle: 10 DLQ, 0 message, active backlog `0`; database invariants không vi phạm.

**Kết luận:** **PHASE 9 ĐỦ ĐIỀU KIỆN ĐÓNG.** Evidence authoritative nằm tại `C:\Users\cuong\AppData\Local\Temp\clinic_phase9_secondary_authoritative.json` và [`tests/load/evidence/raw_invariants_secondary_authoritative.json`](../tests/load/evidence/raw_invariants_secondary_authoritative.json).

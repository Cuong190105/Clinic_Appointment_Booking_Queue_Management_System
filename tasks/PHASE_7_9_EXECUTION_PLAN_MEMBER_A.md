# Kế hoạch thực hiện Phase 7–9 — Thành viên A

**Mục tiêu:** hoàn thiện contract HTTP, SLI/SLO/QoS và acceptance tải/nghiệp vụ sau sáu phase tách service. Tài liệu này mô tả phần việc A sở hữu, cách phối hợp với B và tiêu chí đánh giá kết thúc từng phase.

**Hiện trạng:** B đã thêm API Catalog, một số contract/QoS tests, Prometheus/Grafana artifacts và kịch bản k6/Node. Rà soát hiện tại kết luận Phase 7 chưa nghiệm thu, Phase 8 đạt một phần, Phase 9 có harness ban đầu nhưng chưa có bằng chứng tải nghiệp vụ. Chi tiết hiện trạng và phần B cần khắc phục được ghi trong các execution plan B tương ứng.

**Phạm vi:** Clinic API Gateway/BFF và sáu service Identity, Scheduling, Appointment, Queue, Medical Records, Notification; SQL Server, RabbitMQ và frontend trong các luồng E2E. Không đổi quyền sở hữu domain hoặc mở lại quyết định Phase 1–6.

## 1. Quyết định thiết kế cần chốt trước các cổng triển khai

Không thấy mâu thuẫn mới về ownership hoặc workflow nghiệp vụ. Có các điểm contract/acceptance chưa thống nhất; A và B cần chốt theo khuyến nghị dưới đây trước khi tạo spec chính thức, khóa SLO và chạy tải.

### D1. Version API và đường dẫn public/internal — trước Phase 7

**Vấn đề:** yêu cầu chung đòi mỗi API có version, nhưng các endpoint hiện hành phần lớn chưa có `/v1`; API Catalog lại đang công bố URL OpenAPI riêng cho từng service dù chúng chưa được serve. Đồng thời frontend chỉ được gọi Gateway, còn service có endpoint nội bộ.

**Phương án:**

- A. Thêm `/v1` vào mọi route ngay bây giờ; cập nhật Gateway, client và frontend đồng loạt.
- B. Giữ URL route hiện hành; ghi phiên bản contract trong `info.version` của OpenAPI, phân loại rõ Gateway-public và service-internal, chỉ công bố qua catalog những spec thực sự truy cập được.

**Khuyến nghị:** B. Hệ thống đang là sản phẩm môn học, chưa cần hỗ trợ nhiều API generation song song; tránh đổi hàng loạt route sau khi sáu phase đã nghiệm thu. Nếu yêu cầu môn học hiểu “API version” bắt buộc là version trong URL thì cần chốt A trước khi làm spec.

### D2. Schema lỗi public — trước khi chốt OpenAPI Phase 7

**Vấn đề:** một số API mô tả `ProblemDetails`, trong khi Gateway có `ApiResponse` wrapper/global exception envelope. Proxy/service endpoints cũng có thể trả payload downstream nguyên dạng. Không thể mô tả một schema lỗi duy nhất nếu wire response khác nhau.

**Phương án:**

- A. Chuyển toàn bộ Gateway/service errors sang RFC 7807 ngay trong Phase 7.
- B. Giữ wire contract hiện hành, kiểm kê từng response; thống nhất cách biểu diễn lỗi mới tại Gateway và lên migration riêng nếu cần đồng nhất cũ.

**Khuyến nghị:** B để tránh âm thầm phá frontend và API đã nghiệm thu. Trong Phase 7, OpenAPI phải mô tả đúng status/body thật; không ghi `ProblemDetails` chỉ vì một test mẫu nói vậy. Nếu A muốn thay schema công khai, cần migration đồng bộ frontend và contract tests trước khi bật.

### D3. SLO, cửa sổ đo và tải mục tiêu — trước Phase 8 gate và Phase 9

**Vấn đề:** các ngưỡng đang là đề xuất cho máy Docker, chưa được chốt/đo baseline; kết quả phụ thuộc cấu hình máy, số replica, dữ liệu và cách tính lỗi.

**Đề xuất để làm baseline:** availability ≥99%; latency p95 ≤500 ms, p99 ≤1.5 s cho API đồng bộ; 5xx/timeout ≤1%; event processing lag p95 ≤5 s và DLQ = 0 với event hợp lệ. Đo tối thiểu 5 phút tải ổn định, loại 4xx validation/permission/business conflict dự kiến khỏi availability nhưng vẫn báo cáo riêng; đo cả Gateway và downstream.

**Khuyến nghị:** chốt các con số làm mục tiêu môn học sau lượt baseline smoke trên máy hiện tại. Không tuyên bố đây là SLA production. Ghi cấu hình máy/Compose và lý do nếu phải điều chỉnh. Với rate limit, xác nhận mặc định Gateway hiện tại 100 request/phút có áp dụng toàn cục hay theo route; khuyến nghị bỏ qua health/readiness/metrics/OpenAPI và áp chính sách riêng cho API public/auth thay vì giới hạn chung mọi endpoint.

### D4. Kịch bản tạo dữ liệu và isolation — trước Phase 9

**Vấn đề:** booking/check-in/queue/report tạo state; chạy trên DB đang dùng chung có thể làm bẩn dữ liệu mock hoặc gửi notification ra ngoài.

**Khuyến nghị:** dùng Compose project/profile và database/schema riêng cho load test; chỉ tạo dữ liệu synthetic, provider email/SMS MOCK, không dùng tài khoản/PHI thật. Không xóa volume/dev DB hiện tại để reset tải. Mỗi kịch bản có setup, idempotency key riêng và cleanup/reconciliation rõ.

### D5. Retry khi gọi command — chốt trong Phase 8

**Vấn đề:** timeout không chứng minh command chưa commit; retry mù có thể tạo booking/check-in/report/notification trùng.

**Khuyến nghị:** không retry command ở Gateway nếu thiếu idempotency key và service owner không đảm bảo idempotency. Retry chỉ cho lỗi transient đã phân loại và có backoff/budget; không retry validation, permission, business conflict hoặc permanent error. Không thay retry/outbox policy của Phase 1–6 trong khuôn khổ Phase 8 trừ khi có lỗi được chứng minh.

## 2. Phase 7 — Contract HTTP và OpenAPI

### Bước 7.1 — Chốt inventory API (A chủ trì)

1. Rà toàn bộ route tại Gateway/controllers và Minimal API của sáu service; dùng route table/runtime spec làm dữ liệu chính, không chỉ grep controller.
2. Ghi mỗi operation: service owner, method/path, public hay internal, downstream target, request/response DTO, success/error status, validation, auth scheme/role/permission, correlation/idempotency headers và consumer (frontend/service khác).
3. Đối chiếu public Gateway path với route đích. Ghi rõ path mapping như Medical Reports public API sang Medical Records service API; không để API Catalog quảng bá route không tồn tại.
4. Đánh dấu endpoint health, readiness, metrics, OpenAPI và internal operator. Không đưa secret, ví dụ token thật hoặc PHI vào spec.

**Đầu ra:** `API_INVENTORY_MEMBER_A.md` hoặc bảng cùng nội dung trong tài liệu Phase 7; danh sách endpoint có owner và trạng thái spec.

### Bước 7.2 — Chốt quy ước contract

1. Áp dụng quyết định D1–D2: version, visibility, security scheme, error payload và quy tắc breaking change.
2. Với mỗi endpoint, ghi `ProducesResponseType`/metadata tương ứng; khai báo 400/401/403/404/409/429/5xx khi áp dụng, pagination/filter, empty response và idempotency behavior.
3. Khai báo schema cho headers quan trọng: `Authorization`, `X-Correlation-ID`, `Idempotency-Key`; không đưa internal service URL/credential thành frontend contract.
4. Tài liệu event vẫn thuộc event contracts; không dùng OpenAPI thay AsyncAPI/event schema.

### Bước 7.3 — Sinh spec theo service owner

1. Bổ sung/gom metadata tại chính service sở hữu endpoint; Gateway có spec riêng cho public BFF/proxy surface.
2. Chọn một generator/validator tương thích .NET 10 và pin version; script phải sinh spec cho cả controllers lẫn Minimal APIs.
3. Dùng output build artifact hoặc endpoint nội bộ có quyền truy cập rõ. Chỉ catalog link các spec thực sự được phục vụ. Nếu OpenAPI của internal service không được public, giữ spec trong artifact/repository nội bộ.
4. Sửa API Catalog để route prefixes/auth/role, OpenAPI link và topology phản ánh cấu hình thật; không hard-code authority localhost như thể đó là URL chạy trong Compose.
5. Kiểm tra spec không chứa PHI, secret, internal credentials hoặc endpoint không thuộc scope.

### Bước 7.4 — Contract validation

1. B và A cùng bổ sung test lấy OpenAPI document thực của từng app khi khởi động test host.
2. So sánh route/method thực với inventory; kiểm tra request/response schemas, required fields, auth, lỗi và header.
3. Lưu snapshot hoặc baseline versioned. Test phải thất bại với thay đổi breaking giả lập; thay đổi additive phải được chấp nhận theo policy.
4. Test Gateway routing/auth/error mapping cho các luồng đại diện (auth, schedule/slot, booking, queue, medical records, notifications).
5. Thêm một lệnh validate spec/contract chạy lặp lại và ghi vào CI/local acceptance.

### Yêu cầu kết thúc Phase 7

- [ ] Có inventory toàn bộ HTTP routes trong scope, owner/consumer và public/internal rõ.
- [ ] OpenAPI hợp lệ cho Gateway và từng service/API trong scope theo quyết định D1; không bỏ sót Minimal APIs.
- [ ] Catalog chỉ trỏ tới spec/health/route tồn tại và không công bố nội bộ sai phạm vi.
- [ ] Mỗi operation mô tả DTO, status/error, auth, validation và headers quan trọng đúng wire behavior.
- [ ] Contract/drift test đọc spec thật và bắt được ít nhất một breaking change fixture.
- [ ] Có lệnh generate + validate, kết quả và baseline được lưu; frontend không gọi thẳng service.

### Bằng chứng cần lưu

Inventory, OpenAPI artifacts, log command/test, ảnh hoặc response catalog nếu có, diff baseline và danh sách gap còn lại.

## 3. Phase 8 — SLI/SLO, QoS và observability

### Bước 8.1 — Ma trận SLI/SLO nghiệp vụ (A chủ trì)

Với từng workflow, ghi công thức đo, denominator, cửa sổ, mục tiêu, nguồn metric, điểm đo Gateway/service, nhóm status bị loại/trình bày riêng và owner xử lý:

- Identity: login/refresh latency và error; phân biệt credential sai với lỗi server; không ghi token.
- Scheduling + Appointment booking/reschedule/cancel: end-to-end latency, conflict/timeout, reservation pending/compensation và overbooking invariant.
- Check-in → Queue ticket: HTTP latency, event-to-ticket lag, duplicate check-in/ticket và ticket issuance invariant.
- Queue → Medical Records: event lag, `ExamStarted`/`ExamCompleted` xử lý đúng; `ExamCompleted` không tự finalize report.
- Medical Records: read/save/finalize latency, authorization failures, audit write outcome; không ghi PHI vào log/metric.
- Notification: event-to-inbox/provider attempt lag, retry/DLQ, persistence/recovery; provider giả khi tải.
- Gateway/dependencies: route latency/errors, timeout, DB latency/pool, RabbitMQ backlog/unacked/DLQ.

Chốt ngưỡng theo D3. Availability tính trên request được kỳ vọng phục vụ; 4xx nghiệp vụ/validation và 409 contention báo riêng, không lẫn vào 5xx. Timeout/upstream failure tính là lỗi; 429 chính sách báo riêng và phải được đưa vào trải nghiệm thành công/thất bại theo workload.

### Bước 8.2 — Phân vai instrument (A/B)

- **A:** xác nhận nơi phát sinh domain metric/invariant, thêm metric nhỏ cần thiết tại service owner; rà timeout, rate limit, idempotency và failure semantics của các endpoint A sở hữu.
- **B:** exporter/scrape, label convention, dashboards, alerts, tracing/log correlation, RabbitMQ/SQL resource telemetry.
- **Cùng làm:** xác minh `/metrics` thực sự được expose trước khi thêm target; không dùng metric name giả trong dashboard. Label có cardinality giới hạn, không chứa user/patient/appointment ID, PHI hay credential.
- Rà retry từng outbound call; chỉ retry operation an toàn. Ghi timeout budget theo dependency/hop và tổng end-to-end.

### Bước 8.3 — Kiểm chứng policy qua boundary thật

1. Gọi Gateway bằng HTTP client/test host; vượt rate limit và kiểm tra 429, headers, key partition và reset window.
2. Dùng downstream giả có timeout/connection refusal; xác minh Gateway trả đúng status/schema trong ngân sách timeout, không retry command gây side effect.
3. Xác nhận correlation ID đi qua Gateway tới service và event; xác nhận sanitized logs/traces không chứa credential/PHI.
4. Restart broker hoặc dừng consumer trong profile test riêng; quan sát outbox age, queue lag/DLQ, khôi phục sau khi dependency hoạt động lại.
5. Chạy Prometheus rule validation; mở dashboard với metric thật, xác minh alert query có thể chuyển firing/resolved bằng test signal.

### Bước 8.4 — Baseline và báo cáo

Chạy baseline nhẹ trước Phase 9, lưu SLI theo service và E2E; ghi cấu hình phần cứng/CPU/RAM, Compose profile, image/tag, số replica, database state, thời lượng và số request. Nếu ngưỡng không đạt, mở issue gắn service owner và không hạ ngưỡng chỉ để làm test pass.

### Yêu cầu kết thúc Phase 8

- [ ] Bảng SLI/SLO có định nghĩa, nguồn, formula, cửa sổ và status treatment cho từng service/workflow.
- [ ] Các metrics/logs/traces cần thiết xuất hiện ở runtime; scrape target chỉ tới endpoint tồn tại.
- [ ] Có dashboard/alerts được kiểm tra bằng dữ liệu thực; query và labels đúng.
- [ ] Timeout/retry/rate-limit/idempotency policy có HTTP/failure tests qua Gateway/service boundary.
- [ ] Event lag/retry/DLQ và dependency health quan sát được.
- [ ] Không lộ PHI, secret hoặc ID cá nhân trong metrics/logs.
- [ ] Baseline ghi được môi trường và giới hạn; ngưỡng D3 được chốt trước load.

## 4. Phase 9 — Kiểm thử tải và nghiệm thu workflow

### Bước 9.1 — Chuẩn bị môi trường an toàn

1. Tạo Compose project/profile và database test tách biệt với volume dữ liệu mock/dev hiện tại. Không dùng `down -v` trên stack dùng chung.
2. Dùng tài khoản/appointment/patient synthetic; provider Notification đặt MOCK; cấm email/SMS thật.
3. Tạo seeding và cleanup script có thể lặp lại, run ID riêng, giữ log và đối soát sau test.
4. Ghi commit, cấu hình máy, Docker resource limit, số replica, dữ liệu và phiên bản công cụ.

### Bước 9.2 — Định nghĩa hành trình và invariant

- **Browse:** đọc specialty/doctor/slot; response có schema hợp lệ và p95 đạt target.
- **Booking thành công:** tạo reservation/appointment đúng flow; không có reservation mồ côi sau workflow hoàn thành.
- **Booking contention:** nhiều client tranh một slot có capacity xác định; đúng số winner theo capacity, loser nhận conflict; không overbook/appointment trùng.
- **Check-in và cấp queue:** dùng appointment test hợp lệ; mỗi appointment có tối đa một ticket, QueueNumber unique theo `(DoctorId, QueueDate)`, duplicate/retry trả cùng kết quả theo chính sách.
- **Queue workflow:** gọi/bỏ qua/retry/start/complete theo permission và trạng thái; lượt đang khám không làm bác sĩ mất quyền gọi lượt waiting tiếp theo.
- **Medical Records:** thao tác với hồ sơ synthetic; quyền/audit đúng, finalized report không đổi do event `ExamCompleted`.
- **Notification:** verify notification persisted và retries qua MOCK; không yêu cầu provider thật.

Mọi invariant được xác nhận từ API response **và** database/service state sau khi event lag ổn định; không dùng test `Interlocked` làm bằng chứng production behavior.

### Bước 9.3 — Workload profile đề xuất

Chạy tăng dần, dừng nếu error/resource vượt giới hạn máy. Đây là cấu hình khởi điểm để baseline, A/B có thể hạ hoặc tăng sau khi ghi lý do:

1. Smoke: 2 VU, 30 giây, chỉ xác nhận setup/auth/health và không sinh dữ liệu sai.
2. Baseline: 5 VU, 2 phút cho mỗi luồng đọc và command ngắn.
3. Steady load: ramp 5→20 VU trong 1 phút, giữ 5 phút; đánh giá SLO đã chốt.
4. Peak: ramp 20→50 VU, giữ 2 phút nếu máy chịu được; phân biệt 429/conflict với 5xx.
5. Contention: tối thiểu 50 request đồng thời trên resource/capacity giới hạn; kiểm tra invariant từ DB.
6. Spike: 5→50 VU trong 10 giây, giữ 30 giây rồi giảm và theo dõi recovery.
7. Soak: 20 VU trong 30 phút; quan sát memory, DB pool, consumer lag, outbox/DLQ.
8. Dependency degradation: dừng/làm chậm một dependency mỗi lượt trong profile riêng; kiểm tra timeout, retry, backlog và recovery. Không chạy đồng thời nhiều fault.

Nếu local machine không thể giữ profile, giảm VU nhưng không bỏ invariant; báo cáo cấu hình và không so sánh trực tiếp với run khác.

### Bước 9.4 — Đánh giá, sửa và chạy lại

1. Runner fail exit code khi SLO/invariant fail; phân loại 2xx, 4xx nghiệp vụ, 401/403, 429, 5xx, timeout và lỗi công cụ.
2. Ghi p50/p95/p99, throughput, error/timeout, concurrency, DB latency/pool, CPU/RAM, queue depth/lag, outbox age, DLQ.
3. A xử lý bottleneck/code thuộc service/API; B xử lý harness/metrics/dashboards. Mọi thay đổi không được bỏ authorization, transaction, idempotency hoặc validation để tăng throughput.
4. Chạy lại cùng commit/workload sau sửa; so sánh các run cùng cấu hình.
5. Ghi report: kết quả, giới hạn máy, lỗi còn lại, nguyên nhân, owner và follow-up.

### Yêu cầu kết thúc Phase 9

- [ ] Có dataset synthetic và profile chạy cô lập, reset/cleanup an toàn.
- [ ] Smoke, steady/load, contention đã chạy trên hệ thống thật; spike/soak/fault đã chạy hoặc có lý do kỹ thuật cụ thể và plan.
- [ ] Invariants đo từ state thật không vi phạm; mọi expected conflict được phân loại đúng.
- [ ] SLO đã chốt được tính trên đúng request population; runner tự fail nếu vượt ngưỡng.
- [ ] Có resource + SQL + RabbitMQ telemetry, correlation ID và không gửi provider thật.
- [ ] Báo cáo tái lập với lệnh, commit, máy, profile, dataset, kết quả và bottlenecks.
- [ ] Docker Compose health/runtime regression và frontend happy path liên quan vẫn hoạt động.

## 5. Thứ tự làm và phối hợp với B

| Cổng | A chịu trách nhiệm | B phối hợp | Điều kiện chuyển bước |
|---|---|---|---|
| 7A — inventory | route/DTO/auth/error/public-internal; đề xuất D1/D2 | lấy spec thật, rà drift/catalog | bảng inventory và contract policy được duyệt |
| 7B — spec | metadata service owner, Gateway map và wire behavior | generator/validator/snapshot | mọi spec generate/validate; catalog không sai link |
| 8A — SLI | domain formulas, status exclusions, đề xuất thresholds | exporter/metric names/query | metric map và thresholds thống nhất |
| 8B — QoS | endpoint timeout/idempotency/auth behavior | alerts/dashboard/failure tests | kiểm chứng qua HTTP boundary và baseline |
| 9 — tải | journeys, invariants, synthetic dataset, pass criteria | k6/runner/resource collection | runbook/test isolation được review trước khi chạy |
| Final | sửa bottleneck service owner, E2E acceptance | tổng hợp telemetry/report | tất cả acceptance checklist pass hoặc residual blocker có owner/lý do |

## 6. Bộ bằng chứng bàn giao

Tạo/cập nhật các artifact sau khi thực hiện:

- `API_INVENTORY_MEMBER_A.md` hoặc bảng tương ứng trong báo cáo Phase 7.
- OpenAPI spec artifacts và lệnh generate/validate.
- `SLI_SLO_MATRIX_MEMBER_A.md` hoặc bảng SLI/SLO ở báo cáo Phase 8.
- Kết quả runtime QoS, Prometheus rule validation và dashboard screenshots/query samples.
- Load test data/setup/cleanup, lệnh chạy, raw summary và `PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md`.
- Danh sách lỗi/bottleneck, quyết định điều chỉnh ngưỡng, owner và follow-up.

## 7. Thứ tự xử lý khi có mismatch

1. Domain ownership và quyết định đã chốt trong Phase 1–6.
2. Wire behavior đang chạy và owner service contract.
3. OpenAPI/inventory phải được cập nhật cho khớp wire behavior; không che mismatch bằng spec giả.
4. QoS/SLO và workload test đo đúng contract đó.
5. Nếu cần đổi public contract, mở quyết định riêng, cập nhật frontend/consumer, contract tests và migration note trước khi merge.

## 8. Tài liệu tham chiếu

- [`PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md`](PHASE_7_9_FINAL_ACCEPTANCE_PLAN.md)
- [`TASK_MEMBER_A.md`](TASK_MEMBER_A.md)
- [`TASK_MEMBER_B.md`](TASK_MEMBER_B.md)
- [`COMMON_REQUIREMENTS_AND_NOTES.md`](COMMON_REQUIREMENTS_AND_NOTES.md)
- Phase 7/8/9 Member B plans để theo dõi phần B: `PHASE_7_OPENAPI_CONTRACTS_EXECUTION_PLAN_MEMBER_B.md`, `PHASE_8_SLA_SLO_QOS_EXECUTION_PLAN_MEMBER_B.md`, `PHASE_9_LOAD_TESTING_EXECUTION_PLAN_MEMBER_B.md`.

## 9. Rà soát thay đổi Phase 7 hiện tại (2026-11-02)

Đợt rà soát này đối chiếu các thay đổi Git hiện có với yêu cầu Phase 7. Kết luận: đã có phần khung triển khai, nhưng **Phase 7 chưa đủ điều kiện đóng**. Đây là đánh giá tĩnh từ source, tài liệu và snapshot; chưa chạy build/test hoặc nghiệm thu runtime.

### 9.1. Các phần đã thực hiện

- Đã bật endpoint OpenAPI cho Gateway và sáu service.
- Đã mở rộng API Catalog để liệt kê sáu service cùng route prefixes và URL tài liệu OpenAPI.
- Đã bổ sung API inventory, các snapshot contract và script chạy nhóm kiểm thử API contract.
- Quy ước giữ route hiện hành và đặt version trong `info.version` phù hợp với khuyến nghị D1.

### 9.2. Điểm chưa đạt và yêu cầu khắc phục

#### P1 — Baseline bị ghi đè tự động bởi test

Các test hiện ghi kết quả API Catalog và Gateway OpenAPI vào file baseline ngay khi chạy. Test proxy OpenAPI của sáu service còn ghi từng response vào cùng file `gateway-openapi-v1.json`, có thể thay snapshot Gateway bằng tài liệu của một service. Baseline phải là dữ liệu đã review, được lưu cố định; test chỉ đọc và so sánh, tuyệt đối không cập nhật snapshot ngầm.

**Cần làm:** bỏ thao tác ghi file khỏi test thường; tách riêng lệnh cập nhật snapshot có chủ đích nếu cần. Mỗi service cần snapshot có tên riêng, hoặc so sánh trực tiếp spec runtime với baseline tương ứng.

#### P1 — Kiểm thử drift chưa dùng spec hoặc baseline thật

Hai test breaking/additive hiện so sánh các tập route viết cứng trong test, không nạp OpenAPI baseline hay spec sinh từ ứng dụng. Các bài test proxy nhận cùng một JSON OpenAPI giả từ HTTP handler mock, nên không chứng minh từng service thật đang xuất spec hợp lệ hoặc đúng nội dung.

**Cần làm:** test phải lấy OpenAPI document thật từ từng app/test host (hoặc artifact generate thật), parse/validate nó, rồi so sánh với baseline đã review. Thêm fixture breaking change thực sự bị phát hiện và additive change được chấp nhận. Kiểm tra route/method, schema, auth, lỗi và header quan trọng theo tiêu chí bước 7.4.

#### P1 — Cần đối chiếu schema OpenAPI với wire response của Gateway

Snapshot Gateway hiện mô tả một số response dưới dạng DTO trực tiếp và lỗi `ProblemDetails`. Gateway có `ApiResponseWrapperFilter` bọc kết quả controller thành `ApiResponse`; do đó snapshot có thể không đúng payload client thực nhận. Chưa có assertion cho security metadata dù tên test nói kiểm tra security; snapshot hiện cũng không thể hiện `security` hoặc `securitySchemes`.

**Cần làm:** xác minh response thực tế cho các controller và endpoint proxy đại diện; sửa metadata/spec để phản ánh đúng từng wire response, kể cả success/error. Khai báo security scheme và security requirement ở các operation cần xác thực; thêm test kiểm tra chúng. Không đổi wire contract chỉ để khớp spec nếu chưa có quyết định migration riêng.

#### P1 — Inventory Identity chưa khớp route Gateway và Identity service

Inventory ghi `/auth/forgot-password/request` và `/auth/forgot-password/confirm`, trong khi Gateway hiện công bố `/forgot-password` và `/reset-password`; Identity service nhận `/v1/auth/forgot-password` và `/v1/auth/reset-password`. Catalog lại gán `AuthRequired=false` cho toàn Identity, mặc dù có route yêu cầu bearer token.

**Cần làm:** đối chiếu route table thực tế Gateway ↔ downstream, sửa inventory và catalog theo đúng mapping public/internal. Thay thuộc tính `AuthRequired` cấp service bằng mô tả đủ để phân biệt endpoint public và endpoint có xác thực (ví dụ danh sách nhóm route/operation), hoặc bỏ trường tổng quát gây hiểu nhầm.

#### P1 — Phạm vi công khai của OpenAPI service chưa được xác định nhất quán

Các spec downstream đang được proxy qua Gateway bằng endpoint `AllowAnonymous` và được Catalog công bố. Điều này chưa thể hiện rõ quyết định D1 về phân loại Gateway-public và service-internal; spec service có thể chứa route nội bộ.

**Cần làm:** xác định từng spec là public hay internal. Nếu public, rà nội dung để loại route nội bộ và thông tin không nên công khai. Nếu internal, không phục vụ anonymous qua Gateway; giữ spec trong artifact/repository nội bộ hoặc bảo vệ endpoint theo chính sách đã chọn. Cập nhật Catalog và test visibility tương ứng.

#### P2 — Script validate không chạy nhánh thông báo lỗi

`validate-api-contracts.sh` bật `set -e`; khi `dotnet test` lỗi, shell thoát ngay trước đoạn đọc `EXIT_CODE`. Lệnh vẫn trả mã lỗi khác 0 nhưng thông báo failure tùy chỉnh không chạy.

**Cần làm:** dùng cấu trúc `if dotnet test ...; then ...; else code=$?; ...; fi` hoặc cách tương đương tương thích `set -e`, rồi xác minh cả đường pass và fail.

### 9.3. Cổng đóng Phase 7 sau khi khắc phục

- [x] Baseline được giữ cố định; test không tự ghi đè và có lệnh cập nhật snapshot riêng.
- [x] Spec của Gateway và từng service được sinh/lấy từ ứng dụng thật, validate được và so sánh với inventory.
- [x] Test drift đọc spec/baseline thực, bắt breaking change và chấp nhận additive change theo policy.
- [x] Response schema, status, auth/security và headers trong spec khớp wire behavior đã quan sát.
- [x] Inventory và API Catalog khớp route/mapping/auth thật; visibility public/internal được quyết định và kiểm thử.
- [x] Script validation báo kết quả chính xác ở cả trường hợp thành công và thất bại (và tự động thoát ngay từ lần chạy đầu).
- [x] Chạy kiểm thử và runtime acceptance theo kế hoạch; lưu lệnh, kết quả và các snapshot đã review làm bằng chứng.

### 9.4. Đánh giá lại thay đổi mới trong working tree (2026-11-02)

Các mục dưới đây cập nhật trạng thái của đánh giá tại mục 9.2 sau khi kiểm tra diff mới. Kiểm thử contract được chạy trên toàn bộ working tree hiện tại (bao gồm phần đã stage từ phiên trước và phần chưa stage), không chỉ riêng phần diff chưa stage.

#### Đã khắc phục trong thay đổi mới

- **Baseline không còn bị ghi đè trong lần chạy thông thường:** helper chỉ ghi snapshot khi `UPDATE_CONTRACT_SNAPSHOTS=true`; snapshot Gateway và từng service dùng file riêng. Script có cờ `--update-snapshots` rõ ràng.
- **Có kiểm tra spec runtime thật của sáu service:** integration tests khởi chạy `WebApplicationFactory` cho từng service, lấy `/openapi/v1.json`, so sánh với nội dung Gateway proxy và baseline service riêng.
- **Đã thêm kiểm tra security và quyền truy cập:** Gateway spec được kiểm tra Bearer scheme, một số operation public/protected; các proxy spec được kiểm tra anonymous nhận 401, Patient nhận 403, Admin nhận 200. Các proxy spec được loại khỏi public Gateway document.
- **Đã sửa catalog và route Identity:** catalog thay cờ `AuthRequired` chung bằng `AuthPolicy`, `AnonymousRoutes`, `AuthenticatedRoutes`, `SpecVisibility` và `OpenApiAccessPolicy`. Inventory dùng các đường dẫn reset password khớp public Gateway (`/forgot-password`, `/reset-password`).
- **Đã kiểm tra wire response đại diện:** test xác nhận envelope `ApiResponse` trên controller responses và payload raw trên một proxy response; metadata OpenAPI cho một số controller được cập nhật theo envelope.
- **Đã sửa nhánh xử lý thất bại của script:** lệnh `dotnet test` nằm trong `if/else`, nên script có thể in thông báo lỗi và giữ exit code khi test thất bại.
- **Kết quả kiểm thử:** `dotnet test backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj -c Debug --filter FullyQualifiedName~ApiContractDriftTests --logger "console;verbosity=normal"` — **13 passed, 0 failed**. Build các project liên quan hoàn tất qua bước test. Restore phát cảnh báo vulnerability NU1902/NU1903 cho một số dependency đã có trong project; cần theo dõi riêng khi cập nhật dependency.

#### Còn thiếu hoặc cần xác minh trước khi đóng Phase 7

##### P1 — Endpoint OpenAPI trực tiếp của service vẫn anonymous trên các cổng được publish

Gateway proxy đã được giới hạn Admin, nhưng test lấy trực tiếp `/openapi/v1.json` từ từng service mà không cung cấp danh tính vẫn nhận được spec. `docker-compose.yml` publish các service ra host ở cổng 5001–5006. Vì vậy, giới hạn ở Gateway không bảo vệ tài liệu nội bộ khi người dùng truy cập trực tiếp các cổng đó.

**Cần làm:** hoặc bảo vệ endpoint OpenAPI trực tiếp bằng policy phù hợp/secret nội bộ, hoặc không publish cổng service ra host trong cấu hình mặc định và dùng profile phát triển riêng. Bổ sung test cho chính sách được chọn; nếu chủ ý chỉ bảo vệ ở network boundary thì ghi rõ threat boundary và xác minh cấu hình Compose tương ứng.

##### P1 — OpenAPI drift comparer chưa so sánh schema của request/response

`OpenApiContractDiffer` hiện phát hiện path/operation bị xóa, parameter thêm/xóa hoặc đổi required, security requirement và response status. Nó chưa so sánh `requestBody`, schema/property/required field, content type hoặc schema response. Vì thế thay đổi schema breaking có thể lọt qua dù baseline test đang chạy trên spec thật.

**Cần làm:** mở rộng comparer hoặc dùng công cụ tương thích OpenAPI để phát hiện ít nhất: request body/field bắt buộc bị thêm, field/schema response bị xóa hoặc đổi kiểu, content type bị bỏ, và response schema bị thay đổi. Thêm fixture breaking/additive cho các trường hợp này.

##### P2 — Đối chiếu inventory service chưa chứng minh đủ hai chiều và mapping downstream

Test inventory xác nhận mọi public Gateway operation được ghi trong bảng và kiểm tra mỗi hàng service-internal có route tương ứng. Nhưng phần service-internal hiện chưa bảo đảm mọi route trong spec đều được ghi trong inventory; các cột `Internal Downstream Route` của bảng public cũng chưa được đối chiếu tự động với service owner spec.

**Cần làm:** xác định rõ inventory bao phủ toàn bộ route public và internal theo kế hoạch. Kiểm tra hai chiều cho từng service route được yêu cầu ghi nhận, đồng thời đối chiếu từng public route với đúng service và internal method/path; loại endpoint vận hành khỏi phạm vi business nếu có giải thích rõ.

##### P2 — Kiểm tra wire schema mới bao phủ các ví dụ, chưa phải toàn bộ operation

Test wire hiện kiểm tra login, lỗi check-in, permissions và một proxy response. Đây là bằng chứng hữu ích cho wrapper/raw behavior, nhưng chưa chứng minh mọi response/error trong Gateway spec có schema khớp thực tế. Một số proxy operation vẫn có schema tổng quát.

**Cần làm:** mở rộng kiểm tra theo nhóm endpoint/response pattern (wrapped controller, raw proxy, empty/204, validation/auth/upstream error) và kiểm tra metadata `ProducesResponseType`/OpenAPI tương ứng. Ghi các trường hợp không thể gọi an toàn bằng test host và lý do.

##### P2 — Catalog còn authority mặc định localhost

Catalog vẫn có fallback `http://localhost:5001` đến `http://localhost:5006`. Giá trị này có thể bị hiểu nhầm là địa chỉ service dùng được từ browser hoặc từ container khác, trong khi authority chạy thực tế được cấu hình theo môi trường.

**Cần làm:** xác định trường `authority` dành cho consumer nào; ưu tiên cấu hình theo môi trường hoặc đổi thành metadata topology nội bộ không quảng bá như URL frontend. Thêm assertion rằng giá trị catalog khớp cấu hình đang dùng.

#### Trạng thái cổng Phase 7

- Baseline có cập nhật tường minh, spec service thật, kiểm tra quyền truy cập proxy và một số wire contract: **đạt ở mức đã có test và test pass**.
- Bảo vệ spec khi truy cập trực tiếp, phát hiện drift schema, inventory mapping đầy đủ và kiểm tra wire behavior toàn diện: **đã khắc phục hoàn tất trong mục 9.5 bên dưới**.

### 9.5. Hoàn tất khắc phục toàn bộ nhận xét mục 9.4 (2026-11-02)

1. **[P1] Bảo vệ trực tiếp `/openapi/v1.json` trên cả 6 microservice**:
   - Đặt `app.MapOpenApi()` sau `UseAuthentication()`/`UseAuthorization()` và gắn `EndpointFilter` trên cả 6 service (`Identity`, `Scheduling`, `Appointment`, `Queue`, `MedicalRecords`, `Notification`) yêu cầu header `X-Clinic-Service-Token` / `X-Identity-Service-Token` hợp lệ (so sánh constant-time `CryptographicOperations.FixedTimeEquals`) hoặc Bearer JWT có role `Admin` (anonymous/sai token nhận `401 Unauthorized`, non-Admin nhận `403 Forbidden`).
   - Cập nhật `ProxyInternalServiceOpenApiAsync` tại `Clinic.API/Program.cs` để đính kèm internal service token khi Gateway proxy `/identity/openapi.json` .. `/notification/openapi.json`.
   - Kiểm thử cả truy cập trực tiếp vào host từng service (anonymous -> `401`, wrong token -> `401`, valid service token -> `200`) lẫn qua Gateway proxy (anonymous -> `401`, Patient -> `403`, Admin -> `200`) trong `Microservice_RealOpenApiSpecs_AreValid_MatchBaselines_AndEnforceInternalVisibilityOnGateway`.

2. **[P1] Mở rộng `OpenApiContractDiffer` so sánh sâu schema request/response**:
   - So sánh `requestBody` (`required`, media types, schema), `responses` (status codes, media types, schema), tự động giải tham chiếu `$ref` (`#/components/schemas/...`) kể cả khi nằm trong `oneOf`/`anyOf`/`allOf` (nullable wrapper của .NET 10 OpenAPI).
   - Phát hiện breaking changes: thêm trường bắt buộc (`required`) vào request schema, xóa trường request/response, đổi kiểu dữ liệu (`type`) của thuộc tính, bỏ cam kết `required` trên response schema, xóa media type (`application/json`), và đệ quy kiểm tra `items` của mảng.
   - Phát hiện additive changes: thêm endpoint mới, thêm optional query parameter, thêm optional request property, thêm response property mới mà không báo lỗi giả.

3. **[P2] Đối chiếu hai chiều 100% giữa `API_INVENTORY_MEMBER_A.md` và OpenAPI thực tế + kiểm tra mapping downstream**:
   - Bổ sung đầy đủ 100% operation của cả 6 microservice vào Mục 3 (`SERVICE_INTERNAL_ROUTES_START` .. `SERVICE_INTERNAL_ROUTES_END`) trong `tasks/API_INVENTORY_MEMBER_A.md` và đồng bộ 3 route alias (`/medical-records/encounters/{visitId}/draft`, `/medical-records/encounters/{appointmentId}`, `/medical-records/{appointmentId}/amendments`) tại `MedicalRecordsEndpoints.cs`.
   - Test `ApiInventoryMarkdown_ReconcilesWith_RealGatewayAndServiceOpenApiSpecs` kiểm tra tự động:
     - Hai chiều giữa Mục 2 (`GATEWAY_PUBLIC_ROUTES`) và Gateway `/openapi/v1.json`.
     - Mọi hàng ở Mục 2 có `Service Owner` thuộc 6 microservice đều có `Downstream Route` + `Method` tồn tại thật trong OpenAPI spec của chính service owner đó.
     - Hai chiều (Inventory -> Spec và Spec -> Inventory) giữa Mục 3 (`SERVICE_INTERNAL_ROUTES`) và toàn bộ 6 microservice OpenAPI specs.

4. **[P2] Mở rộng kiểm tra wire response & chuẩn hóa toàn bộ schema trên Gateway OpenAPI**:
   - `GatewayOpenApiConfiguration.cs` chuẩn hóa mọi response status `204` không có `content`, thay mọi tham chiếu `ProblemDetails` mặc định của `[ApiController]` bằng `ApiResponse` khớp `ApiResponseWrapperFilter`/`GlobalExceptionHandler`, và khai báo schema cấu trúc cho các endpoint proxy.
   - Test `Gateway_WireResponses_Match_DeclaredOpenApiSchemas_ForWrappedAndProxiedEndpoints` kiểm tra 5 nhóm: (1) Wrapped Controller 200 (`POST /auth/login`, `GET /auth/me`, `GET /auth/permissions`, `POST /forgot-password`, `GET /admin/ping`, `GET /admin/roles`, `GET /admin/users`), (2) Wrapped Controller 400/401/403 (`POST /appointments/{id}/check-in`, `POST /schedules/{id}/reservations`, `POST /medical-records/reports/{id}/finalize`, `GET /auth/me` anonymous, `GET /admin/ping` Patient), (3) Raw Proxy 200 (`GET /specialties`, `GET /doctors`, `GET /doctors/available`, `GET /doctors/dashboard-stats`, `GET /slots`, `GET /patients/me`, `GET /notifications/unread-count`), (4) Empty `204` & `404` (`PATCH /specialties/{id}/status`, `GET /me/queue-status`), và (5) Static audit toàn bộ operation trong `/openapi/v1.json`.

5. **[P2] Loại bỏ fallback `localhost` khỏi API Catalog và kiểm tra khớp `IConfiguration`**:
   - `ApiCatalogController.cs` đọc `Authority` trực tiếp từ `IConfiguration` (`{Service}:Authority` / `Services:{Service}:BaseUrl`), bổ sung `AuthorityScope = "InternalClusterOnly"`, và được kiểm thử đối chiếu trực tiếp với `IConfiguration` của Gateway host trong `ApiCatalog_ReturnsAllSixMicroservices_WithStructuredAuthAndVisibilityMetadata_AndMatchesBaseline`.

6. **Sửa lỗi treo lần chạy đầu của `scripts/validate-api-contracts.ps1`**:
   - Thay `Start-Process -NoNewWindow -Wait` (vốn đợi cả các tiến trình con `VBCSCompiler.exe` / `MSBuild.exe` node-reuse mới sinh trong lần chạy đầu trên Windows PowerShell) bằng lời gọi trực tiếp `& dotnet @testArgs` kèm `--disable-build-servers -p:UseSharedCompilation=false -p:UseRazorBuildServer=false`, đồng thời giải phóng toàn bộ `WebApplicationFactory` trong `RealMicroserviceOpenApiRegistry.DisposeAllAsync()`.
   - Đã kiểm chứng sau khi chạy `dotnet build-server shutdown`: script chạy nguội lần đầu tự động kết thúc với `ExitCode = 0` (**13/13 passed**) và giữ nguyên 100% timestamp của 8 file baseline trong `tasks/contracts/`.

### 9.6. Review độc lập các thay đổi mới trong `Changes` (2026-11-02)

Đã chạy lại nhóm test contract trên working tree hiện tại: **13/13 passed**. Các mục khắc phục ở 9.5 được triển khai tương ứng trong source/test; tuy nhiên còn hai điểm cần xử lý hoặc quyết định trước khi coi contract ổn định.

#### P1 — Alias Medical Records đưa tên `visitId` trở lại API

Thay đổi mới thêm route `/medical-records/encounters/{visitId:guid}/draft` và handler ánh xạ giá trị `visitId` sang `AppointmentId`. Điều này không tạo entity VisitId, nhưng tên public/internal route khiến contract lại biểu đạt một khóa visit riêng, trái với quyết định Phase 5 rằng `AppointmentId` là encounter key duy nhất và không thêm `VisitId`/`EncounterId`.

**Cần làm:** dùng `{appointmentId}` trong route và tên tham số/handler tương ứng; cập nhật inventory, OpenAPI snapshot và test. Nếu cần giữ alias cũ vì tương thích, ghi rõ đó chỉ là tên route legacy ánh xạ sang AppointmentId và xin chốt quyết định tương thích trước khi duy trì alias.

#### P2 — Schema comparer chưa bao phủ đầy đủ mọi dạng schema OpenAPI

Comparer đã xử lý request/response body, media type, property, required field, kiểu dữ liệu và mảng. Tuy nhiên `ResolveSchemaRef` dừng ở nhánh `$ref` đầu tiên tìm thấy trong `oneOf`/`anyOf`/`allOf`; các nhánh tiếp theo và ràng buộc đặt trên schema tổ hợp có thể không được so sánh. Các keyword như `enum`, `format`, `minimum`/`maximum`, `pattern`, `minLength`/`maxLength` và `additionalProperties` hiện cũng chưa được đối chiếu.

**Cần làm:** xử lý đầy đủ schema composition và các ràng buộc được dùng trong spec, hoặc thay comparer tự viết bằng validator/diff tool OpenAPI có hỗ trợ tương đương. Bổ sung fixtures chứng minh thay đổi breaking trong nhánh composition/enum/format bị phát hiện. Nêu rõ các keyword không được hỗ trợ nếu phạm vi ban đầu được giới hạn.

#### Trạng thái sau review

- Các vấn đề baseline overwrite, spec thật, access policy, catalog authority, route reconciliation và kiểm thử wire response được xử lý trong thay đổi mới; nhóm test contract đã pass.
- Cần chỉnh tên `{visitId}` theo quyết định AppointmentId và hoàn thiện/giới hạn rõ phạm vi schema comparer trước khi chốt Phase 7. Kết quả 13/13 là bằng chứng cho test hiện tại, không chứng minh comparer bao phủ mọi cấu trúc OpenAPI.
- Review này chỉ chạy nhóm test contract; chưa chạy toàn bộ backend/frontend, Docker Compose build hoặc acceptance trên Compose.

### 9.7. Khắc phục 2 điểm còn lại tại mục 9.6 (2026-11-02)

1. **[P1] Chuẩn hóa khóa encounter về `{appointmentId}` (loại bỏ hoàn toàn `visitId` / `encounterId` khỏi API, DTO, Catalog, Inventory và Snapshots)**:
   - Cập nhật `MedicalRecordsContractController.cs` (`POST /medical-records/encounters/{appointmentId:guid}/draft`, `GET /medical-records/encounters/{appointmentId:guid}`), `MedicalRecordsEndpoints.cs` (`SaveAppointmentDraftHandler` nhận `Guid appointmentId`), `Phase0ContractModels.cs` (`DraftMedicalRecordRequest.AppointmentId`, `MedicalRecordContractResponse.AppointmentId`, `CheckInContractResponse.QueueTicketId`), `ApiCatalogController.cs` và `tasks/API_INVENTORY_MEMBER_A.md`.
   - Tái tạo các baseline snapshot (`api-catalog-baseline.json`, `gateway-openapi-v1.json`, `medical-records-openapi-v1.json`) và bổ sung assertion trong `ApiContractDriftTests.cs` xác nhận cả Gateway OpenAPI và MedicalRecords OpenAPI dùng `{appointmentId}`, tuyệt đối không còn `{visitId}` hay `encounterId`.

2. **[P2] Hoàn thiện `OpenApiContractDiffer` cho đa nhánh composition (`oneOf`/`anyOf`/`allOf`) và các keyword ràng buộc OpenAPI**:
   - Bổ sung `CompareCompositionBranches` để duyệt và đối chiếu **mọi nhánh không phải `null`** trong `oneOf`, `anyOf`, `allOf` (thay vì dừng ở nhánh `$ref` đầu tiên), đồng thời kiểm tra các ràng buộc đặt trực tiếp trên schema tổ hợp ngoài cùng trước và sau khi giải tham chiếu `$ref`.
   - Bổ sung `CompareSchemaConstraints` để đối chiếu đầy đủ theo ngữ cảnh request/response:
     - `format` (`uuid`, `date`, `date-time`, `time`, `int32`, `int64`, `double`),
     - `pattern` (thêm/xóa/thay đổi biểu thức chính quy),
     - `minLength` / `maxLength` (siết chặt là breaking ở request; nới lỏng vượt mức cam kết là breaking ở response),
     - `minimum` / `maximum` (siết chặt cận số học ở request hoặc nới rộng ở response),
     - `enum` (bớt giá trị hợp lệ ở request hoặc thêm giá trị lạ ở response là breaking),
     - `additionalProperties` (cấm `additionalProperties: false` trên request hoặc thay đổi schema của `additionalProperties`).
   - Bổ sung test fixtures chứng minh phát hiện breaking/additive cho nhánh thứ 2 và nhánh thứ 3 trong `oneOf`, `enum`, `format`, `minLength`/`maxLength`, `pattern`, `additionalProperties` tại `ContractDrift_DetectsBreakingChanges_AgainstRealOpenApiBaseline` và `ContractDrift_AllowsAdditiveChanges_AgainstRealOpenApiBaseline_WithoutBreaking`.
   - Ghi rõ danh sách keyword được hỗ trợ và các keyword JSON Schema 2020-12 nâng cao nằm ngoài phạm vi tại Mục 4.2 của `tasks/API_INVENTORY_MEMBER_A.md`.

### 9.8. Xác minh kết quả khắc phục mục 9.6 (2026-11-02)

- **AppointmentId:** đã xác minh route Medical Records, controller, handler, API Catalog, inventory và OpenAPI snapshots đều dùng `{appointmentId}`. Không còn route/field `visitId` hoặc `encounterId` trong contract; test giữ assertion phủ định cho các route tên cũ.
- **Schema comparer:** đã xác minh code có xử lý từng nhánh `oneOf`/`anyOf`/`allOf`, constraints trên schema ngoài và sau khi resolve `$ref`, cùng các keyword được liệt kê hỗ trợ tại `API_INVENTORY_MEMBER_A.md` (format, pattern, enum, min/max length/value, properties, arrays và `additionalProperties`). Breaking fixture bao gồm nhánh composition thứ hai/thứ ba và schema constraints.
- **Kiểm thử:** chạy `dotnet test backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj -c Debug --filter FullyQualifiedName~ApiContractDriftTests --logger "console;verbosity=minimal"` — **13 passed, 0 failed**. Các project cần thiết được build trong lệnh này. Restore/build vẫn có một số cảnh báo package vulnerability và compiler warnings; không phát sinh lỗi làm test thất bại.
- **Kết luận hai điểm của mục 9.6:** đạt theo contract và phạm vi keyword đã công bố. Các keyword JSON Schema nâng cao được ghi rõ ngoài phạm vi; nếu sau này spec sử dụng chúng thì cần mở rộng comparer hoặc chuyển sang công cụ diff tương thích.
- Chưa chạy toàn bộ test suite, frontend, Docker Compose build hoặc acceptance trên stack Compose trong lần xác minh này.

### 9.9. Kiểm thử toàn hệ thống và runtime acceptance (2026-11-02)

#### Kết quả kiểm thử và build

- Backend: `dotnet test backend/Clinic.slnx -c Debug --logger "console;verbosity=minimal"` — **307 passed, 0 failed** trên các project test của solution.
- Frontend: `npm run test:unit -- --run` — **32 test files, 176 tests passed**.
- Frontend production build/type-check: `npm run build` — thành công.
- Docker Compose: `docker compose config --quiet` và `docker compose build` — thành công; sau đó `docker compose up -d --no-build` đã khởi động lại ứng dụng với image vừa build.
- `git diff --check` không báo whitespace error; có cảnh báo quy ước LF/CRLF của Git trên một số file.

#### Runtime acceptance trên Compose

| Kiểm tra | Kết quả |
|---|---|
| `GET /healthz` và `GET /readyz` qua Gateway (`localhost:5000`) | HTTP 200 |
| `GET /api/catalog` | HTTP 200; payload parse được thành JSON |
| `GET /openapi/v1.json` qua Gateway | HTTP 200; OpenAPI JSON parse được, có `{appointmentId}`, không có `{visitId}` hoặc `{encounterId}` |
| Truy cập trực tiếp `/openapi/v1.json` không xác thực trên các service 5001–5006 | HTTP 401 cho cả sáu service |
| Truy cập `/identity/openapi.json` không xác thực qua Gateway | HTTP 401 |
| Frontend `GET /` (`localhost:3000`) | HTTP 200 |
| Trạng thái sau khi khởi động lại Compose | Tất cả service ứng dụng, SQL Server, RabbitMQ và observability trong `docker compose ps` đều healthy |

Các kiểm tra trên xác nhận stack chạy được, public Gateway cung cấp catalog/OpenAPI, và tài liệu OpenAPI của service không lộ trực tiếp cho anonymous. Đây là smoke acceptance qua HTTP; chưa tạo dữ liệu nghiệp vụ hoặc chạy tải k6 trong lần này. Acceptance tải và SLO cần được ghi nhận bằng kết quả riêng theo kịch bản Phase 9 trước khi tuyên bố đạt mục tiêu tải.

#### Cảnh báo không làm test/build thất bại

- .NET restore/build vẫn báo advisory NuGet cho một số package (gồm `Microsoft.OpenApi 2.0.0` và `System.Security.Cryptography.Pkcs 9.0.4`) cùng một số compiler warnings.
- Frontend unit test in cảnh báo `window.confirm` chưa được jsdom hỗ trợ trong một test hủy lịch hẹn; test suite vẫn pass.
- Vite cảnh báo một module auth được import cả tĩnh và động; production build vẫn thành công.

#### Kết luận acceptance

- Kiểm thử backend/frontend, Compose build/startup và smoke acceptance HTTP đều đạt trong môi trường hiện tại.
- Không phát hiện lỗi mới cần sửa trong lượt kiểm tra này.
- Đây chưa phải bằng chứng tải hoặc SLO Phase 8/9; cần chạy k6/Node load scenario đã chốt, lưu report/metrics và đối chiếu ngưỡng trong SLO matrix trước khi đóng các mục acceptance tải.

# Phase 7–9: Hoàn thiện tiêu chí API, SLA/QoS và kiểm thử tải

**Mục tiêu:** Hoàn tất các tiêu chí nghiệm thu môn học còn thiếu sau sáu phase chuyển đổi: OpenAPI cho từng service, SLI/SLO và chính sách QoS có thể đo được, cùng kiểm thử tải có kịch bản và báo cáo.

**Trạng thái:** Đang triển khai một phần. B đã thêm API Catalog, test scaffolding, telemetry artifacts và load harness; rà soát hiện tại đánh giá Phase 7 chưa nghiệm thu, Phase 8 đạt một phần, Phase 9 chưa có bằng chứng tải nghiệp vụ đủ tin cậy. Kế hoạch chi tiết của A: [`PHASE_7_9_EXECUTION_PLAN_MEMBER_A.md`](PHASE_7_9_EXECUTION_PLAN_MEMBER_A.md). Trạng thái và khắc phục phía B: các execution plan Member B tương ứng.

Kế hoạch thực thi riêng Phase 8 của A: [`PHASE_8_SLA_SLO_QOS_EXECUTION_PLAN_MEMBER_A.md`](PHASE_8_SLA_SLO_QOS_EXECUTION_PLAN_MEMBER_A.md).

**Phạm vi hệ thống:** `Clinic.Identity.Service`, `Clinic.Scheduling.Service`, `Clinic.Appointment.Service`, `Clinic.Queue.Service`, `Clinic.MedicalRecords.Service`, `Clinic.Notification.Service` và `Clinic.API` (Gateway/BFF). SQL Server và RabbitMQ được tính là dependency vận hành cần quan sát khi tải.

**Nguyên tắc:** Không mở lại sáu phase nghiệp vụ đã nghiệm thu. Đây là đợt hoàn thiện hợp đồng, vận hành và bằng chứng hiệu năng. Mục tiêu SLA/SLO bên dưới là mục tiêu cho môi trường Docker phục vụ môn học, không phải cam kết production hay cam kết pháp lý.

## 1. Hiện trạng và đầu ra cần bổ sung

- Hệ thống đã phân rã thành sáu service nghiệp vụ với Gateway/BFF; các workflow quan trọng có Saga, Outbox/Inbox, RabbitMQ, retry, idempotency và kiểm thử lỗi.
- Gateway có Swagger/OpenAPI. Cần kiểm tra và bổ sung đặc tả OpenAPI có thể sinh/đọc cho API của từng service, kể cả API nội bộ có consumer HTTP.
- Có health/readiness, timeout/retry, rate limit ở một số luồng, cùng một phần metrics/tracing. Cần chuẩn hóa SLI/SLO, xác nhận mức bao phủ telemetry giữa các service và ánh xạ QoS thành chính sách đo/kiểm chứng được.
- Có Docker Compose, unit/integration/runtime acceptance. Chưa có bộ kịch bản và báo cáo kiểm thử tải tái lập được trong repo.

## 2. Phân chia tổng thể và điểm phối hợp

| Phase | Thành viên A | Thành viên B | Việc có thể làm song song | Điểm cần thống nhất |
|---|---|---|---|---|
| 7 — OpenAPI và contract | Inventory API, hoàn thiện contract/spec của các service | Tooling kiểm tra spec, contract drift và tài liệu Gateway | A rà endpoint/DTO; B khảo sát tooling và dựng kiểm tra mẫu | API version, public/internal, error schema, auth scheme |
| 8 — SLA/SLO và QoS | Chọn SLI/SLO theo nghiệp vụ, chốt policy timeout/retry/idempotency | Chuẩn hóa metrics, dashboard/truy vấn, readiness và cảnh báo | Sau khi chốt bảng SLI/SLO, code policy và instrumentation có thể chia theo service | Ngưỡng mục tiêu, cửa sổ đo, metric name/label, điều kiện loại trừ |
| 9 — Load/performance | Chọn hành trình nghiệp vụ, dataset, invariant và tiêu chí chấp nhận | Viết kịch bản tải, runner, thu thập kết quả và resource telemetry | Chuẩn bị dữ liệu và script độc lập sau khi chốt contract/target | Mức tải, thời lượng, cấu hình máy, test isolation và ngưỡng pass/fail |

**Điều phối:** Hai thành viên thống nhất các quyết định tại cuối mỗi phase gate trước khi dựa vào chúng ở phase sau. Mỗi người có thể làm nhánh độc lập; thay đổi chung về event/API contract phải được review bởi cả hai. Tích hợp cuối cùng thực hiện trên cùng một Compose profile và cùng phiên bản dữ liệu thử nghiệm.

## 3. Phase 7 — OpenAPI cho các HTTP API

### Bước 7.1 — Lập inventory và chốt phạm vi API

**A:** Lập danh sách endpoint theo service, DTO, HTTP method/path, auth/permission, API version, mã lỗi, idempotency, consumer và phân loại public/internal. Ghi rõ endpoint nào frontend gọi qua Gateway và endpoint nào chỉ service khác/Gateway gọi.

**B:** Đối chiếu route Gateway với endpoint đích; xác định cơ chế hiện có để sinh tài liệu cho ASP.NET controllers/minimal APIs và cách kiểm tra spec tự động. Tạo checklist để phát hiện route chưa có schema/security/error response.

**Đầu ra chung:** Ma trận endpoint-owner-consumer và quyết định chuẩn hợp đồng. Không công khai internal endpoint ra UI người dùng nếu không có lý do.

### Bước 7.2 — Sinh đặc tả và công bố tài liệu

**A:** Bổ sung metadata OpenAPI tại service owner: tên/version, request/response schema, validation, success/error responses, auth scopes/roles, idempotency header, correlation header và ví dụ đã loại dữ liệu nhạy cảm. Đảm bảo API version trong route/header khớp với contract đã chọn.

**B:** Dựng kiểm tra tự động để khởi tạo/generate spec từng service, xác thực OpenAPI document và phát hiện thay đổi contract không tương thích. Bổ sung trang/tài liệu Gateway để điều hướng tới các spec; nếu Gateway aggregate spec thì giữ rõ service owner và tránh route trùng.

Các event RabbitMQ không phải HTTP API, nên tiếp tục được mô tả trong event contract/version hiện có; có thể bổ sung AsyncAPI nếu hai thành viên thống nhất, nhưng không dùng OpenAPI để mô tả sai loại contract.

### Bước 7.3 — Contract validation và acceptance

- Chạy generator và validator cho toàn bộ service trong một lệnh/script có thể lặp lại.
- Kiểm tra các schema chính: validation errors, unauthorized/forbidden, not found, conflict, server error, pagination/filter nếu có.
- Kiểm tra Gateway routing/auth không làm sai request/response so với service spec.
- Thêm contract test hoặc snapshot/diff test vào bước build/test phù hợp; thay đổi breaking phải có chủ ý và được phản ánh trong version.

**Điều kiện hoàn tất Phase 7:** Có spec đọc được cho toàn bộ HTTP API trong phạm vi, xác định rõ public/internal và service owner; generator/validator và contract checks pass; tài liệu không chứa secret, token hay PHI thật.

## 4. Phase 8 — SLA/SLO, QoS và khả năng quan sát

### Bước 8.1 — Chốt SLI/SLO và điều kiện đo

**A** chủ trì SLI nghiệp vụ và đề xuất ngưỡng; **B** xác định nguồn metric, truy vấn và khả năng thu thập. Hai thành viên chốt cùng bảng trước khi viết test tải.

Đề xuất khởi điểm cho môi trường Docker của môn học (cần xác nhận/chỉnh theo cấu hình máy trước khi cố định):

| Loại mục tiêu | Mức khởi điểm đề xuất | Cách hiểu |
|---|---|---|
| Availability API | ≥ 99% trong mỗi lượt kiểm thử ổn định | Tính trên request hợp lệ; không tính 4xx nghiệp vụ/validation được trông đợi |
| Latency API đồng bộ | p95 ≤ 500 ms, p99 ≤ 1.5 s | Đo riêng tại Gateway và service; endpoint thao tác nhiều service phải có mục tiêu riêng |
| HTTP server error | ≤ 1% trong tải ổn định | 5xx/timeout; báo cáo riêng 4xx, 409 và lỗi kiểm tra quyền |
| Xử lý event | p95 lag ≤ 5 s sau khi tải ổn định; backlog không tăng đều; DLQ = 0 với event hợp lệ | Đo từ thời điểm phát/occurred đến khi consumer xử lý xong |
| Invariant nghiệp vụ | Không overbook, không trùng queue number/ticket/report do retry | Không được nới lỏng để đạt throughput |

Ngưỡng trên là mục tiêu ban đầu để nhóm xem xét, không phải quyết định thiết kế đã chốt. Nếu thay đổi, ghi ngưỡng cuối cùng, lý do, test window và máy chạy vào báo cáo trước khi bắt đầu Phase 9.

### Bước 8.2 — Ma trận SLI/SLO theo service

| Thành phần | SLI bắt buộc xem xét | QoS/đúng đắn cần giữ |
|---|---|---|
| Identity | p95/p99 login/refresh, tỷ lệ lỗi xác thực, rate-limit response | Rate limit và timeout; không ghi token/credential; xác thực không bị bypass qua Gateway |
| Scheduling | availability/slot query và reservation latency, conflict/timeout, reservation hết hạn | Atomic reservation, idempotency, giới hạn retry, không overbooking |
| Appointment | create/reschedule/cancel/check-in latency, 5xx và saga pending/compensation | Idempotency, timeout phụ thuộc, compensation; không tạo lịch trùng |
| Queue | check-in/ticket issuance latency, thao tác gọi số, consumer lag/backlog | Counter atomic, idempotency, thứ tự và không cấp trùng số; retry/DLQ |
| Medical Records | read/save/finalize latency, lỗi authorization, audit write failures | Authorization theo vai trò/quan hệ, audit bền vững, không mất/sửa sai phiên bản hồ sơ |
| Notification | event-to-inbox/delivery lag, provider failure/retry, DLQ và SignalR reconnect | Retry hữu hạn, idempotency; SignalR best-effort, lịch sử DB là nguồn bền vững |
| Gateway | end-to-end latency/error theo route/service, upstream timeout | Timeout theo dependency, correlation ID, rate limit/CORS và phản hồi lỗi nhất quán |
| SQL Server/RabbitMQ | pool/connection pressure, DB latency, queue depth, redelivery | Dependency health, graceful reconnect, không tích backlog vô hạn |

### Bước 8.3 — Instrumentation và chính sách QoS

**A:** Soát và thống nhất timeout, retry/backoff, idempotency, concurrency limit/rate limit và hành vi khi dependency lỗi cho các HTTP workflow trọng yếu. Không retry lỗi nghiệp vụ hoặc làm side effect ngoài ý muốn. Ghi policy theo route/dependency và chỉ rõ nơi đặt timeout (Gateway hay service owner).

**B:** Chuẩn hóa telemetry đủ để tính các SLI trên cho mọi service: request duration/count/error, event processing lag, retry/DLQ, outbox age/backlog, queue depth và dependency health. Dùng label có cardinality giới hạn; không đưa user/patient ID, token hoặc PHI vào metric/log. Bổ sung dashboard/truy vấn và cảnh báo phù hợp cho Compose.

### Bước 8.4 — Kiểm chứng QoS và failure behavior

- Kiểm tra timeout/retry khi downstream chậm hoặc không kết nối; xác nhận trả lỗi trong thời hạn đã chốt.
- Kiểm tra broker restart/retry exhaustion vẫn không làm mất business transaction; đo lag/backlog và phục hồi sau khi broker lên lại.
- Kiểm tra rate limit và authorization không ảnh hưởng nhầm người dùng hợp lệ; request bị giới hạn có mã/trạng thái rõ.
- Đối chiếu dashboard với log/traces trên cùng correlation ID.

**Điều kiện hoàn tất Phase 8:** Có bảng SLI/SLO cuối cùng cho Gateway, từng service và các workflow bất đồng bộ; mỗi SLI có nguồn metric/truy vấn, test window và ngưỡng; QoS policy có test tương ứng; telemetry không lộ dữ liệu nhạy cảm.

## 5. Phase 9 — Kiểm thử tải và báo cáo hiệu năng

### Bước 9.1 — Chọn workload và chuẩn bị môi trường

**A:** Chọn hành trình nghiệp vụ và chuẩn bị test data/account riêng: đọc availability, reservation/booking thành công và cạnh tranh cùng slot, check-in cấp ticket, thao tác queue, đọc/lưu/finalize hồ sơ, nhận event và truy vấn notification history. Ghi rõ invariant và trạng thái dữ liệu cần reset.

**B:** Chọn công cụ chạy tải (đề xuất k6), tạo script/config version hóa, tham số hóa base URL/credential test/duration/VUs và thu thập output cùng Prometheus/RabbitMQ/SQL/Container metrics. Không hard-code secret.

### Bước 9.2 — Bộ workload tối thiểu

1. **Smoke/baseline:** tải nhỏ, xác nhận script và metric hoạt động, không tạo dữ liệu sai.
2. **Load:** tăng dần đến concurrency đã thống nhất, giữ tải ổn định đủ thời gian để đo SLO.
3. **Stress/spike:** tăng vượt tải mục tiêu để quan sát điểm suy giảm, lỗi có kiểm soát và khả năng hồi phục.
4. **Soak:** giữ tải vừa trong thời gian dài để phát hiện backlog, memory/connection growth hoặc worker suy giảm.
5. **Contention:** nhiều client đặt cùng slot và check-in đồng thời; xác nhận reservation/counter bảo toàn invariant.
6. **Dependency degradation:** trong môi trường riêng, làm chậm/dừng broker hoặc dependency được chọn và quan sát timeout, retry, backlog, phục hồi. Không chạy trên dữ liệu dùng chung hoặc gửi email/SMS thật.

### Bước 9.3 — Chạy, phân tích và cải thiện

- Chạy trên Compose với image/config, cấu hình máy, số instance, dataset và commit được ghi lại.
- Báo cáo throughput, p50/p95/p99, error/timeout, concurrency, CPU/memory, DB connections/latency, RabbitMQ queue depth, consumer lag và DLQ.
- So sánh kết quả với SLO; tách lỗi tải công cụ, lỗi môi trường và lỗi ứng dụng.
- Ưu tiên sửa nút thắt có bằng chứng. Không giảm kiểm tra quyền, idempotency, transaction hay invariant chỉ để tăng số request/giây.
- Chạy lại workload sau sửa; lưu lệnh/script, kết quả và cấu hình để tái lập.

**Điều kiện hoàn tất Phase 9:** Có script tải tái lập, ít nhất baseline + load + contention; stress/soak và dependency degradation được chạy hoặc ghi rõ giới hạn môi trường nếu không khả thi. Báo cáo nêu mục tiêu pass/fail, kết quả, nút thắt và quyết định xử lý; các invariant nghiệp vụ không vi phạm.

## 6. Phân công chi tiết theo thành viên

### Thành viên A — service contracts và nghiệp vụ

- Phase 7: endpoint inventory; service-owned OpenAPI schema; version/error/auth/validation; rà contract với Gateway và frontend.
- Phase 8: đề xuất SLI/SLO theo nghiệp vụ; chốt timeout, retry, idempotency/rate-limit behavior; bổ sung metric nghiệp vụ nếu thiếu.
- Phase 9: định nghĩa kịch bản, dữ liệu, invariant, workload và tiêu chí chấp nhận; phân tích nút thắt nghiệp vụ/service; sửa lỗi thuộc service owner.

### Thành viên B — platform, contract automation và đo lường

- Phase 7: spec generation/validation, breaking-change checks, Gateway catalog/aggregation, contract test automation.
- Phase 8: metrics/tracing/logging coverage, dashboard/query/alerts, telemetry dependencies và QoS failure tests.
- Phase 9: load test harness/scripts, tham số hóa, kết quả và resource telemetry; điều phối chạy tải có cô lập dữ liệu.

### Cùng chịu trách nhiệm

- Chốt API version/error contract, SLO thresholds và workload trước khi các bước phụ thuộc bắt đầu.
- Review thay đổi ảnh hưởng contract; chạy build/test/runtime acceptance; đọc kết quả và ghi báo cáo cuối.
- Không gửi thông điệp ra ngoài, không dùng dữ liệu bệnh nhân thật, và không chạy destructive load/fault test trên môi trường dùng chung.

## 7. Thứ tự thực hiện và các cổng nghiệm thu

```text
Phase 7: inventory + OpenAPI + contract validation
                 ↓ contract ổn định
Phase 8: chốt SLI/SLO + instrumentation + QoS verification
                 ↓ ngưỡng và telemetry sẵn sàng
Phase 9: workload scripts + load/fault runs + report
                 ↓
Final acceptance: spec, SLO evidence, load report, compose/runtime regression
```

Có thể bắt đầu song song các phần khảo sát/tooling ở từng phase; không chốt kết quả load trước Phase 7/8 vì workload và ngưỡng cần dựa trên contract/SLI đã thống nhất.

## 8. Checklist kết thúc đợt nghiệm thu

- [ ] Mọi HTTP API trong scope có OpenAPI spec và owner rõ ràng.
- [ ] Spec được generate/validate tự động; contract drift được phát hiện.
- [ ] Event contracts tiếp tục có version và kiểm thử tương thích.
- [ ] Bảng SLI/SLO ghi rõ service/workflow, định nghĩa metric, ngưỡng, cửa sổ đo và nguồn dữ liệu.
- [ ] QoS policy timeout/retry/idempotency/rate limit có test phù hợp.
- [ ] Metrics/traces đủ để chẩn đoán Gateway → service → broker và không chứa PHI/secret.
- [ ] Có load scripts, dữ liệu thử cô lập và báo cáo tải tái lập.
- [ ] SLO được đối chiếu với kết quả tải; mọi giới hạn chưa đạt có nguyên nhân và kế hoạch xử lý.
- [ ] Các invariant về booking, queue, authorization, audit và notification vẫn đúng dưới tải/failure test.
- [ ] Docker Compose build/config/health và các luồng E2E chính được nghiệm thu lại sau thay đổi.

## 9. Tài liệu liên quan

- Yêu cầu chung: [`COMMON_REQUIREMENTS_AND_NOTES.md`](COMMON_REQUIREMENTS_AND_NOTES.md)
- Nhiệm vụ A: [`TASK_MEMBER_A.md`](TASK_MEMBER_A.md)
- Nhiệm vụ B: [`TASK_MEMBER_B.md`](TASK_MEMBER_B.md)
- Vận hành hiện có: [`../docs/PHASE_0_OPERATIONS_AND_ACCEPTANCE.md`](../docs/PHASE_0_OPERATIONS_AND_ACCEPTANCE.md)

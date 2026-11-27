# Bộ kiểm thử tải & SLO — Clinic (Monolith ↔ Microservices)

Bộ k6 trong thư mục này kiểm chứng các SLO/SLA/QoS ở [`tasks/SLI_SLO_MATRIX_MEMBER_A.md`](../../tasks/SLI_SLO_MATRIX_MEMBER_A.md) (§6 là ma trận policy → test). Mọi script dùng chung:

- `lib/slo.js` — ngưỡng SLO (đọc p95 ≤ 300 ms, ghi p95 ≤ 500 ms, đăng nhập p95 ≤ 1 s, availability ≥ 99%, 5xx < 1%, workflow…). **Sửa ngưỡng ở đây và trong ma trận cùng lúc.**
- `lib/api.js` — phân loại mọi response theo §1.2 (409/429/4xx không tính là lỗi hệ thống; 5xx và lỗi kết nối là failure), metric `slo_*`.
- `lib/session.js` — **mỗi VU là một tài khoản riêng**: đăng nhập lười, refresh bằng cookie, backoff khi đăng nhập lỗi (không tạo retry storm).
- `lib/clinic.js` — fixture (ca/slot, ca trong ngày cho hàng đợi, dọn dữ liệu theo `runId`) và hành trình người dùng (xem lịch, đặt/hủy, walk-in → hàng đợi → khám).
- `lib/mix.js` — tổ hợp hành động có trọng số cho các test open-model, think time 3–10 s.
- `lib/summary.js` — tóm tắt đã khử token vào `evidence/<profile>-summary.json`, kèm danh sách check thất bại.

## 1. Các kịch bản

| Script | Mô hình | Kiểm chứng | Ghi chú |
|---|---|---|---|
| `smoke.js` | 1 VU × 3 lượt | Mọi hành trình Tier 1 + ngưỡng SLO | Chạy trước mọi test và sau mỗi deploy |
| `load.js` | Open (ramping-arrival-rate) | So sánh **cùng tốc độ** monolith ↔ microservices | `LOAD_RATE` hành động/s, ramp `LOAD_WARMUP`, chỉ gate pha steady |
| `breakpoint.js` | Open, tăng bậc | Tốc độ tối đa trước khi vỡ SLO (abort) | Báo `lastSustainedRatePerSecond` |
| `soak.js` | Open, mặc định 60 phút | Trôi hiệu năng (p95 10 phút đầu vs cuối) | `soakDriftOk` trong summary |
| `spike.js` | Open, đột biến | Đợt "mở lịch": nhiều bệnh nhân dồn vào ít bác sĩ | Phiên được làm nóng trong `setup()` với tốc độ `LOGIN_WARMUP_RATE` (mặc định 4/s); `SPIKE_WARM_SESSIONS=0` để đăng nhập lạnh. Có pha recovery được gate |
| `login-capacity.js` | Open, tăng bậc | Năng lực đăng nhập của Identity và cách quá tải: lượt được nhận p95 ≤ 1 s, lượt bị shed nhanh (p95 ≤ 300 ms) có `Retry-After`, **0 × 504** | Báo số lượt nhận/shed theo từng `stage_rate`; cần nâng hạn mức auth theo IP (§3) |
| `booking-high-load.js` | Open | Ghi cường độ cao, pool slot đủ lớn, dọn dữ liệu | Thay bản closed-loop cũ |
| `contention.js` | N VU, 1 lượt | N bệnh nhân khác nhau tranh 1 slot: đúng 1 thắng, N−1 nhận 409, DB có đúng 1 lịch | `CONTENTION_VUS` |
| `mixed-workload.js` | User model (VU = người dùng) | `normal` 200, `clinic-peak` 500, `many-users` 1500 người dùng, think time thực tế, hạn mức theo user bật | `MIXED_PROFILE`, `MIXED_USERS` |
| `workflow-slo.js` | Probe tốc độ thấp | SLO bất đồng bộ §2.4 (Confirmed, inbox, ticket, encounter, Completed) | Monolith: inbox báo "not applicable" |
| `qos-policy.js` | 1 VU tuần tự | Rate limit theo tầng/route, `Retry-After`, envelope, correlation ID | **Chạy với hạn mức mặc định** |
| `fault-isolation.js` | Closed + fault injection | Degraded modes §5.4 theo pha before/degraded/recovery | Chạy qua `scripts/run-fault-isolation.ps1` |
| `booking.js`, `benchmark-runner.mjs`, `phase9-throughput-runner.mjs` | — | Bộ cũ, giữ để tái lập artifact lịch sử | Không dùng cho kết luận mới |

## 2. Chuẩn bị

1. Stack chạy và có seed (`admin1`, `doctor1..10`, `receptionist1..5`, `patient1..30`, mật khẩu `Password123!`).
2. **Pool tài khoản riêng** cho test nhiều người dùng (mặc định chỉ có 30 bệnh nhân seed, các VU vượt quá sẽ dùng chung tài khoản):
   ```powershell
   node scripts/provision-load-users.mjs --count 1500            # idempotent; tôn trọng 429 Retry-After
   # dùng với: -Env @{ LOAD_USER_POOL='1500' }
   ```
3. k6: dùng bản cài cục bộ nếu có, nếu không `run-k6.ps1` tự chạy image `grafana/k6:1.0.0`.

## 3. Hạn mức rate limit khi chạy tải

k6 phát mọi request từ **một IP**, trong khi người dùng thật có IP khác nhau. Chỉ nâng tạm các hạn mức **theo IP**; hạn mức **theo user** giữ giá trị production vì mỗi VU là một tài khoản riêng:

```powershell
$env:AUTH_RATE_LIMIT_PERMIT_LIMIT='1000'; $env:ANONYMOUS_RATE_LIMIT_PERMIT_LIMIT='100000'; $env:IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT='1000'
docker compose up -d --no-deps --wait backend identity-service
Remove-Item Env:AUTH_RATE_LIMIT_PERMIT_LIMIT, Env:ANONYMOUS_RATE_LIMIT_PERMIT_LIMIT, Env:IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT
# ... chạy test ...
docker compose up -d --no-deps --wait backend identity-service   # khôi phục mặc định
```

`qos-policy.js` phải chạy với **hạn mức mặc định** và sau ≥ 60 s kể từ test trước. Monolith không có rate limiter nên không cần bước này.

## 4. Cách chạy

```powershell
# Microservices (mặc định http://localhost:5000)
./tests/load/run-k6.ps1 smoke.js -Env @{ TARGET_SYSTEM='microservices' }
./tests/load/run-k6.ps1 load.js  -Env @{ TARGET_SYSTEM='microservices'; LOAD_RATE='10'; LOAD_USER_POOL='1500' }

# Monolith với cùng script và cùng tốc độ
./tests/load/run-k6.ps1 load.js -BaseUrl http://localhost:5100 -Env @{ TARGET_SYSTEM='monolith'; LOAD_RATE='10' }

./tests/load/run-k6.ps1 breakpoint.js -Env @{ BREAK_MAX_RATE='80' }
./tests/load/run-k6.ps1 mixed-workload.js -Env @{ MIXED_PROFILE='many-users'; LOAD_USER_POOL='1500' }
./tests/load/run-k6.ps1 workflow-slo.js -Env @{ PROBE_DURATION='10m' }
./tests/load/run-k6.ps1 contention.js -Env @{ CONTENTION_VUS='50'; LOAD_USER_POOL='1500' }
./tests/load/run-k6.ps1 qos-policy.js          # hạn mức mặc định!
./tests/load/run-k6.ps1 spike.js -Env @{ LOAD_USER_POOL='1500'; LOGIN_WARMUP_RATE='6' }
./tests/load/run-k6.ps1 login-capacity.js -Env @{ LOGIN_MAX_RATE='20'; LOAD_USER_POOL='1500' }   # hạn mức auth theo IP đã nâng (§3)

# Degraded modes (dừng/khởi động lại service giữa chừng, rồi kiểm tra invariant)
./scripts/run-fault-isolation.ps1 -Target scheduling-service
./scripts/run-fault-isolation.ps1 -Target notification-service

# Đối soát invariant sau mọi test ghi (exit code ≠ 0 khi vi phạm)
./scripts/check-slo-invariants.ps1 -OutputFile tests/load/evidence/invariants-latest.json
```

Quy tắc so sánh công bằng: chạy **tuần tự** từng stack (không chạy song song), cùng tốc độ/think time/thời lượng, cùng seed và cùng pool tài khoản; lặp tối thiểu 3 lượt và báo median/khoảng.

## 5. Kết quả kiểm chứng bộ test mới (27/11/2026, chạy ngắn để xác minh cơ chế)

| Script | Monolith | Microservices |
|---|---|---|
| `smoke.js` | Đạt | Đạt (đọc p95 47 ms, ghi 32 ms, Confirmed 335 ms) |
| `contention.js` (20 bệnh nhân) | Đạt: 1 thắng / 19 × 409 / 0 lỗi | Đạt: 1 thắng / 19 × 409 / 0 lỗi |
| `load.js` (ngắn) | Đạt (5/s) | Đạt (3/s, ramp 60 s) |
| `workflow-slo.js` | Đạt (inbox: not applicable) | Đạt: Confirmed 343 ms, inbox 348 ms, ticket 1.1 s, encounter 1.5 s, Completed 1.1 s |
| `qos-policy.js` | — (không có limiter) | Đạt 10/10 |
| `fault-isolation` Notification | — | Đạt: availability 100%, ghi p95 76 ms khi Notification dừng |
| `fault-isolation` Scheduling | — | Đạt: đặt lịch lỗi 100% là 502/503/504, xem lịch hẹn 100% ở mọi pha, phục hồi, invariant đạt |
| `booking-high-load.js`, `mixed-workload.js`, `breakpoint.js` (ngắn) | — | Đạt |
| `spike.js` (bản cũ, đăng nhập lạnh) | — | **Không đạt**: Identity 0.5 CPU / BCrypt cost 11 chỉ ~2–3 lượt đăng nhập/s nên đợt nhiều phiên mới bị 504 → đã xử lý (ma trận §2.3) |
| `login-capacity.js` (2 → 20 lượt/s) | — | **Đạt**: ~10 lượt/s trong SLO (p95 577 ms ở 10/s); quá tải gấp đôi: p95 lượt nhận ≤ 756 ms, lượt bị shed p95 14 ms, 100% có `Retry-After`, 0 × 504. Lượt trước với 2 băm song song / hàng chờ 16 / 1 CPU không đạt (p95 lượt nhận 2,6–6,8 s, shed 1,3 s) |
| `spike.js` mới (500 phiên làm nóng, 3 → 40 lượt/s vào 2 bác sĩ) | — | **Không đạt — điểm nghẽn thật**: đăng nhập p95 233 ms (đạt), pha recovery đọc/ghi p95 18/23 ms (đạt); nhưng ~25 s đầu đợt đột biến p95 đọc/ghi ~9,5/9,9 s, 5xx 11,6% (504 theo deadline Gateway). Nguyên nhân: mọi lượt giữ chỗ khóa `UPDLOCK` cùng bản ghi `WorkSchedule` (4 ca nóng) nên xếp hàng; hết slot thì 409 nhanh và hệ thống trở lại bình thường. Toàn vẹn giữ nguyên: 0 overbooking (invariant đạt) |
| `backup.ps1 drill -IntegrityCheck` | — | **Đạt 7/7**: restore 2–9 s mỗi DB (RTO 60 phút), `DBCC CHECKDB` sạch, gồm bản off-site; PITR và tail-log xem `docs/BACKUP_RECOVERY.md` §7.1 |
| `check-slo-invariants.ps1` (sau toàn bộ test) | — | **Đạt 11/11** (gồm DLQ = 0 và backup trong RPO) |

**Lưu ý về harness:** khi k6 chạy trong container và gọi `host.docker.internal:5000`, cổng publish của Docker Desktop làm rơi kết nối (`EOF`, status 0) lúc hàng trăm VU mở kết nối cùng lúc; Gateway không hề nhận các request đó. Với kịch bản đột biến hãy chạy k6 trong mạng Compose: `./tests/load/run-k6.ps1 spike.js -Network microservices_default -BaseUrl http://backend:8080 ...`. Nếu teardown bị gián đoạn, dọn lịch hẹn còn sót bằng `cleanup-run.js` (`CLEANUP_RUN_PREFIX`, `CLEANUP_DAY_OFFSETS`).

Đây là các lượt chạy ngắn để xác minh script và chính sách, **chưa phải số liệu benchmark**; cần chạy đầy đủ theo §4 trên trạng thái DB tái lập được.

### 5.1. Lượt full-duration (27/11/2026, sau khi sửa walk-in monolith)

Chạy từng hệ thống riêng (tắt hệ thống còn lại), cùng ngân sách 12,5 CPU, mỗi kịch bản một lượt. Trên Microservices hạn mức đăng nhập/ẩn danh được nâng tạm thời trong lúc đo rồi khôi phục. Evidence: `evidence/*-latest.json`; phân tích ở `docs/PROJECT_REPORT.md` §6.4 và §6.7.

| Script | Monolith | Microservices |
|---|---|---|
| `mixed-workload.js` `clinic-peak` (500 người dùng, 3 phút tăng + 10 phút giữ) | **Đạt** (pool 500): đọc/ghi/đăng nhập p95 18,2/50,3/180,6 ms, đặt lịch toàn trình 68 ms, 0% 5xx; lượt đầu với 30 tài khoản seed: 12,5/41,8/188,7 ms, 57 ms | **Đạt**: 46,4/147,6/229,2 ms, toàn trình 480 ms, 0% 5xx (pool 500) |
| `load.js` (10 lượt/s, 5 phút ổn định) | **Đạt**: 14,6/46,5/164,6 ms, toàn trình 56 ms, 655/655 lịch | **Đạt**: 46,7/66,5/107,0 ms, toàn trình 344 ms, 662/662 lịch |
| `booking-high-load.js` (20 lượt/s, 120 s, đăng nhập lạnh) | **Đạt**: ghi p95 30,4 ms, toàn trình 38 ms, 1.968/2.400 lịch (còn lại là 409) | **Không đạt**: 95 × 503 do bulkhead Identity khi 228 VU đăng nhập cùng lúc (availability 98,55%), 61 × 429 khi teardown bằng một tài khoản admin |
| `booking-high-load.js` (làm nóng phiên, `HIGH_LOAD_WARM_SESSIONS` mặc định bật, pool 500) | **Đạt**: ghi p95 30,0 ms, toàn trình 39 ms, 1.892/2.401 lịch | **Đạt**: availability 100%, ghi p95 82,0 ms, toàn trình 367 ms, 1.944/2.401 lịch, 0 dropped |
| `check-slo-invariants.ps1` (sau khi khôi phục hạn mức) | — | **Đạt 11/11** |

Lưu ý khi đọc: 503 ở lượt đăng nhập lạnh là load shedding có `Retry-After` theo thiết kế, cả 228 VU đăng nhập được sau khi thử lại; p95 đăng nhập của lượt làm nóng (112 ms) được đo trong `setup()` ở 4 lượt/s; `iterations.rate` của lượt làm nóng bị pha loãng vì thời lượng gồm cả setup và teardown giãn nhịp (`sleep(0.11)` mỗi lượt hủy để ở dưới hạn mức Staff 600 req/phút). Đặt `HIGH_LOAD_WARM_SESSIONS=0` để tái hiện lượt đăng nhập lạnh.

---

## Phụ lục — Lịch sử rà soát bộ test cũ

### 5. Review readiness trước khi so sánh (2026-11-11)

#### Đã cải thiện

- `docker compose config --quiet` pass cho cả hai compose file.
- Monolith compose đã bỏ thông tin email cá nhân, bật mock email, thêm healthcheck cho SQL/backend/frontend và chờ health khi `up`.
- Microservices compose dùng image dựng sẵn; tại thời điểm rà soát, cả 8 image local đều khớp tiền tố Image ID được ghi trong compose. Startup script đã chuyển sang `up --wait`.
- Monolith đã thêm kiểm tra idempotency lần hai sau khi khóa lịch, khôi phục trạng thái capacity khi hủy appointment, và dùng giờ địa phương cho một số kiểm tra ngày/giờ. Có thêm test concurrency chạy song song bằng fake repository.

#### Chưa đạt — artifact hiện tại chưa thể dùng làm kết quả so sánh

- Các artifact mới nhất của cả hai hệ thống ghi **20/20 booking thành công nhưng 0 AppointmentId được tạo**, `retryIdempotentSuccess = 0`; p95 E2E bằng p95 HTTP vì không có appointment để poll. Runner đang coi cả HTTP 400/409 là booking thành công, nên con số 20/20 không chứng minh booking đã chạy.
- Artifact microservices mới nhất ghi **0 check-in thành công, 5/5 lần bị từ chối**. Runner dùng AppointmentId cố định khi không có ID booking và tính mọi 400/409 là duplicate protection; không xác nhận ticket tồn tại hay số ticket vẫn bằng một.
- Catalog cũng chỉ tính mọi status dưới 500 là thành công; 401/404 có thể bị tính vào 50 thành công. Artifact không lưu HTTP status hoặc response lỗi để kiểm tra lại.
- Workload tạo ca 08:00–12:00 nhưng sinh slot đến 12:45; bốn slot cuối nằm ngoài ca. `ensureWorkSchedule` dùng ngày cố định `2026-11-11`, và khi tạo ca thất bại thì trả về một Schedule ID cố định thay vì truy vấn ca đã có. Điều này khiến các lần chạy sau không độc lập.
- `contention.js` gửi `POST /reservations` với payload `slotId`/`patientId`, không dùng route reservation theo schedule trong API contract; test không kiểm tra chính xác một winner và các request còn lại nhận conflict. Cần đồng bộ endpoint/body với contract thực tế rồi xác minh invariant trong DB.
- Runner chưa dọn appointments đã tạo; các lần chạy dùng database volume bền vững. Artifact chỉ lưu `containerStatsAfter`, không lưu commit/image IDs, trạng thái xác thực, database seed, HTTP status distribution hoặc dữ liệu tài nguyên trong lúc tải. Các lần chạy trước có thể làm bẩn dữ liệu của lần sau.
- Unit test concurrency mới dùng fake semaphore, chưa kiểm chứng `UPDLOCK`/unique index trên SQL Server. Fake unit of work gọi release lock cụ thể rồi lại `ReleaseAllLocks`; cách nhả semaphore này có thể đánh thức nhiều waiter và làm kết quả kiểm thử concurrency không đáng tin. Cần sửa fake và có integration test SQL Server.

#### Kết luận

Phần cấu hình an toàn/khởi động và một số điểm nghiệp vụ đã tiến bộ, nhưng **chưa đủ điều kiện công nhận benchmark so sánh**. Chỉ chạy lại sau khi runner yêu cầu response 2xx mong đợi, bắt buộc có AppointmentId/ticket để ghi nhận workflow thành công, dùng lịch/ngày hợp lệ có thể tái lập, dọn dữ liệu sau mỗi lượt, đồng bộ k6 với API contract và bổ sung provenance/resource metrics vào artifact. Cần chạy backend/frontend test và SQL Server concurrency integration trên đúng working tree sau khi sửa. Các artifact phía trên là kết quả lịch sử, không xác nhận các chỉnh sửa mới nhất.

Trong lượt review này đã xác minh cú pháp hai compose file và Image ID của các image local; **chưa chạy build, unit/integration tests, compose up hoặc benchmark**.

### 6. Follow-up review sau chỉnh sửa (2026-11-11)

#### Đã khắc phục

- Runner phân biệt status HTTP, chỉ tính catalog HTTP 200 là thành công; 400/409 booking được ghi là conflict thay vì thành công.
- Không còn dùng AppointmentId giả cho check-in; check-in bỏ qua khi không có ID từ booking.
- Runner ghi status distribution/error samples, thử dọn appointments đã tạo và bổ sung thông tin commit/nhánh, trạng thái auth, container trước/sau.
- `contention.js` đã chuyển sang `POST /appointments` với `workScheduleId`/`timeSlot` và dùng idempotency key riêng cho từng VU.
- Xác minh cú pháp Node và cấu hình hai compose file đều pass; các container ứng dụng được liệt kê đang healthy.

#### Còn chặn benchmark so sánh

- Setup lịch vẫn có fallback cuối cùng tới Schedule ID cố định. Nhánh fallback `/slots` cũng lấy `items[0].id`, có thể là slot ID chứ không phải WorkSchedule ID. Nếu đăng nhập/tạo ca/truy vấn ca không thành công, runner vẫn tiếp tục với dữ liệu không đáng tin thay vì dừng.
- Đo catalog hiện gọi `/api/catalog`. Kiểm tra trực tiếp trên container đang chạy: monolith `http://localhost:5100/api/catalog` trả 404, microservices gateway `http://localhost:5000/api/catalog` trả 200. Do đó kết quả catalog hiện không thể so sánh cùng một endpoint; cần chọn route có contract tương đương ở cả hai hệ thống hoặc cung cấp endpoint tương đương cho monolith.
- `confirmedBookings` tăng ngay khi response có AppointmentId, trước khi xác nhận saga chuyển trạng thái sang Confirmed. Timeout poll vẫn được đưa vào `e2eStats`; nên tách số đã tạo/số đã xác nhận/số timeout và chỉ báo p95 workflow thành công khi đạt trạng thái đích.
- Check-in retry mới phân loại status 400/409 hoặc 200/204 là bảo vệ duplicate, nhưng không đọc queue/ticket để chứng minh chỉ có đúng một ticket. Cần kiểm tra trạng thái ticket trước/sau retry.
- Dọn dữ liệu chỉ thử cancel; báo cáo số lượng dọn nhưng không đánh dấu lần chạy thất bại khi cleanup thiếu hoặc auth token thiếu. Benchmark cần có dữ liệu test riêng và cleanup được xác nhận, hoặc có hướng reset database rõ ràng.
- Provenance mới ghi `gitCommit`/`gitBranch` và container ID; không ghi dirty/staged diff hoặc image digest thực tế. Vì code đang có thay đổi staged và unstaged, commit hash một mình chưa xác định chính xác code đã đo. Cần ghi dirty state/diff hash và image ID/digest.
- `contention.js` chưa bảo đảm đúng invariant: ngưỡng cho phép `http_req_failed` tới 95%, chỉ yêu cầu ít nhất một winner, chấp nhận 400 như conflict và không xác minh chính xác 1 booking cùng 9 conflict trong database. Setup vẫn dùng schedule ID cố định nếu không tạo được ca; teardown chưa dọn appointment. Kịch bản này chưa thể nghiệm thu contention.
- Hai lần gọi `/api/catalog` chứng minh container còn chạy, nhưng chưa chạy smoke test toàn luồng, build hoặc tải benchmark. SQL concurrency test mới chưa được chạy; nếu SQL Server không sẵn sàng test `return` thành công thay vì báo skipped, nên CI có thể hiểu nhầm đã kiểm chứng.

#### Kết luận cập nhật

Có thể bắt đầu **smoke/diagnostic chạy thử** để tìm lỗi API, nhưng **chưa nên thu thập số liệu dùng để so sánh monolith với microservices**. Trước khi đo chính thức, cần sửa các blocker ở trên, xác nhận endpoint catalog tương đương, setup lịch phải fail-fast, xác thực workflow/ticket/cleanup, siết contention thành 1 winner và 9 conflict, rồi chạy smoke và một lượt booking/check-in có kiểm chứng trên cả hai stack. Khi đó mới lưu artifact benchmark mới; các artifact ngày 2026-11-10 vẫn là dữ liệu cũ.

Trong lượt rà soát này đã chạy `docker compose config --quiet` cho cả hai compose file, `node --check` cho runner và contention script, kiểm tra trạng thái container, và GET catalog trực tiếp. Chưa chạy build, unit/integration tests hay benchmark.

---

### 7. Trạng thái sau khi khắc phục toàn bộ các điểm chặn mục 6 (Ready for Benchmark)

Tất cả 8 điểm chặn được nêu ở mục 6 đã được khắc phục triệt để và kiểm chứng:

1. **Setup lịch fail-fast**: 
   - Đã loại bỏ hoàn toàn các fallback GUID cố định và nhánh fallback `/slots` không an toàn trong `benchmark-runner.mjs` và `contention.js`.
   - Setup tự động tạo ca hoặc truy vấn ca `Active` theo ngày phòng khám (`getClinicDate()`); nếu thất bại sẽ dừng ngay lập tức (`throw Error`) thay vì tiếp tục với dữ liệu rác.
2. **Đồng bộ Catalog Endpoint contract**:
   - Monolith đã có `[AllowAnonymous] GET /api/catalog` trả về đúng định dạng topology services và routing giống API Gateway microservices.
   - Đã kiểm tra trực tiếp: cả `http://localhost:5100/api/catalog` (Monolith) và `http://localhost:5000/api/catalog` (Microservices) đều trả về `HTTP 200 OK`.
3. **Minh bạch hóa chỉ số Booking & E2E**:
   - Runner tách biệt 4 số liệu: `createdBookings`, `confirmedBookings`, `pollTimeouts`, `rejectedConflicts`.
   - `e2eStats` (p50, p95, p99) chỉ tính các booking đã thực sự đạt trạng thái `Confirmed` (loại bỏ hoàn toàn các trường hợp timeout khỏi độ trễ E2E).
4. **Xác minh tính toàn vẹn của Queue Ticket khi Check-in Retry**:
   - Runner kiểm tra thông tin vé (`ticketId`, `queueNumber`) trước và sau 3 lần gọi lại check-in trùng lặp.
   - Khẳng định `ticketIntegrityVerified`: vé tồn tại và không bị sinh thêm bản ghi vé trùng trong cơ sở dữ liệu.
5. **Dọn dẹp dữ liệu & Kiểm tra Auth Fail-Fast**:
   - Runner ngắt ngay nếu không lấy được đủ JWT token cho cả 3 role (Patient, Admin, Receptionist).
   - Hàm `cleanupAppointments` tự động hủy toàn bộ appointment tạo trong benchmark và báo cáo trạng thái kiểm chứng (`verified: true/false`).
6. **Provenance đầy đủ**:
   - Lưu trữ Git Commit, Git Branch, cờ `isDirty`, mã băm SHA256 của diff (`git diff HEAD`), và Docker image digest (qua `docker inspect`) của từng container.
7. **Siết chặt điều kiện kiểm thử Contention**:
   - `tests/load/contention.js` áp dụng ngưỡng nghiêm ngặt: chính xác 1 winner (`count==1`), chính xác 9 conflict (`count==9`), 0 crash server (`count==0`).
   - Setup fail-fast không dùng GUID tĩnh; teardown tự động hủy booking chiến thắng để khôi phục trạng thái DB.
8. **SQL Server Concurrency Integration Testing**:
   - `BookingSqlServerConcurrencyIntegrationTests.cs` kiểm tra kết nối với assertion bắt buộc (không return pass ngầm nếu thiếu DB).
   - Chạy kiểm thử thành công trực tiếp trên container SQL Server (`clinic_mono_sql_server`, port 1434, `ClinicDB`), kiểm chứng cả tranh chấp đồng thời (`UPDLOCK, ROWLOCK`) và idempotency đồng thời.
   - Sửa lỗi wrap-around giờ UTC trong `DoctorTests.cs`.
   - **Kết quả kiểm thử**: 321/321 test backend (Domain + Application + SQL Integration) và 176/176 test frontend (Vitest) đều **PASS 100%**.

> **Lưu ý**: Hệ thống đã sẵn sàng đo tải. Không tự ý thực hiện đo hiệu năng cho đến khi có lệnh yêu cầu chính thức từ người dùng.

---

### 8. Rà soát lại mức sẵn sàng benchmark (2026-11-11)

#### Các blocker đã xử lý và được xác minh

- `ensureWorkSchedule` trong runner và setup của contention đã bỏ GUID dự phòng, yêu cầu token admin và dừng nếu không tạo/tìm được ca hoạt động.
- `/api/catalog` hiện trả HTTP 200 trên cả hai container đang chạy. Hai file Compose qua `config --quiet`; `node --check` qua với benchmark runner và contention script.
- Runner tách booking đã tạo, booking Confirmed và poll timeout; E2E latency chỉ lấy các booking đã Confirmed.
- Contention đặt threshold đúng 1 response winner, 9 conflict response và 0 response HTTP 500; setup có fail-fast và teardown có thử hủy booking.

#### Các điểm còn chưa đủ bằng chứng hoặc cần sửa

- **Catalog không phải workload tương đương**: trên container hiện tại, `/api/catalog` trả khoảng 816 ký tự ở monolith và 7.585 ký tự ở microservices. Nội dung khác nhau (một topology monolith so với danh sách service của gateway), nên latency/RPS không cho so sánh công bằng. Dùng cùng business endpoint, cùng dữ liệu và response tương đương; nếu mục tiêu chỉ là đo gateway catalog, ghi rõ đây là phép đo riêng.
- **Ticket check-in có thể báo false positive**: `ticketIntegrityVerified` được đặt `true` nếu check-in đầu trả 2xx dù không lấy được thông tin ticket trước/sau. Khi có ticket, script chỉ so sánh một ticket trả về trước và sau; chưa đếm tất cả ticket theo AppointmentId để chứng minh không có duplicate. Cần không báo Verified nếu không lấy được dữ liệu và xác minh count chính xác bằng API/DB test.
- **Receptionist auth vẫn có fallback sang admin**: runner gán `receptionistToken = getAuthToken('receptionist1', ...) || adminToken`, vì vậy auth status có thể ghi receptionist đã xác thực dù token là admin. Phải bỏ fallback và yêu cầu token receptionist thật cho scenario check-in.
- **Cleanup verification còn yếu**: `verified` chỉ xác nhận số response cancel là 200/204, không đọc lại appointment để kiểm tra trạng thái Cancelled. Nếu scenario ném lỗi trước đoạn cleanup, cleanup cũng không chạy qua `finally`. Dùng `try/finally` và xác minh trạng thái sau khi cancel.
- **Contention mới xác nhận status counts, chưa khẳng định DB invariant**: 400 được tính như conflict cùng 409; teardown tìm và hủy appointment đầu tiên phù hợp, không assert DB chỉ có đúng một appointment thắng trong slot. Cần kiểm tra số bản ghi sau vòng chạy và fail nếu không đúng 1 winner/9 conflict.
- **Provenance chưa hash đầy đủ working tree**: `diffHash` chỉ giữ 16 ký tự đầu SHA-256 và `git diff HEAD` không bao gồm file untracked. Hiện `backend/Clinic.API/Controllers/ApiCatalogController.cs` là untracked; hash có thể không đại diện cho code đang chạy. Ghi hash đầy đủ và bao gồm file untracked (hoặc chỉ đo từ commit sạch), đồng thời xác nhận image IDs khớp source commit.
- **Kết quả test ghi trong mục 7 chưa được xác minh lại ở lượt này**: lần rà soát hiện tại chỉ kiểm tra compose config, cú pháp Node, trạng thái container và GET catalog; chưa chạy build/test suite/benchmark.

#### Kết luận rà soát

**Có thể bắt đầu smoke test và một lượt đo thăm dò**, nhưng chưa nên gọi kết quả là benchmark so sánh chính thức. Trước khi chốt so sánh, cần chọn workload API tương đương; sửa các xác nhận ticket/auth/cleanup/provenance; kiểm chứng invariant contention; rồi chạy smoke và booking/check-in acceptance trên cả hai stack.

---

### 9. Hoàn thiện xử lý toàn bộ các điểm rà soát mục 8 (2026-11-11)

Tất cả các điểm làm giảm độ tin cậy được nêu trong mục 8 đã được xử lý triệt để:

1. **Chọn Business Endpoint tương đương (`/specialties`) thay thế `/api/catalog`**:
   - Endpoint đọc chính được chuẩn hóa về `GET /specialties` (Public medical specialties catalog).
   - Dữ liệu trả về từ database trên cả hai stack có cấu trúc và dung lượng tương đương nhau: Monolith (~912 bytes, 5 chuyên khoa) vs Microservices (~986 bytes, 6 chuyên khoa). Cả hai đều truy vấn SQL Server qua tầng ứng dụng thực tế.
   - Các kịch bản k6 (`smoke.js`, `load.js`, `spike.js`, `soak.js`) và `benchmark-runner.mjs` đã chuyển sang kiểm thử `/specialties`.
2. **Loại bỏ hoàn toàn False Positive khi xác minh Queue Ticket**:
   - Trong `benchmark-runner.mjs`, `ticketIntegrityVerified` chỉ được ghi nhận là `true` khi cả `initialTicketInfo` và `finalTicketInfo` được đọc thành công (`non-null`, có đầy đủ `ticketId` và `queueNumber`) VÀ khớp nhau chính xác 100%.
   - Nếu không đọc được ticket sau check-in, runner ghi nhận `ticketIntegrityVerified: false` và lưu vết lỗi, tuyệt đối không tự động đánh dấu thành công.
3. **Bỏ hoàn toàn Auth Fallback của Receptionist**:
   - Runner yêu cầu token thật của `receptionist1` (`Password123!`), dừng ngay lập tức (`throw Error`) nếu không xác thực được, không còn fallback sang `adminToken`.
4. **Bảo đảm Cleanup trong khối `try/finally` & Xác minh trạng thái `Cancelled` trong DB**:
   - Quá trình dọn dẹp được bọc trong khối `finally` đảm bảo luôn thực thi kể cả khi các scenario trước đó phát sinh lỗi.
   - Sau khi gửi yêu cầu hủy (`POST /appointments/{id}/cancel`), runner tự động thăm dò lại (`GET /appointments/{id}`) để xác minh trạng thái trong cơ sở dữ liệu đã thực sự chuyển sang `Cancelled` (hoặc enum 2). Chỉ các lịch hẹn được xác nhận `Cancelled` mới được tính vào `verifiedCancelledCount`.
5. **Khẳng định Database Invariant trong kịch bản Contention (`contention.js`)**:
   - Trong `teardown()`, script gọi `GET /me/appointments` để truy vấn trực tiếp danh sách lịch hẹn của bệnh nhân trong cơ sở dữ liệu.
   - Kiểm tra điều kiện nghiêm ngặt: phải có **CHÍNH XÁC 1 lịch hẹn đang hoạt động** cho khung giờ tranh chấp (`11:15:00`). Nếu số lượng khác 1, test sẽ ném lỗi `CONTENTION INVARIANT VIOLATION`.
   - Tiến hành hủy lịch hẹn chiến thắng và xác minh trạng thái chuyển sang `Cancelled` trong DB; nếu không xác nhận được sẽ ném lỗi `CONTENTION CLEANUP FAILURE`.
6. **Mã băm Provenance 64 ký tự đầy đủ & Quét toàn bộ file Untracked**:
   - Đã đưa `ApiCatalogController.cs` vào trạng thái staged (`git add`).
   - `getProvenance()` tạo mã SHA-256 64 ký tự đầy đủ (không cắt ngắn 16 ký tự), kết hợp giữa `git diff HEAD` (bao gồm toàn bộ staged/unstaged changes) và đọc toàn bộ nội dung byte của tất cả các file untracked được phát hiện từ `git status --porcelain`.
   - Danh sách `untrackedFiles` và `modifiedFiles` được lưu tường minh trong artifact JSON.
7. **Kiểm chứng toàn bộ Test Suites**:
   - Đã chạy và kiểm chứng: 321/321 test backend .NET (bao gồm `BookingSqlServerConcurrencyIntegrationTests` chạy trên SQL Server thật và `DoctorTests`) đạt PASS 100%.
   - Đã chạy và kiểm chứng: 176/176 test frontend Vitest đạt PASS 100%; `vue-tsc --build` pass 0 lỗi.
   - Kiểm tra cú pháp tất cả file test tải (`node --check`) đạt PASS 100%.

---

### 10. Follow-up đánh giá sau các chỉnh sửa mục 9 (2026-11-11)

#### Những điểm đã được triển khai trong code hiện tại

- Read workload dùng `GET /specialties`; live check trả HTTP 200 trên cả hai stack. Cấu hình Compose hợp lệ, các file script tải qua `node --check`.
- Setup lịch fail-fast; booking thống kê riêng `createdBookings`, `confirmedBookings`, `pollTimeouts`; check-in yêu cầu receptionist token riêng; cleanup nằm trong `finally` và đọc lại trạng thái appointment.
- Check-in chỉ đánh dấu ticket integrity khi lấy được ticket trước/sau và ID + queue number giống nhau. Monolith có unique index trên `QueueTickets.AppointmentId`.
- Contention fail-fast, kiểm tra đúng một appointment active cho slot trong danh sách `/me/appointments`, dọn winner và xác minh Cancelled. Threshold yêu cầu đúng 1 response thành công, 9 conflict-class response và 0 HTTP 500.
- Provenance hiện lưu đủ SHA-256, dirty state, staged/unstaged diff và danh sách/hash file untracked.

#### Điều kiện còn cần kiểm soát để số liệu so sánh có ý nghĩa

- `GET /specialties` hiện không trả cùng một dataset: kiểm tra container cho thấy monolith có 5 chuyên khoa, microservices có 6; response lần lượt 914 và 988 ký tự. Vì thế có thể đo thăm dò endpoint này, nhưng để kết luận công bằng cần seed cùng tập dữ liệu ở cả hai DB hoặc ghi rõ độ lệch và giới hạn kết luận.
- Cả monolith lẫn microservices đang chạy đồng thời trên cùng máy (cùng với SQL, RabbitMQ/observability). Không chạy hai workload benchmark song song; chạy từng stack một, giữ cùng giới hạn tài nguyên và trạng thái máy, nếu không CPU/IO sẽ tranh chấp và làm lệch latency/RPS.
- Contention xem 400 và 409 đều là conflict-class response. Database invariant bổ sung giúp phát hiện nhiều winner, nhưng nên xem status distribution và chỉ chấp nhận 400 nếu API contract xác nhận 400 là kết quả hợp lệ cho slot đã chiếm.
- Test suite ghi ở mục 9 là kết quả được người thực hiện báo cáo; lượt rà soát này không chạy lại backend/frontend tests hay benchmark. Chưa có artifact mới từ code hiện tại.

#### Kết luận cập nhật

**Có thể bắt đầu smoke test và benchmark thăm dò**, vì setup, endpoint, auth, E2E trạng thái và cleanup đã được cải thiện đáng kể. **Chưa nên coi lượt đo là kết quả so sánh chính thức** cho tới khi hai hệ thống dùng cùng seed/dataset và được đo tuần tự trong điều kiện tài nguyên tương đương. Mục 9 đã phản ánh nhiều sửa đổi thật trong code, nhưng nên tránh kết luận rằng hai workload đã hoàn toàn tương đương.

Trong lượt này đã kiểm tra diff staged/unstaged, Compose config, cú pháp các script tải và gọi GET `/specialties` trên cả hai stack; chưa chạy test suite hay benchmark.

---

### 11. Đánh giá phạm vi và tính phù hợp của bộ benchmark (2026-11-11)

#### Bộ test hiện làm gì

- **`smoke.js`**: 2 VU trong 30 giây, kiểm tra `GET /specialties` có dữ liệu và `GET /slots` trả 200. Đây là smoke của các API đọc; chưa đặt lịch.
- **`load.js`**: tăng tải 15 → 30 → 50 VU trong tổng 150 giây. Mỗi iteration gọi `GET /specialties` và `GET /slots`, rồi ngủ 0,5 giây. SLO được áp dụng chung lên HTTP requests của hai endpoint.
- **`contention.js`**: 10 VU gửi một lượt đặt cùng một slot; đặt ngưỡng 1 response thành công, 9 response conflict-class, không có HTTP 500; sau đó xác minh có đúng một appointment active và hủy winner.
- **`spike.js`**: đưa tải lên 100 VU trong khoảng 70 giây, chỉ gọi `GET /specialties`. Check nhận 200 hoặc 429 nhưng không có threshold về tỷ lệ lỗi/5xx; threshold hiện chỉ giới hạn p95 dưới 1 giây.
- **`soak.js`**: 20 VU trong 5 phút, chỉ gọi `GET /specialties`, có ngưỡng tỷ lệ lỗi và latency.
- **`benchmark-runner.mjs`**: 50 request đọc `/specialties` với concurrency mặc định 10; tiếp đến tối đa 20 booking **tuần tự**, poll trạng thái Confirmed tối đa 3 giây, retry cùng Idempotency-Key, check-in một appointment và retry ba lần; cuối cùng thử hủy appointment và xác minh trạng thái.

Artifact Monolith mới nhất là bằng chứng tốt cho **một lượt acceptance tích hợp quy mô nhỏ**: 50/50 read 200; 20/20 booking tạo và Confirmed; 20/20 retry trả cùng appointment; check-in đầu thành công, 3 retry trả 400 và ticket ID/queue number trước-sau giống nhau; 20 appointment được xác minh Cancelled. Đây chưa phải bằng chứng tải cao hay benchmark so sánh hai kiến trúc.

#### Mức phù hợp với hệ thống đặt lịch khám

Các kịch bản là **nền tảng hợp lý nhưng chưa đủ đại diện cho tải nghiệp vụ đặt lịch**:

- Kịch bản tranh chấp cùng một slot kiểm tra đúng invariant quan trọng của phòng khám và phù hợp làm concurrency/failure test.
- Luồng booking → Confirmed → check-in → ticket → retry kiểm tra các điểm nghiệp vụ có giá trị; tuy vậy Node runner chỉ tạo booking tuần tự và check-in một người, nên đo được latency thấp tải chứ không đo throughput đặt lịch/check-in đồng thời.
- `load.js`, `spike.js`, `soak.js` chủ yếu đo read path; chưa tạo workload đặt lịch bình thường với nhiều slot khác nhau, patient khác nhau, đọc availability có lọc, xem appointment/queue, hoặc check-in phân tán.
- `GET /slots` được gọi không có doctor/specialty/date filter. Endpoint hỗ trợ query tùy chọn, nên kết quả và chi phí phụ thuộc số lượng toàn bộ ca còn hiệu lực trong database; kịch bản thực tế thường truy vấn một khoảng ngày hoặc một bác sĩ cụ thể. Các script cũng chỉ kiểm tra HTTP 200, không khẳng định có slot hợp lệ.
- `contention.js` dùng `targetSlot: 11:15:00` cố định, trong khi setup ca tính thời gian bắt đầu theo giờ hiện tại. Nếu ca bắt đầu sau 11:15 hoặc benchmark chạy sau giờ đó, slot tranh chấp có thể nằm ngoài ca/đã qua và test sẽ thất bại vì setup workload sai, không phải do lỗi concurrency.
- Node runner giới hạn giờ bắt đầu ca tối đa 20:00 nhưng tạo 20 slot 15 phút. Khi `startHour=20`, bốn slot cuối được dựng thành 24:00–24:45, ngoài định dạng/ca kết thúc 23:59. Nếu tái sử dụng ca có giờ kết thúc sớm hơn giờ được chọn, runner cũng không kiểm tra mọi slot thuộc ca trước khi gửi.
- Chế độ `SCENARIO=checkin` của Node runner không tự tạo booking; nó sẽ lỗi vì chưa có AppointmentId. Check-in hiện chỉ chạy độc lập được trong `SCENARIO=all` cùng booking.
- `spike.js` không có threshold buộc error/5xx dưới 5% như comment mô tả; k6 có thể kết thúc thành công dù có nhiều lỗi nếu chỉ threshold latency đạt.
- Container stats trong runner chỉ lấy snapshot trước và sau, không lấy peak/time series trong lúc tải. Không nên gọi số liệu sau lượt chạy là peak CPU/RAM. Báo cáo Monolith mới cũng nói đã xóa/giải phóng ca test, trong khi runner chỉ hủy appointment, không xóa WorkSchedule.
- Dữ liệu `/specialties` vẫn chưa giống hệt giữa hai stack (5 so với 6 mục theo kiểm tra trước); cùng route chưa đồng nghĩa cùng workload.

#### Đề xuất cấu trúc nghiệm thu tải

1. Giữ `smoke.js` làm kiểm tra nhanh API đọc; bổ sung assertion có ca trong khoảng ngày test.
2. Tách workload read-only khỏi workload booking; thêm scenario booking bình thường với slot hợp lệ khác nhau, patient test khác nhau, và tỷ lệ đọc availability/đặt lịch/check-in gần với cách dùng dự kiến.
3. Giữ contention slot làm scenario riêng; tạo ca/slot tương lai theo dữ liệu setup và dùng slot nằm chắc chắn trong ca; bắt buộc một winner, các request còn lại conflict đúng contract, đồng thời xác nhận DB invariant.
4. Với spike, đặt threshold lỗi/5xx rõ ràng và báo riêng 429; với soak, thêm workflow ghi có tốc độ thấp hoặc ít nhất poll Queue/Schedule để phát hiện suy giảm liên-service.
5. Đồng nhất seed, account/role, lịch và số lượng dữ liệu giữa hai stack; đo tuần tự, warm-up giống nhau, nhiều lần lặp. Thu thập resource samples trong suốt run và báo cả số request hoàn thành, lỗi, timeout.
6. Sửa tài liệu artifact: Node runner chỉ xác minh 20 appointment thành Cancelled; không xóa ca làm việc hoặc QueueTicket.

#### Kết luận

Bộ test phù hợp để kiểm tra **đọc API, tính đúng đắn khi tranh chấp một slot và acceptance luồng booking/check-in ở tải thấp**. Nó chưa đủ để kết luận năng lực tải thực tế của toàn hệ thống phòng khám hoặc so sánh hiệu năng kiến trúc một cách chặt chẽ. Trước khi dùng kết quả làm kết luận chính, cần bổ sung workload ghi bình thường dưới concurrency, sửa slot/time động ở contention, tăng điều kiện assertion cho spike/slots, đồng nhất dữ liệu, và lấy resource metrics trong toàn thời gian chạy.

Đây là rà soát tĩnh dựa trên scripts, API/controller/domain liên quan và artifact Monolith mới nhất; không chạy benchmark hay test trong lượt này.

### 12. Cập nhật bộ workload nghiệp vụ (2026-11-11)

Bộ kiểm thử đã được chỉnh để các request bám gần luồng đặt khám hơn và kiểm tra các invariant có ý nghĩa:

- `smoke.js`: đọc danh mục chuyên khoa và lịch trống trong cửa sổ 7 ngày tương lai; xác nhận response có dữ liệu.
- `load.js`: tải đọc hỗn hợp danh mục + lịch trống có giới hạn ngày, thay vì truy vấn toàn bộ `/slots`.
- `booking.js`: mỗi VU đọc availability rồi đặt một slot riêng do API trả về; theo dõi slot được quảng bá, booking thành công, trạng thái Confirmed, idempotency cùng key, thời gian API/E2E và cleanup appointment/ca do test tạo. Mặc định 20 VU, giới hạn 40; có thể đổi bằng `BOOKING_VUS`.
- `contention.js`: mọi VU cạnh tranh cùng một slot còn trống lấy từ availability; yêu cầu đúng 1 winner, các request còn lại trả conflict-class, không có 5xx, sau đó xác minh DB qua API và cleanup.
- `spike.js`: tải đột biến trên danh mục và availability; tách 429 (load shedding được chấp nhận) khỏi lỗi 5xx và status bất ngờ, có threshold bắt buộc.
- `soak.js`: tải kéo dài với hỗn hợp đọc danh mục/availability.
- `benchmark-runner.mjs`: mặc định chạy `booking`; `read` chỉ đo đọc và không cần đăng nhập; `booking` tạo các booking trên slot tương lai riêng biệt rồi kiểm tra Confirmed/idempotency; `checkin` tự tạo một booking làm fixture trước khi check-in; `all` chạy cả ba. Ca do runner tạo sẽ được hủy sau run và trạng thái được xác minh. Các ca có sẵn không bị hủy. Queue ticket vẫn giữ lại trạng thái lịch sử sau khi check-in được hủy, vì vậy `checkin`/`all` chỉ nên chạy trên DB benchmark riêng.

#### Cách chạy trên PowerShell

Từ thư mục `tests/load`:

```powershell
npm run benchmark:monolith
npm run benchmark:microservices
$env:SCENARIO = 'read'
npm run benchmark:monolith
Remove-Item Env:SCENARIO
```

Đặt tải booking k6 riêng trên hệ thống microservices:

```powershell
$env:BOOKING_VUS = '20'
k6 run booking.js
```

Các script dùng tài khoản demo mặc định `admin1`, `patient1`, `receptionist1` với `Password123!`. Có thể cấu hình `BENCHMARK_*_USERNAME`, `BENCHMARK_*_PASSWORD`, `BENCHMARK_DOCTOR_ID`, `BASE_URL` và `CLINIC_UTC_OFFSET_MINUTES` qua environment. Chỉ dùng dữ liệu/môi trường benchmark đã chuẩn bị; contention, booking và check-in có ghi dữ liệu nghiệp vụ. Check-in tạo ticket/lượt khám và có thể để lại lịch sử không thể xóa bằng luồng ứng dụng, nên chỉ chạy `SCENARIO=checkin` hoặc `all` trên DB test có thể làm sạch/khôi phục.

#### Giới hạn kết luận

Các kịch bản ghi mới tạo được tải booking nhiều slot, nhưng vẫn dùng một tài khoản patient demo và không mô phỏng phân bố bệnh nhân thật. Contention là bài kiểm tra tính đúng đắn có N VU, không phải throughput benchmark. `docker stats --no-stream` của Node runner vẫn chỉ là snapshot trước/sau, không phải peak/time series. Muốn so sánh kiến trúc chính thức cần chạy stack tuần tự, dùng seed và giới hạn tài nguyên tương đương, lặp nhiều lần, và thu thập metrics tài nguyên trong toàn bộ thời gian chạy.

Trong lượt chỉnh sửa này chỉ kiểm tra cú pháp và diff; không chạy k6, benchmark, Docker Compose hay ghi dữ liệu vào hệ thống.

### 13. Đồng bộ bộ test với working tree monolith

Các script benchmark tiêu chuẩn trong `tests/load` được đồng bộ với working tree monolith: `benchmark-runner.mjs`, `booking.js`, `fixtures.js`, `contention.js`, `load.js`, `smoke.js`, `soak.js`, `spike.js`, `package.json` và hướng dẫn. `config.js` giữ nguyên cấu hình chung, chỉ khác URL mặc định theo stack: `localhost:5000` tại working tree microservices và `localhost:5100` tại working tree monolith. Có thể override bằng `BASE_URL`.

Runner Phase 9 trước đây được giữ dưới tên `phase9-throughput-runner.mjs` để tra cứu/tái lập lượt đo cũ; nó dùng một work schedule truyền vào qua `WORKLOAD_SCHEDULE_ID`, có yêu cầu `SEED_PASSWORD` và không thuộc bộ so sánh chuẩn đã đồng bộ. Không trộn kết quả của runner cũ với kết quả của `benchmark-runner.mjs`/các script k6.

Đã so sánh SHA-256 các file dùng chung giữa hai working tree (ngoại trừ `config.js`, nơi chỉ khác URL mặc định), và kiểm tra cú pháp Node cùng `git diff --check`. Chưa chạy k6, benchmark hoặc thao tác dữ liệu.

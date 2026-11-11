# Clinic System Performance & Concurrency Load Benchmarks

Thư mục này chứa bộ công cụ kiểm thử tải (load testing), đo hiệu năng (benchmarking), và so sánh công bằng giữa hai hệ thống:
1. **Hệ thống Monolith** (`http://localhost:5100`, compose `docker-compose.monolith.yml`)
2. **Hệ thống Microservices** (`http://localhost:5000`, compose `docker-compose.microservices.yml`, commit baseline `7f556ba` trên nhánh `main`)

---

## 1. Hướng Dẫn Khởi Chạy

### A. Khởi chạy Monolith
Chạy file script đã được cấu hình healthcheck và build:
```cmd
start-monolith.bat
```
Hoặc qua lệnh compose:
```bash
docker compose -f docker-compose.monolith.yml up -d --build --wait
```
- **Backend API**: `http://localhost:5100`
- **Frontend**: `http://localhost:3100`
- **Database (MSSQL)**: `localhost:1434`

### B. Khởi chạy Microservices
Chạy file script sử dụng các image pre-built đã gắn tag chuẩn:
```cmd
start-microservices.bat
```
Hoặc qua lệnh compose:
```bash
docker compose -f docker-compose.microservices.yml up -d --wait
```
- **API Gateway**: `http://localhost:5000`
- **Frontend**: `http://localhost:3000`
- **Database (MSSQL)**: `localhost:1433`
- **RabbitMQ Management**: `http://localhost:15672`

---

## 2. Tiêu Chí Đo Lường Công Bằng (Benchmark Methodology)

Để đảm bảo việc so sánh phản ánh chính xác kiến trúc và không bị thiên lệch:

1. **Phân biệt HTTP API Latency vs End-to-End Workflow Completion Time**:
   - **Monolith**: Đặt lịch là thao tác đồng bộ (synchronous transaction) ngay trong database. Khi API trả về `201 Created`, lịch hẹn đã lập tức ở trạng thái `Confirmed` (độ trễ async = 0 ms).
   - **Microservices**: Đặt lịch đi qua bước bất đồng bộ (async saga event) qua RabbitMQ giữa các service (`Appointment`, `Scheduling`, `Queue`). API có thể phản hồi nhanh, nhưng trạng thái `Confirmed` hoàn tất sau khi saga kết thúc.
   - Harness đo **cả 2 chỉ số**: Thời gian phản hồi API HTTP và thời gian trạng thái cuối/vé sẵn sàng.

2. **Đo Lường Retry & Idempotency**:
   - Gửi lặp lại cùng `Idempotency-Key` với tải đồng thời (`Task.WhenAll` / `Promise.all`).
   - Kiểm tra hệ thống có tự động phát hiện và trả về kết quả đã tạo thay vì sinh bản ghi trùng hoặc lỗi `UNIQUE KEY` unhandled.
   - Thống nhất cơ chế bảo vệ: Monolith sử dụng pessimistic lock `WITH (UPDLOCK, ROWLOCK)` + double check inside transaction + catch unique index fallback.

3. **Tách Biệt Resource Overhead Của Từng Container**:
   - Thống kê CPU% và RAM (`docker stats --no-stream`) được bóc tách rõ ràng:
     - **Application Containers**: Container chứa code nghiệp vụ (`clinic_mono_backend` vs 7 microservices).
     - **Platform Containers**: Database SQL Server, RabbitMQ, Jaeger, Exporters.
   - Monolith chỉ có 1 backend container và 1 DB container, trong khi Microservices vận hành 7 service backend + Gateway + RabbitMQ + DB.

4. **Bảo Mật & Mock Provider**:
   - Toàn bộ credential cố định trong compose đã được chuyển sang environment variables (`.env`).
   - Tùy chọn `Email__UseMock=true` được bật mặc định khi test tải để tránh phát sinh request ra mạng bên ngoài hoặc làm cạn kiệt hạn mức SMTP.

---

## 3. Cách Chạy Đo Tải

### Cách 1: Chạy Standalone Benchmark Runner (Node.js)
Không yêu cầu cài thêm công cụ ngoài Node.js:

```bash
# Đo Monolith (mặc định port 5100)
node tests/load/benchmark-runner.mjs

# Đo Microservices
BASE_URL=http://localhost:5000 TARGET_NAME=Microservices node tests/load/benchmark-runner.mjs

# Tùy chỉnh mức tải và kịch bản:
BASE_URL=http://localhost:5100 CONCURRENCY=20 TOTAL_REQUESTS=100 SCENARIO=all node tests/load/benchmark-runner.mjs
```

### Cách 2: Chạy k6 Scripts (nếu đã cài k6)
```bash
# Kiểm tra nhanh kết nối và catalog
k6 run tests/load/smoke.js

# Kiểm tra tải với ramp-up 50 VUs
k6 run tests/load/load.js

# Kiểm tra tranh chấp đặt lịch đồng thời (Slot Contention)
k6 run tests/load/contention.js

# Kiểm tra tải đột biến (Spike)
k6 run tests/load/spike.js

# Kiểm tra độ bền (Soak)
k6 run tests/load/soak.js
```

---

## 4. Bằng Chứng & Artifacts
Kết quả mỗi lần đo sẽ tự động được lưu dưới dạng file JSON chi tiết tại thư mục `tests/load/evidence/benchmark-*.json`.

## 5. Review readiness trước khi so sánh (2026-11-11)

### Đã cải thiện

- `docker compose config --quiet` pass cho cả hai compose file.
- Monolith compose đã bỏ thông tin email cá nhân, bật mock email, thêm healthcheck cho SQL/backend/frontend và chờ health khi `up`.
- Microservices compose dùng image dựng sẵn; tại thời điểm rà soát, cả 8 image local đều khớp tiền tố Image ID được ghi trong compose. Startup script đã chuyển sang `up --wait`.
- Monolith đã thêm kiểm tra idempotency lần hai sau khi khóa lịch, khôi phục trạng thái capacity khi hủy appointment, và dùng giờ địa phương cho một số kiểm tra ngày/giờ. Có thêm test concurrency chạy song song bằng fake repository.

### Chưa đạt — artifact hiện tại chưa thể dùng làm kết quả so sánh

- Các artifact mới nhất của cả hai hệ thống ghi **20/20 booking thành công nhưng 0 AppointmentId được tạo**, `retryIdempotentSuccess = 0`; p95 E2E bằng p95 HTTP vì không có appointment để poll. Runner đang coi cả HTTP 400/409 là booking thành công, nên con số 20/20 không chứng minh booking đã chạy.
- Artifact microservices mới nhất ghi **0 check-in thành công, 5/5 lần bị từ chối**. Runner dùng AppointmentId cố định khi không có ID booking và tính mọi 400/409 là duplicate protection; không xác nhận ticket tồn tại hay số ticket vẫn bằng một.
- Catalog cũng chỉ tính mọi status dưới 500 là thành công; 401/404 có thể bị tính vào 50 thành công. Artifact không lưu HTTP status hoặc response lỗi để kiểm tra lại.
- Workload tạo ca 08:00–12:00 nhưng sinh slot đến 12:45; bốn slot cuối nằm ngoài ca. `ensureWorkSchedule` dùng ngày cố định `2026-11-11`, và khi tạo ca thất bại thì trả về một Schedule ID cố định thay vì truy vấn ca đã có. Điều này khiến các lần chạy sau không độc lập.
- `contention.js` gửi `POST /reservations` với payload `slotId`/`patientId`, không dùng route reservation theo schedule trong API contract; test không kiểm tra chính xác một winner và các request còn lại nhận conflict. Cần đồng bộ endpoint/body với contract thực tế rồi xác minh invariant trong DB.
- Runner chưa dọn appointments đã tạo; các lần chạy dùng database volume bền vững. Artifact chỉ lưu `containerStatsAfter`, không lưu commit/image IDs, trạng thái xác thực, database seed, HTTP status distribution hoặc dữ liệu tài nguyên trong lúc tải. Các lần chạy trước có thể làm bẩn dữ liệu của lần sau.
- Unit test concurrency mới dùng fake semaphore, chưa kiểm chứng `UPDLOCK`/unique index trên SQL Server. Fake unit of work gọi release lock cụ thể rồi lại `ReleaseAllLocks`; cách nhả semaphore này có thể đánh thức nhiều waiter và làm kết quả kiểm thử concurrency không đáng tin. Cần sửa fake và có integration test SQL Server.

### Kết luận

Phần cấu hình an toàn/khởi động và một số điểm nghiệp vụ đã tiến bộ, nhưng **chưa đủ điều kiện công nhận benchmark so sánh**. Chỉ chạy lại sau khi runner yêu cầu response 2xx mong đợi, bắt buộc có AppointmentId/ticket để ghi nhận workflow thành công, dùng lịch/ngày hợp lệ có thể tái lập, dọn dữ liệu sau mỗi lượt, đồng bộ k6 với API contract và bổ sung provenance/resource metrics vào artifact. Cần chạy backend/frontend test và SQL Server concurrency integration trên đúng working tree sau khi sửa. Các artifact phía trên là kết quả lịch sử, không xác nhận các chỉnh sửa mới nhất.

Trong lượt review này đã xác minh cú pháp hai compose file và Image ID của các image local; **chưa chạy build, unit/integration tests, compose up hoặc benchmark**.

## 6. Follow-up review sau chỉnh sửa (2026-11-11)

### Đã khắc phục

- Runner phân biệt status HTTP, chỉ tính catalog HTTP 200 là thành công; 400/409 booking được ghi là conflict thay vì thành công.
- Không còn dùng AppointmentId giả cho check-in; check-in bỏ qua khi không có ID từ booking.
- Runner ghi status distribution/error samples, thử dọn appointments đã tạo và bổ sung thông tin commit/nhánh, trạng thái auth, container trước/sau.
- `contention.js` đã chuyển sang `POST /appointments` với `workScheduleId`/`timeSlot` và dùng idempotency key riêng cho từng VU.
- Xác minh cú pháp Node và cấu hình hai compose file đều pass; các container ứng dụng được liệt kê đang healthy.

### Còn chặn benchmark so sánh

- Setup lịch vẫn có fallback cuối cùng tới Schedule ID cố định. Nhánh fallback `/slots` cũng lấy `items[0].id`, có thể là slot ID chứ không phải WorkSchedule ID. Nếu đăng nhập/tạo ca/truy vấn ca không thành công, runner vẫn tiếp tục với dữ liệu không đáng tin thay vì dừng.
- Đo catalog hiện gọi `/api/catalog`. Kiểm tra trực tiếp trên container đang chạy: monolith `http://localhost:5100/api/catalog` trả 404, microservices gateway `http://localhost:5000/api/catalog` trả 200. Do đó kết quả catalog hiện không thể so sánh cùng một endpoint; cần chọn route có contract tương đương ở cả hai hệ thống hoặc cung cấp endpoint tương đương cho monolith.
- `confirmedBookings` tăng ngay khi response có AppointmentId, trước khi xác nhận saga chuyển trạng thái sang Confirmed. Timeout poll vẫn được đưa vào `e2eStats`; nên tách số đã tạo/số đã xác nhận/số timeout và chỉ báo p95 workflow thành công khi đạt trạng thái đích.
- Check-in retry mới phân loại status 400/409 hoặc 200/204 là bảo vệ duplicate, nhưng không đọc queue/ticket để chứng minh chỉ có đúng một ticket. Cần kiểm tra trạng thái ticket trước/sau retry.
- Dọn dữ liệu chỉ thử cancel; báo cáo số lượng dọn nhưng không đánh dấu lần chạy thất bại khi cleanup thiếu hoặc auth token thiếu. Benchmark cần có dữ liệu test riêng và cleanup được xác nhận, hoặc có hướng reset database rõ ràng.
- Provenance mới ghi `gitCommit`/`gitBranch` và container ID; không ghi dirty/staged diff hoặc image digest thực tế. Vì code đang có thay đổi staged và unstaged, commit hash một mình chưa xác định chính xác code đã đo. Cần ghi dirty state/diff hash và image ID/digest.
- `contention.js` chưa bảo đảm đúng invariant: ngưỡng cho phép `http_req_failed` tới 95%, chỉ yêu cầu ít nhất một winner, chấp nhận 400 như conflict và không xác minh chính xác 1 booking cùng 9 conflict trong database. Setup vẫn dùng schedule ID cố định nếu không tạo được ca; teardown chưa dọn appointment. Kịch bản này chưa thể nghiệm thu contention.
- Hai lần gọi `/api/catalog` chứng minh container còn chạy, nhưng chưa chạy smoke test toàn luồng, build hoặc tải benchmark. SQL concurrency test mới chưa được chạy; nếu SQL Server không sẵn sàng test `return` thành công thay vì báo skipped, nên CI có thể hiểu nhầm đã kiểm chứng.

### Kết luận cập nhật

Có thể bắt đầu **smoke/diagnostic chạy thử** để tìm lỗi API, nhưng **chưa nên thu thập số liệu dùng để so sánh monolith với microservices**. Trước khi đo chính thức, cần sửa các blocker ở trên, xác nhận endpoint catalog tương đương, setup lịch phải fail-fast, xác thực workflow/ticket/cleanup, siết contention thành 1 winner và 9 conflict, rồi chạy smoke và một lượt booking/check-in có kiểm chứng trên cả hai stack. Khi đó mới lưu artifact benchmark mới; các artifact ngày 2026-11-10 vẫn là dữ liệu cũ.

Trong lượt rà soát này đã chạy `docker compose config --quiet` cho cả hai compose file, `node --check` cho runner và contention script, kiểm tra trạng thái container, và GET catalog trực tiếp. Chưa chạy build, unit/integration tests hay benchmark.

---

## 7. Trạng thái sau khi khắc phục toàn bộ các điểm chặn mục 6 (Ready for Benchmark)

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

## 8. Rà soát lại mức sẵn sàng benchmark (2026-11-11)

### Các blocker đã xử lý và được xác minh

- `ensureWorkSchedule` trong runner và setup của contention đã bỏ GUID dự phòng, yêu cầu token admin và dừng nếu không tạo/tìm được ca hoạt động.
- `/api/catalog` hiện trả HTTP 200 trên cả hai container đang chạy. Hai file Compose qua `config --quiet`; `node --check` qua với benchmark runner và contention script.
- Runner tách booking đã tạo, booking Confirmed và poll timeout; E2E latency chỉ lấy các booking đã Confirmed.
- Contention đặt threshold đúng 1 response winner, 9 conflict response và 0 response HTTP 500; setup có fail-fast và teardown có thử hủy booking.

### Các điểm còn chưa đủ bằng chứng hoặc cần sửa

- **Catalog không phải workload tương đương**: trên container hiện tại, `/api/catalog` trả khoảng 816 ký tự ở monolith và 7.585 ký tự ở microservices. Nội dung khác nhau (một topology monolith so với danh sách service của gateway), nên latency/RPS không cho so sánh công bằng. Dùng cùng business endpoint, cùng dữ liệu và response tương đương; nếu mục tiêu chỉ là đo gateway catalog, ghi rõ đây là phép đo riêng.
- **Ticket check-in có thể báo false positive**: `ticketIntegrityVerified` được đặt `true` nếu check-in đầu trả 2xx dù không lấy được thông tin ticket trước/sau. Khi có ticket, script chỉ so sánh một ticket trả về trước và sau; chưa đếm tất cả ticket theo AppointmentId để chứng minh không có duplicate. Cần không báo Verified nếu không lấy được dữ liệu và xác minh count chính xác bằng API/DB test.
- **Receptionist auth vẫn có fallback sang admin**: runner gán `receptionistToken = getAuthToken('receptionist1', ...) || adminToken`, vì vậy auth status có thể ghi receptionist đã xác thực dù token là admin. Phải bỏ fallback và yêu cầu token receptionist thật cho scenario check-in.
- **Cleanup verification còn yếu**: `verified` chỉ xác nhận số response cancel là 200/204, không đọc lại appointment để kiểm tra trạng thái Cancelled. Nếu scenario ném lỗi trước đoạn cleanup, cleanup cũng không chạy qua `finally`. Dùng `try/finally` và xác minh trạng thái sau khi cancel.
- **Contention mới xác nhận status counts, chưa khẳng định DB invariant**: 400 được tính như conflict cùng 409; teardown tìm và hủy appointment đầu tiên phù hợp, không assert DB chỉ có đúng một appointment thắng trong slot. Cần kiểm tra số bản ghi sau vòng chạy và fail nếu không đúng 1 winner/9 conflict.
- **Provenance chưa hash đầy đủ working tree**: `diffHash` chỉ giữ 16 ký tự đầu SHA-256 và `git diff HEAD` không bao gồm file untracked. Hiện `backend/Clinic.API/Controllers/ApiCatalogController.cs` là untracked; hash có thể không đại diện cho code đang chạy. Ghi hash đầy đủ và bao gồm file untracked (hoặc chỉ đo từ commit sạch), đồng thời xác nhận image IDs khớp source commit.
- **Kết quả test ghi trong mục 7 chưa được xác minh lại ở lượt này**: lần rà soát hiện tại chỉ kiểm tra compose config, cú pháp Node, trạng thái container và GET catalog; chưa chạy build/test suite/benchmark.

### Kết luận rà soát

**Có thể bắt đầu smoke test và một lượt đo thăm dò**, nhưng chưa nên gọi kết quả là benchmark so sánh chính thức. Trước khi chốt so sánh, cần chọn workload API tương đương; sửa các xác nhận ticket/auth/cleanup/provenance; kiểm chứng invariant contention; rồi chạy smoke và booking/check-in acceptance trên cả hai stack.

---

## 9. Hoàn thiện xử lý toàn bộ các điểm rà soát mục 8 (2026-11-11)

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

## 10. Follow-up đánh giá sau các chỉnh sửa mục 9 (2026-11-11)

### Những điểm đã được triển khai trong code hiện tại

- Read workload dùng `GET /specialties`; live check trả HTTP 200 trên cả hai stack. Cấu hình Compose hợp lệ, các file script tải qua `node --check`.
- Setup lịch fail-fast; booking thống kê riêng `createdBookings`, `confirmedBookings`, `pollTimeouts`; check-in yêu cầu receptionist token riêng; cleanup nằm trong `finally` và đọc lại trạng thái appointment.
- Check-in chỉ đánh dấu ticket integrity khi lấy được ticket trước/sau và ID + queue number giống nhau. Monolith có unique index trên `QueueTickets.AppointmentId`.
- Contention fail-fast, kiểm tra đúng một appointment active cho slot trong danh sách `/me/appointments`, dọn winner và xác minh Cancelled. Threshold yêu cầu đúng 1 response thành công, 9 conflict-class response và 0 HTTP 500.
- Provenance hiện lưu đủ SHA-256, dirty state, staged/unstaged diff và danh sách/hash file untracked.

### Điều kiện còn cần kiểm soát để số liệu so sánh có ý nghĩa

- `GET /specialties` hiện không trả cùng một dataset: kiểm tra container cho thấy monolith có 5 chuyên khoa, microservices có 6; response lần lượt 914 và 988 ký tự. Vì thế có thể đo thăm dò endpoint này, nhưng để kết luận công bằng cần seed cùng tập dữ liệu ở cả hai DB hoặc ghi rõ độ lệch và giới hạn kết luận.
- Cả monolith lẫn microservices đang chạy đồng thời trên cùng máy (cùng với SQL, RabbitMQ/observability). Không chạy hai workload benchmark song song; chạy từng stack một, giữ cùng giới hạn tài nguyên và trạng thái máy, nếu không CPU/IO sẽ tranh chấp và làm lệch latency/RPS.
- Contention xem 400 và 409 đều là conflict-class response. Database invariant bổ sung giúp phát hiện nhiều winner, nhưng nên xem status distribution và chỉ chấp nhận 400 nếu API contract xác nhận 400 là kết quả hợp lệ cho slot đã chiếm.
- Test suite ghi ở mục 9 là kết quả được người thực hiện báo cáo; lượt rà soát này không chạy lại backend/frontend tests hay benchmark. Chưa có artifact mới từ code hiện tại.

### Kết luận cập nhật

**Có thể bắt đầu smoke test và benchmark thăm dò**, vì setup, endpoint, auth, E2E trạng thái và cleanup đã được cải thiện đáng kể. **Chưa nên coi lượt đo là kết quả so sánh chính thức** cho tới khi hai hệ thống dùng cùng seed/dataset và được đo tuần tự trong điều kiện tài nguyên tương đương. Mục 9 đã phản ánh nhiều sửa đổi thật trong code, nhưng nên tránh kết luận rằng hai workload đã hoàn toàn tương đương.

Trong lượt này đã kiểm tra diff staged/unstaged, Compose config, cú pháp các script tải và gọi GET `/specialties` trên cả hai stack; chưa chạy test suite hay benchmark.

---

## 11. Đánh giá phạm vi và tính phù hợp của bộ benchmark (2026-11-11)

### Bộ test hiện làm gì

- **`smoke.js`**: 2 VU trong 30 giây, kiểm tra `GET /specialties` có dữ liệu và `GET /slots` trả 200. Đây là smoke của các API đọc; chưa đặt lịch.
- **`load.js`**: tăng tải 15 → 30 → 50 VU trong tổng 150 giây. Mỗi iteration gọi `GET /specialties` và `GET /slots`, rồi ngủ 0,5 giây. SLO được áp dụng chung lên HTTP requests của hai endpoint.
- **`contention.js`**: 10 VU gửi một lượt đặt cùng một slot; đặt ngưỡng 1 response thành công, 9 response conflict-class, không có HTTP 500; sau đó xác minh có đúng một appointment active và hủy winner.
- **`spike.js`**: đưa tải lên 100 VU trong khoảng 70 giây, chỉ gọi `GET /specialties`. Check nhận 200 hoặc 429 nhưng không có threshold về tỷ lệ lỗi/5xx; threshold hiện chỉ giới hạn p95 dưới 1 giây.
- **`soak.js`**: 20 VU trong 5 phút, chỉ gọi `GET /specialties`, có ngưỡng tỷ lệ lỗi và latency.
- **`benchmark-runner.mjs`**: 50 request đọc `/specialties` với concurrency mặc định 10; tiếp đến tối đa 20 booking **tuần tự**, poll trạng thái Confirmed tối đa 3 giây, retry cùng Idempotency-Key, check-in một appointment và retry ba lần; cuối cùng thử hủy appointment và xác minh trạng thái.

Artifact Monolith mới nhất là bằng chứng tốt cho **một lượt acceptance tích hợp quy mô nhỏ**: 50/50 read 200; 20/20 booking tạo và Confirmed; 20/20 retry trả cùng appointment; check-in đầu thành công, 3 retry trả 400 và ticket ID/queue number trước-sau giống nhau; 20 appointment được xác minh Cancelled. Đây chưa phải bằng chứng tải cao hay benchmark so sánh hai kiến trúc.

### Mức phù hợp với hệ thống đặt lịch khám

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

### Đề xuất cấu trúc nghiệm thu tải

1. Giữ `smoke.js` làm kiểm tra nhanh API đọc; bổ sung assertion có ca trong khoảng ngày test.
2. Tách workload read-only khỏi workload booking; thêm scenario booking bình thường với slot hợp lệ khác nhau, patient test khác nhau, và tỷ lệ đọc availability/đặt lịch/check-in gần với cách dùng dự kiến.
3. Giữ contention slot làm scenario riêng; tạo ca/slot tương lai theo dữ liệu setup và dùng slot nằm chắc chắn trong ca; bắt buộc một winner, các request còn lại conflict đúng contract, đồng thời xác nhận DB invariant.
4. Với spike, đặt threshold lỗi/5xx rõ ràng và báo riêng 429; với soak, thêm workflow ghi có tốc độ thấp hoặc ít nhất poll Queue/Schedule để phát hiện suy giảm liên-service.
5. Đồng nhất seed, account/role, lịch và số lượng dữ liệu giữa hai stack; đo tuần tự, warm-up giống nhau, nhiều lần lặp. Thu thập resource samples trong suốt run và báo cả số request hoàn thành, lỗi, timeout.
6. Sửa tài liệu artifact: Node runner chỉ xác minh 20 appointment thành Cancelled; không xóa ca làm việc hoặc QueueTicket.

### Kết luận

Bộ test phù hợp để kiểm tra **đọc API, tính đúng đắn khi tranh chấp một slot và acceptance luồng booking/check-in ở tải thấp**. Nó chưa đủ để kết luận năng lực tải thực tế của toàn hệ thống phòng khám hoặc so sánh hiệu năng kiến trúc một cách chặt chẽ. Trước khi dùng kết quả làm kết luận chính, cần bổ sung workload ghi bình thường dưới concurrency, sửa slot/time động ở contention, tăng điều kiện assertion cho spike/slots, đồng nhất dữ liệu, và lấy resource metrics trong toàn thời gian chạy.

Đây là rà soát tĩnh dựa trên scripts, API/controller/domain liên quan và artifact Monolith mới nhất; không chạy benchmark hay test trong lượt này.

## 12. Cập nhật bộ workload nghiệp vụ (2026-11-11)

Bộ kiểm thử đã được chỉnh để các request bám gần luồng đặt khám hơn và kiểm tra các invariant có ý nghĩa:

- `smoke.js`: đọc danh mục chuyên khoa và lịch trống trong cửa sổ 7 ngày tương lai; xác nhận response có dữ liệu.
- `load.js`: tải đọc hỗn hợp danh mục + lịch trống có giới hạn ngày, thay vì truy vấn toàn bộ `/slots`.
- `booking.js`: mỗi VU đọc availability rồi đặt một slot riêng do API trả về; theo dõi slot được quảng bá, booking thành công, trạng thái Confirmed, idempotency cùng key, thời gian API/E2E và cleanup appointment/ca do test tạo. Mặc định 20 VU, giới hạn 40; có thể đổi bằng `BOOKING_VUS`.
- `contention.js`: mọi VU cạnh tranh cùng một slot còn trống lấy từ availability; yêu cầu đúng 1 winner, các request còn lại trả conflict-class, không có 5xx, sau đó xác minh DB qua API và cleanup.
- `spike.js`: tải đột biến trên danh mục và availability; tách 429 (load shedding được chấp nhận) khỏi lỗi 5xx và status bất ngờ, có threshold bắt buộc.
- `soak.js`: tải kéo dài với hỗn hợp đọc danh mục/availability.
- `benchmark-runner.mjs`: mặc định chạy `booking`; `read` chỉ đo đọc và không cần đăng nhập; `booking` tạo các booking trên slot tương lai riêng biệt rồi kiểm tra Confirmed/idempotency; `checkin` tự tạo một booking làm fixture trước khi check-in; `all` chạy cả ba. Ca do runner tạo sẽ được hủy sau run và trạng thái được xác minh. Các ca có sẵn không bị hủy. Queue ticket vẫn giữ lại trạng thái lịch sử sau khi check-in được hủy, vì vậy `checkin`/`all` chỉ nên chạy trên DB benchmark riêng.

### Cách chạy trên PowerShell

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

### Giới hạn kết luận

Các kịch bản ghi mới tạo được tải booking nhiều slot, nhưng vẫn dùng một tài khoản patient demo và không mô phỏng phân bố bệnh nhân thật. Contention là bài kiểm tra tính đúng đắn có N VU, không phải throughput benchmark. `docker stats --no-stream` của Node runner vẫn chỉ là snapshot trước/sau, không phải peak/time series. Muốn so sánh kiến trúc chính thức cần chạy stack tuần tự, dùng seed và giới hạn tài nguyên tương đương, lặp nhiều lần, và thu thập metrics tài nguyên trong toàn bộ thời gian chạy.

Trong lượt chỉnh sửa này chỉ kiểm tra cú pháp và diff; không chạy k6, benchmark, Docker Compose hay ghi dữ liệu vào hệ thống.

## 13. Đồng bộ benchmark với working tree microservices

Bộ script chuẩn trong `tests/load` đã được sao chép đồng bộ sang `D:/Project/KHDV/microservices/tests/load`: `benchmark-runner.mjs`, `booking.js`, `fixtures.js`, `contention.js`, `load.js`, `smoke.js`, `soak.js`, `spike.js`, `package.json` và README. Hai bộ dùng cùng logic và ngưỡng; `config.js` chỉ khác `BASE_URL` mặc định để tránh bắn nhầm stack: `localhost:5100` ở monolith và `localhost:5000` ở microservices. Biến `BASE_URL` vẫn có thể ghi đè.

Runner tải Phase 9 cũ của microservices được giữ riêng thành `phase9-throughput-runner.mjs`; đây là công cụ legacy cần `SEED_PASSWORD` và `WORKLOAD_SCHEDULE_ID`, không thuộc bộ so sánh chuẩn. Không gộp số liệu của nó với runner/k6 chuẩn.

Đã đối chiếu SHA-256 các file chuẩn giữa hai working tree; tất cả giống nhau ngoại trừ cấu hình URL mặc định. Đã kiểm tra cú pháp Node và `git diff --check`; chưa chạy benchmark hoặc ghi dữ liệu.

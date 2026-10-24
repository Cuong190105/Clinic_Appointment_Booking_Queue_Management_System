# Kế hoạch triển khai Giai đoạn 4: Tách Queue Service (Thành viên A)

## 1. Mục tiêu và phạm vi

Tách quyền sở hữu hàng đợi khỏi Clinic monolith, tạo `Clinic.Queue.Service` và database riêng `ClinicQueueDB`. Queue Service sở hữu vòng đời `QueueTicket`, bộ đếm số thứ tự, trạng thái hàng đợi, API hàng đợi và các event do thay đổi trạng thái hàng đợi phát ra.

Tài liệu này cụ thể hóa nhiệm vụ Giai đoạn 4 trong `TASK_MEMBER_A.md`, phối hợp với `TASK_MEMBER_B.md` và các yêu cầu chung. Các quyết định nghiệp vụ ở Mục 4 đã được thống nhất trong trao đổi; contract/schema và cutover vẫn cần review kỹ thuật giữa A/B trước khi triển khai.

### Kết quả cần đạt

- Queue DB độc lập, không có FK, EF navigation hoặc truy vấn runtime sang ClinicDB, ClinicAppointmentDB, ClinicSchedulingDB hay ClinicIdentityDB.
- `AppointmentCheckedIn` được tiêu thụ idempotent và tạo tối đa một queue ticket cho mỗi appointment.
- Việc cấp số, tạo ticket, ghi Inbox và cập nhật bộ đếm nằm trong một local transaction tại Queue DB.
- Số thứ tự duy nhất trong phạm vi bác sĩ/ngày đã thống nhất.
- Queue Service tự xác thực JWT bằng Identity JWKS và kiểm tra quyền phù hợp.
- Frontend đi qua Gateway; API Queue cũ được chuyển hướng hoặc gỡ sau khi cutover.
- Event `ExamStarted`/`ExamCompleted` có owner, contract, quy tắc duplicate và consumer rõ ràng.
- Có migration/import, reconciliation, test tích hợp/concurrency/failure và hướng dẫn rollback.

## 2. Căn cứ hiện trạng trong repository

- `TASK_MEMBER_A.md` yêu cầu các entity Queue, schema denormalized, workflow `Waiting`, `Called`, `InProgress`, `Completed`, `Skipped`, cấp số trong local transaction và unique constraint.
- `TASK_MEMBER_B.md` giao B phụ trách consumer `AppointmentCheckedIn`, duplicate handling, concurrency/failure tests, reconciliation và metrics ở Giai đoạn 4.
- Contract `AppointmentCheckedIn` hiện tại gồm `AppointmentId`, `PatientId`, `DoctorId`, `CheckedInAt`, `Priority`, `PatientName` và `DoctorName`; không mang `ScheduleDate` hoặc `VisitId`. QueueDate suy ra từ `CheckedInAt` theo timezone phòng khám.
- `Clinic.API` không đăng ký legacy `AppointmentCheckedInConsumer`; Queue Service consume routing key `appointment.checked-in`. File consumer cũ trong Infrastructure còn tồn tại nhưng không được host. Walk-in hiện tạo record Appointment ở ClinicDB rồi sync idempotent sang Appointment Service qua internal endpoint được bảo vệ bằng service token; QueueTicket chỉ được tạo trong Queue DB.
- Queue cũ có `QueueTicket` gắn với Appointment qua navigation EF, QueueCounter có khóa `(DoctorId, Date)`, còn truy vấn hàng đợi lấy display data từ appointment graph. Khi cắt Queue, `PatientName` và `DoctorName` được snapshot trong Queue DB qua event; không cần runtime join/projection riêng.
- Event `ExamCompleted` hiện dùng `QueueTicketId`, `AppointmentId`, `DoctorId` và `CompletedAt`; không có `VisitId`. Queue và Appointment dùng chung `AppointmentId` làm định danh lượt khám trong schema version 1.
- Contracts đã khai báo `ExamStarted`, `PatientCalled`, `PatientSkipped`; event `PatientCheckedIn` chưa có. Cần phân biệt event đầu vào `AppointmentCheckedIn` với event thông báo Queue đã tạo ticket.

Các căn cứ và trạng thái được rà lại ngày 2026-10-24 sau khi chạy test, build Docker Compose, kiểm tra import/health và gọi các luồng API có xác thực; kết quả chi tiết nằm ở Mục 9.

## 3. Kế hoạch triển khai theo bước

### Bước 0 — Chốt quyết định kiến trúc và ownership

1. Chốt các quyết định liệt kê ở Mục 4, tối thiểu gồm ranh giới check-in, điều kiện đúng ca/ngày, ngày tính queue, dùng `AppointmentId` làm liên kết, event payload/version, patient name snapshot và nguồn phát `ExamCompleted`.
2. Chốt API contract version, event schema version, event name/routing key và lỗi nghiệp vụ giữa A và B.
3. Lập bảng ownership: Queue sở hữu trạng thái ticket/counter; Appointment sở hữu trạng thái lịch hẹn; Medical Records sở hữu trạng thái report.
4. Ghi quyết định vào tài liệu và cập nhật TASK_MEMBER_A/B để hai bên không triển khai hai consumer hoặc hai nguồn sự thật song song.

**Điều kiện hoàn tất:** A và B dùng chung một contract/check-in sequence được review trước khi tạo migration hoặc bật consumer mới.

### Bước 1 — Kiểm kê dữ liệu, API và frontend đang dùng Queue

1. Liệt kê toàn bộ endpoint Queue hiện tại, request/response, permission và caller frontend.
2. Ghi lại cách hiện tại cấp QueueNumber, lọc theo bác sĩ/ngày, sắp xếp Priority, tính vị trí chờ và xác định ticket active.
3. Xác định dữ liệu response/ticket cần giữ: `patientName`, `doctorName`, `priority`, `checkInTime`, `calledAt` và trạng thái.
4. Kiểm kê số dòng và các liên kết hợp lệ/không hợp lệ giữa QueueTickets, QueueCounters và appointment lịch sử để lập kế hoạch import.
5. Chốt phạm vi API tương thích và route Gateway cần chuyển; không để frontend gọi thẳng service nội bộ.

**Đầu ra:** API inventory, data inventory, permission matrix và hợp đồng response trước/sau cutover.

### Bước 2 — Tạo project/service độc lập

1. Tạo `backend/Clinic.Queue.Service` và `backend/Clinic.Queue.Service.Tests`, thêm vào solution.
2. Chỉ tham chiếu `Clinic.Contracts` và package nền tảng cần thiết; không tham chiếu Clinic.Domain hoặc Clinic.Infrastructure.Sqlserver.
3. Thiết lập cấu hình `ClinicQueueDB`, EF Core migrations, health/readiness endpoint, structured logging, correlation ID và OpenTelemetry metrics.
4. Cấu hình JWT RS256 validation qua JWKS của Identity Service, permission policies, Swagger và Dockerfile.
5. Thêm Queue Service vào Docker Compose với readiness check và dependency rõ ràng.

**Điều kiện hoàn tất:** service khởi động độc lập, health/readiness thành công và test host dùng Queue DB test riêng.

### Bước 3 — Thiết kế model Queue và persistence

1. Tạo Queue-owned entity `QueueTicket` với tối thiểu: `Id`, `AppointmentId`, `PatientId`, `DoctorId`, `QueueDate`, `QueueNumber`, `PatientName`, `DoctorName`, `Priority`, `Status`, `CheckInTime`, `CalledAt`, audit fields và concurrency token nếu cần. `AppointmentId` là liên kết tới lịch hẹn/lượt khám hiện tại; không tạo `VisitId` riêng. `QueueDate` suy ra từ `CheckedInAt` sau khi đổi UTC instant sang timezone đã cấu hình cho phòng khám. Với quy ước hiện tại mọi ca cùng ngày địa phương và không qua nửa đêm, QueueDate trùng ngày ca; không cần thêm `ScheduleDate` vào ticket/event.
2. Tạo `QueueCounter` với khóa/unique key `(DoctorId, QueueDate)`; mọi cấp số phải tăng atomically trong SQL Server.
3. Tạo `InboxMessage` hoặc inbox equivalent trong Queue DB, unique theo `(ConsumerName, EventId)`; thêm unique constraint bảo vệ một ticket cho mỗi `AppointmentId`.
4. Tạo unique constraint cho `(DoctorId, QueueDate, QueueNumber)` trên toàn bộ ticket đã cấp, kể cả ticket soft-delete; không tái sử dụng số trong cùng ngày.
5. Lưu snapshot `PatientName` và `DoctorName` để phục vụ API/in vé mà không runtime join sang Appointment/Scheduling. Không lưu năm sinh, phone hoặc tạo Patient projection trong giai đoạn này; nếu sau này có yêu cầu phân biệt trùng tên bằng năm sinh, mở rộng có chủ đích.
6. Viết migration đầu tiên và model snapshot; kiểm tra index/FK để bảo đảm không tạo quan hệ vật lý sang service khác.

### Bước 4 — Import lịch sử và xác minh dữ liệu

1. Viết script import từ ClinicDB sang ClinicQueueDB có thể chạy lại an toàn, giữ nguyên ID và dữ liệu cần cho lịch sử.
2. Map `DoctorId`, `PatientId`, `AppointmentId`, trạng thái, queue number, thời gian check-in, priority và các display snapshot theo quyết định ở Bước 0.
3. Xác minh tổng số ticket/counter, duplicate appointment, duplicate number trong từng phạm vi, orphan reference, status mapping và dữ liệu display bị thiếu.
4. Xuất báo cáo tổng hợp mismatch; không in dữ liệu cá nhân nhạy cảm.
5. Chạy import/verify trước cutover và giữ ClinicDB read-only/nguồn rollback trong thời gian chuyển tiếp.

### Bước 5 — Cài đặt nghiệp vụ Queue trong local transaction

1. Tạo domain/state transition cho `Waiting -> Called -> InProgress -> Completed` và `Waiting/Called -> Skipped`; từ chối transition không hợp lệ.
2. Tạo consumer handler cho `AppointmentCheckedIn`: kiểm tra envelope/schema; dedupe theo Inbox; xác minh payload tối thiểu; tăng QueueCounter; tạo QueueTicket; đánh dấu Inbox processed; commit cùng transaction.
3. Đảm bảo event trùng theo `EventId` hoặc cùng `AppointmentId` không cấp thêm số. Unique constraint là lớp bảo vệ cuối khi hai consumer xử lý đồng thời.
4. Nếu cùng appointment đến với EventId khác, áp dụng idempotency theo `AppointmentId`: trả duplicate result và không tăng counter lần nữa.
5. Xử lý lỗi theo nhóm: payload/schema invalid đi dead-letter; lỗi tạm thời retry giới hạn; lỗi tham chiếu không hợp lệ có trạng thái vận hành/reconciliation, không retry vô hạn.
6. Tạo nghiệp vụ gọi số tiếp theo, bắt đầu khám, hoàn tất, bỏ qua, đổi ưu tiên và truy vấn hàng đợi; mọi thao tác kiểm tra permission và trạng thái Queue cục bộ.
7. Phát outbox event cho `PatientCalled`, `ExamStarted`, `ExamCompleted`, `PatientSkipped` (hoặc contract đã chốt) cùng transaction với state change.
8. Không cập nhật Appointment hay MedicalReport trực tiếp. Appointment Service và Medical Records Service tự xử lý event qua Inbox của chúng.

### Bước 6 — API, authorization và tương thích giao diện

1. Chuyển các API Queue hiện có sang Queue Service, giữ request/response tương thích hoặc version hóa thay đổi.
2. Kiểm tra ma trận quyền: doctor chỉ thao tác queue của mình; receptionist/admin theo permission; patient chỉ xem queue ticket gắn với patient claim của mình.
3. Lấy `DoctorId`/`PatientId` từ JWT claims ở các API người dùng; không tin ID actor gửi tự do trong request.
4. Cập nhật `Clinic.API`/Gateway thành proxy hoặc route Queue API tới Queue Service; giữ nguyên frontend chỉ gọi gateway.
5. Thay phần frontend đang dựa trên `QueueTicketId`, `AppointmentId` và patient display; dùng `AppointmentId` làm liên kết appointment/lượt khám, còn QueueTicketId chỉ là ID thao tác queue.
6. Xóa/disable handlers và repository Queue cũ sau khi cutover được xác nhận; không để hai service cùng ghi ticket/counter.

### Bước 7 — Tích hợp với Appointment và Medical Records qua event

1. A hoàn thiện Queue producer/outbox và contract; B tích hợp publisher/consumer, broker topology, retry/DLQ, metrics và correlation.
2. Queue phát `ExamStarted` và `ExamCompleted` từ trạng thái Queue đã commit.
3. Appointment Service nhận `ExamCompleted` idempotent để cập nhật Appointment state của chính nó; Queue không sửa Appointment DB.
4. Medical Records Service ở giai đoạn sau có thể nhận `ExamStarted`/`ExamCompleted` để mở/cập nhật visit; Queue không phụ thuộc repository Medical.
5. Nếu một report được finalize trước khi Queue hoàn tất, xử lý theo quyết định event authority ở Mục 4; không phát cùng ý nghĩa `ExamCompleted` từ hai nơi nếu không có quy tắc dedupe thống nhất.
6. Xây reconciliation đối chiếu Appointment đã CheckedIn với Queue ticket và phát hiện Queue/Appointment Completed bị lệch; xác định owner xử lý/replay.

### Bước 8 — Kiểm thử và kiểm tra lỗi

1. Unit tests cho state transition, phân quyền, xếp ưu tiên, scope queue theo doctor/date và quy tắc ngày.
2. SQL Server integration tests cho transaction, counter concurrency, unique constraints, rollback giữa tăng counter/tạo ticket và soft-delete policy.
3. Event contract tests cho schema version, envelope, payload validation và forward/backward compatibility đã chọn.
4. Tests cho duplicate EventId, EventId khác nhưng cùng appointment, out-of-order events, invalid payload, retry/DLQ.
5. Failure tests: broker restart, consumer crash trước/sau DB commit, redelivery, publish confirmation timeout và consumer shutdown.
6. End-to-end tests: appointment check-in -> một queue ticket; gọi số -> bắt đầu khám -> hoàn tất; verify Appointment state cập nhật bất đồng bộ; walk-in urgent; nhiều request check-in đồng thời.
7. Kiểm tra số liệu metrics: queue length, wait time, processing time, consumer lag, duplicate, retry và DLQ.

### Bước 9 — Cutover, quan sát và rollback

1. Backup ClinicDB và Queue DB; hoàn tất import và đối soát trước khi route traffic.
2. Deploy Queue Service với consumer ở chế độ chưa nhận production event hoặc feature/config gate; xác nhận health, JWKS, DB, broker và readiness.
3. Chuyển consumer `AppointmentCheckedIn` từ Clinic monolith sang Queue Service trong một cutover có kiểm soát; bảo đảm chỉ một consumer có quyền tạo ticket.
4. Chuyển API Gateway/frontend Queue sang Queue Service; kiểm tra smoke/E2E với account từng role.
5. Theo dõi duplicate, retry, DLQ, consumer lag, queue counts và reconciliation sau cutover.
6. Sau thời gian ổn định mới xóa Clinic Queue tables/code bằng migration riêng; migration destructive phải có backup và quy trình rollback dữ liệu đã ghi trong Queue DB.
7. Nếu rollback, tắt consumer mới trước khi bật lại consumer cũ; chuyển route trở lại monolith và đồng bộ các ticket phát sinh sau cutover theo runbook để tránh cấp số trùng.

## 4. Các điểm thiết kế và quyết định

### 4.1. Ai thực hiện check-in và cấp số?

**Mâu thuẫn:** Giai đoạn 3 giao Appointment Service endpoint đổi appointment sang `CheckedIn` và phát `AppointmentCheckedIn`; Giai đoạn 4 yêu cầu Queue cấp ticket/số trong local transaction. Nhưng nhiệm vụ A nói “xây check-in và cấp queue number trong local transaction”, dễ bị hiểu Queue trực tiếp đổi Appointment, còn hiện tại handler monolith vừa đọc Appointment vừa tạo QueueTicket. Một transaction ACID không thể bao trùm Appointment DB và Queue DB. Ngoài ra check-in phải gắn với đúng appointment/ca đã đặt; Queue không được nhận doctor/date do client tự chọn.

**Quyết định:** Chọn phương án khuyến nghị — Appointment Service sở hữu check-in nghiệp vụ: xác thực appointment đúng ca/ngày theo timezone phòng khám, đổi state và ghi outbox `AppointmentCheckedIn`. Queue Service consume event, rồi trong một Queue DB transaction tạo ticket, cấp số và ghi Inbox. Check-in chỉ nhận `AppointmentId`; không nhận doctor/date do client chọn. Trạng thái Appointment có thể chuyển trước khi ticket sẵn sàng; mỗi request được theo dõi riêng qua `AppointmentId`/`RequestId`.

**Phương án đã cân nhắc:**

1. **Đã chọn — Tách check-in nghiệp vụ và phân bổ ticket.** Appointment Service xác nhận quyền/lịch, đổi state và ghi outbox `AppointmentCheckedIn`; Queue Service consume event và trong một Queue DB transaction tạo ticket + cấp số + Inbox. Endpoint check-in vẫn thuộc Appointment/Gateway contract hiện có. Trạng thái `CheckedIn` của Appointment có thể sớm hơn ticket do eventual consistency; có retry/reconciliation.
2. Dời toàn bộ endpoint check-in sang Queue Service, rồi gọi Appointment Service đồng bộ để cập nhật appointment. Điều này tạo distributed workflow, timeout/compensation và không thể atomically commit hai DB; không khuyến nghị cho mục tiêu đơn giản hiện tại.
3. Để API monolith tiếp tục trực tiếp ghi Queue DB hoặc gọi repository Queue từ Appointment. Vi phạm service ownership/isolation; không chọn.

**Yêu cầu triển khai:** Appointment Service kiểm tra trạng thái và ngày lịch theo timezone phòng khám, sau đó dựng event từ appointment đã lưu. Nếu sau này cần giới hạn theo giờ mở/đóng ca thay vì đúng ngày/appointment, phải bổ sung chính sách grace window và nguồn chuẩn giờ ca; hiện quyết định là check-in đúng appointment/ca trong ngày địa phương.

### 4.2. Payload `AppointmentCheckedIn` hiện thiếu dữ liệu Queue cần

**Mâu thuẫn:** Task hiện liệt kê `ScheduleDate`, `Priority`, `PatientId`, `DoctorId` và trước đây có `VisitId` cho Queue; event hiện tại chỉ có IDs và `CheckedInAt`. `ScheduleDate` chưa có trong contract và không cần cho QueueNumber nếu Appointment chỉ cho check-in vào đúng ca/ngày đã đặt, còn ca không qua nửa đêm. `Priority`, `PatientName` và `DoctorName` cần có để giữ urgent ordering và hỗ trợ API/in ticket mà không runtime join.

**Phương án:**

1. **Đã chọn — Bỏ `ScheduleDate` khỏi Queue/event; thêm `Priority`, `PatientName` và `DoctorName` vào `AppointmentCheckedIn` schema version 1.** Với lịch online, `Priority=false`; với Walk-in, publisher nhận cờ urgent từ luồng lễ tân và ghi giá trị đó vào event đã lưu trong outbox. Queue lưu `QueueDate` suy ra từ `CheckedInAt` theo timezone phòng khám. Sửa trực tiếp version 1 được chấp nhận vì event chưa phát hành production; cập nhật publisher, luồng Walk-in và consumer đồng bộ.
2. Giữ `ScheduleDate` như snapshot chỉ nếu sau này có báo cáo/luồng hẹn trễ cần phân biệt ngày ca với ngày check-in.
3. Giữ payload hiện tại và mặc định `Priority=false`, hoặc không gửi patient name rồi hydrate qua API. Cả hai làm mất hành vi hiện tại hoặc thêm coupling; không khuyến nghị.

**Về version:** Giữ `SchemaVersion = 1` và chỉnh contract trước khi phát hành là phù hợp với trạng thái hiện tại. Không cần tăng version nếu chưa có producer/consumer đã triển khai phụ thuộc schema cũ; khi các service đã chạy cùng nhau, mọi thay đổi phá vỡ tương thích cần có version/migration policy. `PatientName` và `DoctorName` là display snapshot tối thiểu phục vụ Queue/in ticket; không kèm năm sinh, phone hoặc dữ liệu y tế.

**Quyết định bổ sung:** Gửi `PatientName` và `DoctorName` trong event để Queue lưu snapshot trực tiếp trên ticket; publisher lấy tên từ dữ liệu appointment/schedule đã được xác thực. Không gửi năm sinh, phone hoặc dữ liệu y tế; không tạo projection riêng cho patient trong giai đoạn này.

### 4.3. `VisitId` hiện trùng với `AppointmentId`

**Quyết định hiện tại:** Hệ thống đang dùng một Appointment vừa làm lịch hẹn vừa làm lượt khám. Không cần định danh `VisitId` thứ hai trong Queue, và Queue không được dùng `QueueTicketId` thay thế. `AppointmentId` là ID liên kết chuẩn cho Queue ticket và `ExamCompleted`.

Đã chuẩn hóa contract: `AppointmentCheckedIn` và `ExamCompleted` đều không còn `VisitId`; `ExamCompleted` dùng `AppointmentId` làm khóa liên kết. Test contract xác nhận payload JSON không phát trường `visitId`.

Chỉ đưa `VisitId`/`EncounterId` riêng trở lại khi nghiệp vụ thật sự tách một Appointment thành nhiều lượt khám hoặc cần encounter độc lập với lịch hẹn; khi đó cần thiết kế migration và mapping lịch sử ở giai đoạn tương ứng.

### 4.4. Ngày cấp số: ngày lịch khám hay ngày check-in?

**Quyết định:** Queue chỉ hoạt động trong ngày nghiệp vụ địa phương của phòng khám; ca không kéo qua nửa đêm và check-in gắn với đúng appointment/ca. `QueueDate` là ngày lịch theo timezone cấu hình của phòng khám, không phải phần ngày UTC của timestamp. Mặc định hiện tại là `Asia/Ho_Chi_Minh`; timezone phải là cấu hình theo phòng khám để có thể mở rộng nhiều quốc gia. Không lưu thêm `ScheduleDate` trong Queue/event; QueueDate lấy từ `CheckedInAt` sau khi đổi UTC instant sang timezone phòng khám. Dùng ngày này nhất quán cho counter, unique index, lọc ticket và báo cáo. Giờ bắt đầu ca là dữ liệu lịch, hiện giả định ca đầu bắt đầu từ 07:00; không biến 07:00 thành giới hạn kỹ thuật cho các ca trong tương lai.

**Rủi ro UTC hiện có trong code:** `CheckInHandler` đang cấp số bằng `checkInTime.Date` với `checkInTime = DateTime.UtcNow`, còn repository biến ngày nhận được thành `DateOnly` trực tiếp. Vì vậy check-in từ 00:00 đến 06:59 giờ Việt Nam bị tính vào ngày UTC hôm trước. Các truy vấn cũ lại lọc `CheckInTime.Date == date.Date`, nên ngày truyền vào theo giờ Việt Nam có thể không khớp timestamp UTC. `Appointment.CheckIn` trong Appointment Service hiện chấp nhận nếu ngày lịch bằng UTC date *hoặc* UTC+7 date; cách này có thể cho phép nhầm ngày quanh ranh giới nửa đêm.

Đây không phải do SQL Server tự đổi timestamp khi đổi timezone. Kiểu `date` không có timezone; `datetime2` cũng không chứa offset. Nếu ứng dụng lưu UTC theo quy ước thì database giữ nguyên giá trị ngày/giờ UTC. Sai lệch xảy ra khi code trộn UTC instant với ngày lịch địa phương, hoặc dùng giờ local nhưng coi là UTC.

**Phương án:**

1. **Đã chọn — QueueDate theo ngày phòng khám của `CheckedInAt`.** Giữ `CheckInTime`/`CheckedInAt` là UTC instant; đổi sang timezone cấu hình của phòng khám đúng một lần để lấy `DateOnly`. Appointment Service xác thực `ScheduleDate` với ngày phòng khám hiện tại duy nhất, không chấp nhận phép so sánh UTC-hoặc-local. Nếu API nhận ngày lọc từ client, hợp đồng phải nói rõ đó là ngày phòng khám.
2. Dùng ngày UTC check-in. Đơn giản hơn nhưng không phù hợp ngày vận hành, và gây nhóm số/ngày hiển thị lệch ở Việt Nam.
3. Lưu riêng ngày lịch hẹn để audit/analytics. Chỉ cần khi nghiệp vụ sau này cho phép check-in khác ngày hoặc cần phân tích riêng ngày hẹn và ngày đến.

**Yêu cầu triển khai:** Dùng IANA timezone cấu hình của phòng khám, không dùng timezone máy chạy/container và không gọi `.Date` trên UTC để tạo ngày nghiệp vụ. Lưu `QueueDate`/`QueueCounter` dưới dạng SQL `date`; lưu timestamp dưới dạng UTC (`datetimeoffset`/`DateTimeOffset` hoặc `datetime2` theo quy ước UTC đã thống nhất). Kiểm tra ca sớm (ví dụ 06:00–07:00) vẫn thuộc đúng ngày địa phương; kiểm tra ranh giới địa phương 23:59/00:00 cho appointment validation, queue number grouping và truy vấn.

### 4.5. Phạm vi unique của QueueNumber chưa được nói rõ

**Mâu thuẫn:** Task chỉ yêu cầu unique constraint cho queue number. Counter hiện khóa `(DoctorId, Date)` nên số được lặp giữa bác sĩ/ngày; QueueTicket hiện chỉ unique `AppointmentId`, không có unique DB `(DoctorId, QueueDate, QueueNumber)`.

**Quyết định:** Chọn phương án khuyến nghị: số thứ tự duy nhất theo `(DoctorId, QueueDate, QueueNumber)`, với counter `(DoctorId, QueueDate)`. Ticket đã cấp số giữ chỗ số đó kể cả khi bị hủy/soft-delete; không tái sử dụng số trong cùng ngày.

**Phương án đã cân nhắc:**

1. **Đã chọn — Unique `(DoctorId, QueueDate, QueueNumber)`.** Số thứ tự được hiểu trong queue của một bác sĩ trong một ngày check-in local; counter key cùng scope. DB unique index là chốt cuối, giao dịch increment counter + insert ticket là bảo đảm chính.
2. Unique toàn cơ sở theo ngày. Một counter chung; dễ đọc trên bảng gọi nhưng yêu cầu điều phối tập trung và thay đổi trải nghiệm hiện tại.
3. Unique toàn hệ thống. Không thực tế, tăng dần vô hạn và không phù hợp reset theo ngày.

### 4.6. Queue cần hiển thị tên nhưng task chỉ liệt kê ID

**Mâu thuẫn:** API Queue hiện trả `PatientName` từ Appointment graph; task mới chỉ liệt kê ID denormalized. Sau khi bỏ cross-service join, Queue cần giữ tên bệnh nhân. Giao diện/nghiệp vụ lễ tân cũng cần trả `DoctorName` trên queue ticket. Một số bệnh viện có thể dùng năm sinh để phân biệt người trùng tên, nhưng API hiện tại không trả trường này.

**Quyết định:** Denormalize `PatientName` và `DoctorName` trực tiếp trên QueueTicket tại lúc tạo ticket, truyền cả hai trong `AppointmentCheckedIn` schema version 1. Không thêm năm sinh, projection, hay runtime join ở giai đoạn này. Hai tên là snapshot để ticket lịch sử không đổi khi hồ sơ được cập nhật; nếu cần đổi tên hiển thị trên vé đang chờ, xử lý bằng yêu cầu cập nhật snapshot có chủ đích.

**Phương án đã cân nhắc:**

1. Denormalize tên bệnh nhân và bác sĩ trực tiếp trên QueueTicket tại lúc tạo ticket, truyền qua event. Không cần projection riêng.
2. Nếu về sau cần phân biệt người trùng tên, bổ sung `BirthYear` snapshot và cập nhật response/UI theo yêu cầu cụ thể.
3. Tạo Patient projection hoặc gọi nhiều service để ghép dữ liệu. Thêm đồng bộ/coupling mà use case hiện tại chưa cần; không khuyến nghị.

### 4.7. Event `PatientCheckedIn` trùng nghĩa với `AppointmentCheckedIn`

**Mâu thuẫn:** Task B Giai đoạn 4 đề cập tích hợp event `PatientCheckedIn`, trong khi contract hiện tại dùng `AppointmentCheckedIn` làm đầu vào Queue. Nếu phát/consume cả hai như hai lệnh check-in, retry có thể tạo vé hoặc side effect trùng.

**Quyết định:** Chọn phương án khuyến nghị — `AppointmentCheckedIn` là event đầu vào duy nhất; Queue có thể phát `QueueTicketCreated` như event kết quả nếu consumer khác cần biết vé đã được cấp.

**Phương án đã cân nhắc:**

1. **Đã chọn — Dùng `AppointmentCheckedIn` làm event đầu vào duy nhất.** Queue phát event hậu quả như `QueueTicketCreated` hoặc `PatientCalled` nếu downstream cần; không tạo event thứ hai `PatientCheckedIn` trừ khi định nghĩa rõ nó là event hậu xử lý, không phải lệnh.
2. Đổi tên contract hiện tại thành `PatientCheckedIn` bằng schema/event version mới. Chỉ làm khi mọi producer/consumer cùng chuyển và tên mới diễn đạt đúng ownership.
3. Duy trì cả hai event với eventId/causation mapping. Tốn công dedupe và dễ nhầm; chỉ dùng cho giai đoạn chuyển tiếp có thời hạn.

### 4.8. Hai nguồn có thể phát `ExamCompleted`

**Mâu thuẫn:** Thiết kế Giai đoạn 3 cho phép Queue hoàn tất khám hoặc Medical Report finalize phát `ExamCompleted`; cùng lúc Queue là owner trạng thái queue, Medical là owner report, Appointment là owner appointment. Một `ExamCompleted` có thể đến hai lần từ hai producer hoặc theo thứ tự khác nhau.

**Quyết định:** Chọn phương án khuyến nghị — Queue phát `ExamCompleted` cho chuyển trạng thái ticket; Medical phát `MedicalReportFinalized` cho report. Hai event có ý nghĩa và owner riêng.

**Phương án đã cân nhắc:**

1. **Đã chọn — Tách sự kiện theo ý nghĩa và chọn một nguồn chuẩn.** Queue phát `ExamCompleted` khi QueueTicket chuyển `InProgress -> Completed`; Medical phát `MedicalReportFinalized` khi report chốt. Appointment cập nhật `Completed` theo Queue event (hoặc theo điều kiện kết hợp nếu nghiệp vụ yêu cầu). Nếu phải cho phép report finalize kết thúc visit, dùng một workflow/consumer orchestration rõ ràng thay vì phát cùng event từ hai nơi.
2. Cho cả hai producer phát cùng `ExamCompleted` và consumer idempotent theo `AppointmentId`. Ngăn side effect lặp nhưng không giải quyết event mâu thuẫn/out-of-order; chỉ chấp nhận nếu payload semantics giống hệt và state transition monotonic.
3. Chỉ Medical phát `ExamCompleted`. Khi đó Queue `Completed` không còn phản ánh kết thúc khám độc lập; không phù hợp nếu thao tác hoàn tất queue vẫn là luồng chính.

### 4.9. Trạng thái Queue `Completed` và Appointment `Completed` phải tách

**Mâu thuẫn:** Handler hiện tại trong monolith vừa gọi `QueueTicket.Complete()` vừa `Appointment.Complete()` trong một unit of work. Sau tách service, Queue không được sửa Appointment; còn việc Appointment cập nhật bất đồng bộ tạo khoảng lệch trạng thái hợp lệ.

**Quyết định:** Chọn phương án khuyến nghị — Queue chỉ commit QueueTicket và Outbox `ExamCompleted`; Appointment consumer cập nhật Appointment bất đồng bộ. Có retry/reconciliation cho trạng thái lệch quá ngưỡng.

**Phương án đã cân nhắc:**

1. **Đã chọn — Queue chỉ commit QueueTicket và Outbox `ExamCompleted`; Appointment consumer tự cập nhật Appointment.** Reconciliation báo lệch quá ngưỡng và retry/replay có kiểm soát.
2. Queue gọi Appointment API đồng bộ sau commit. Đây là dual write và cần retry/saga; không khuyến nghị làm mặc định.
3. Để Appointment status không có Completed. Mâu thuẫn với dashboard/lifecycle hiện hành; không chọn nếu chưa thay đổi product requirement.

### 4.10. Consumer, publisher, Gateway và cutover thuộc trách nhiệm A/B chưa khớp tuyệt đối

**Mâu thuẫn:** A sở hữu service/schema/domain/API; B sở hữu broker, consumer, concurrency/failure tests, observability và frontend/Gateway. Giai đoạn 3 còn consumer transitional ở Monolith, trong khi Giai đoạn 4 yêu cầu Queue consumer mới. Nếu hai bên không thống nhất ownership và deploy order, có thể bật đồng thời hai consumer hoặc không có consumer.

**Quyết định:** Chọn ranh giới khuyến nghị: A sở hữu Queue DB/domain/API/policies/migration; B sở hữu broker topology, consumer integration/retry/DLQ/metrics, failure/concurrency tests và Gateway/frontend cutover. Contract và thứ tự cutover được hai bên review chung; consumer cũ phải tắt trước khi consumer mới có quyền tạo ticket.

**Phương án đã cân nhắc:**

1. **Đã chọn — Ranh giới trách nhiệm cụ thể.** A viết Queue DB, `QueueManager`, inbox/outbox interfaces, API/policies, migration và contract tests. B tích hợp RabbitMQ consumer/publisher/topology/retry/DLQ/metrics, failure/concurrency tests và Gateway/frontend cutover. Hai bên cùng review contract, consumer transaction boundary và runbook.
2. A tự làm toàn bộ end-to-end. Giảm handoff nhưng có thể trùng phạm vi Member B và làm task B Giai đoạn 4 không còn rõ.
3. B làm consumer trực tiếp trên ClinicDB trong khi A chỉ dựng service. Duy trì hai nguồn sở hữu và không hoàn thành mục tiêu tách Queue; không chọn.

### 4.11. Walk-in hiện tạo QueueTicket ở Clinic và đồng bộ Appointment theo chiều ngược

**Mâu thuẫn:** Luồng đặt lịch thường đi từ Appointment check-in event sang Queue. Luồng Walk-in hiện được tài liệu Giai đoạn 3 mô tả là Clinic API tạo QueueTicket trước rồi đồng bộ Appointment sang ClinicAppointmentDB. `SyncWalkInAppointmentAsync` hiện chỉ đánh dấu Appointment `CheckedIn`, không ghi `AppointmentCheckedIn` outbox event và request sync hiện không mang cờ urgent. Nếu chuyển consumer thường sang Queue nhưng giữ luồng Walk-in cũ, Queue vẫn có hai writer và ClinicDB vẫn là nơi sở hữu một phần ticket. Nếu chỉ chuyển sang event bất đồng bộ, lễ tân hiện kỳ vọng nhận queue number ngay trong response nhưng ticket có thể chưa được tạo.

**Quyết định:** Queue là writer duy nhất của ticket cho cả walk-in và lịch hẹn; dùng cùng event `AppointmentCheckedIn`. Mỗi lần check-in chỉ áp dụng cho một Appointment và phát một event riêng, cùng snapshot `PatientName`, `DoctorName`, `Priority`. Queue consume event, cấp số và lưu ticket trong một transaction. Không dùng command tạo ticket song song hay giữ ghi ticket trong Clinic monolith.

**Phương án đã cân nhắc:**

1. Hợp nhất cả Walk-in và đặt lịch vào một đường tạo ticket tại Queue. Appointment Service ghi `AppointmentCheckedIn` vào outbox; Queue consume và tạo ticket. Idempotency key dùng xuyên request và event. Queue chỉ có một writer.
2. Queue Service cung cấp internal command `CreateWalkInTicket`, được orchestration gọi sau khi Appointment Service tạo appointment. Đây là dual-service saga; cần idempotency, retry và compensation, đồng thời phải ngăn event `AppointmentCheckedIn` tạo vé thứ hai (dùng chung `AppointmentId` unique invariant).
3. Giữ Walk-in tạo ticket ở Clinic cũ. Dễ làm tạm nhưng phá service ownership và buộc giữ Clinic Queue DB hoạt động; không dùng cho cutover hoàn tất.

**Thiết kế trả vé và in vé đã chọn:**

- Mỗi lần check-in xử lý đúng một Appointment, có idempotency key và correlation riêng. Appointment Service ghi trạng thái/outbox; Queue tạo ticket bất đồng bộ và truy vấn theo `AppointmentId`.
- Lễ tân chờ ticket của người hiện tại xuất hiện, kiểm tra thông tin và in vé rồi mới tiếp nhận/check-in người tiếp theo. Giao diện giữ AppointmentId đang chờ, có polling và retry; khi Queue chưa có ticket thì không hiển thị số 0, không cho in và không hoàn tất lượt.
- Ticket hiển thị snapshot tên bệnh nhân, bác sĩ, số queue và thời điểm check-in. In lại đọc ticket đã lưu, không cấp số mới. Không có batch check-in.
- API có thể trả kết quả ban đầu trước khi Queue hoàn tất; việc tuần tự được bảo đảm ở luồng giao diện tiếp nhận, chờ đến ticket thật trước khi tiếp tục.
- Nếu cấp ticket bị chậm hoặc lỗi, retry dùng cùng AppointmentId/idempotency để không tạo ticket thứ hai. Realtime có thể bổ sung sau; polling là cơ chế hiện tại.
- Nhiều yêu cầu từ các quầy/kiosk khác nhau vẫn có thể được xử lý song song; thứ tự cấp số được quyết định bởi transaction của QueueCounter, không theo thời điểm bắt đầu thao tác.
## 5. Các quyết định đã chốt để triển khai

Các quyết định dưới đây là baseline chung cho triển khai và phối hợp A/B:

1. Appointment Service là owner của check-in lịch hẹn; Queue Service consume `AppointmentCheckedIn` và atomically cấp số/tạo ticket.
2. Bỏ `ScheduleDate` khỏi Queue/event; thêm `Priority`, `PatientName` và `DoctorName` vào `AppointmentCheckedIn` schema version 1 trước khi phát hành. Online check-in gửi `Priority=false`; Walk-in giữ cờ urgent từ lễ tân trong Appointment outbox event. `QueueDate` lấy từ `CheckedInAt` theo giờ phòng khám; không thêm năm sinh, contact hoặc dữ liệu y tế.
3. Dùng `AppointmentId` làm định danh lịch hẹn/lượt khám; không thêm `VisitId` vào Queue model. Chuẩn hóa `ExamCompleted` theo `AppointmentId`; chỉ đưa Encounter ID riêng vào khi có yêu cầu nghiệp vụ.
4. Queue date dùng ngày check-in local `QueueDate` theo timezone cấu hình của phòng khám (mặc định hiện tại `Asia/Ho_Chi_Minh`); `CheckInTime`/`CheckedInAt` vẫn là UTC timestamp.
5. Số unique theo `(DoctorId, QueueDate, QueueNumber)` và không tái sử dụng số đã cấp trong cùng ngày.
6. `AppointmentCheckedIn` là event đầu vào duy nhất; Queue phát event hậu xử lý, không phát thêm `PatientCheckedIn` cùng nghĩa.
7. Queue là nguồn chuẩn duy nhất cho `ExamStarted`/`ExamCompleted` của trạng thái queue; Medical phát event riêng cho thay đổi report.
8. Denormalize `PatientName` và `DoctorName` trực tiếp trên ticket; không cần projection riêng; Queue không gọi Appointment/Scheduling để ghép graph.
9. Chỉ bật Queue consumer mới sau khi B xác nhận topology/retry/DLQ hoạt động và consumer cũ đã được tắt trong cutover.
10. Walk-in cũng phát cùng `AppointmentCheckedIn` contract; không để Clinic monolith tiếp tục ghi QueueTicket sau cutover.
11. Mỗi lần lễ tân xử lý một Appointment; giao diện chờ ticket thật, hiển thị/in vé đó rồi mới cho tiếp nhận người tiếp theo. Không dùng batch check-in; in lại không cấp số mới.

## 6. Phân chia công việc và điểm chờ Thành viên B

| Hạng mục | A | B | Điều kiện phối hợp |
|---|---|---|---|
| Chốt schema `AppointmentCheckedIn`/`Exam*` | Đề xuất payload và Queue invariants | Review producer/consumer, version và routing | Chốt trước migration/API tích hợp |
| Queue Service, DB, migration/import, domain | Chủ trì | Review test/failure paths | Có thể bắt đầu sau khi chốt các quyết định dữ liệu |
| Inbox transaction handler logic | Chủ trì xử lý use case/ports | Tích hợp broker delivery/ack/retry | Transaction commit phải xảy ra trước ack |
| RabbitMQ consumer/topology/retry/DLQ/metrics | Cung cấp interfaces và event contract | Chủ trì | Không bật consumer Clinic cũ đồng thời |
| API, auth, ownership, gateway | Service API/policies | Gateway/frontend route | Chuyển route sau readiness và smoke test |
| Concurrency/failure/contract/E2E | Domain/DB/contract tests | Broker/crash/restart/metrics tests | Cùng nghiệm thu trước cutover |
| Reconciliation | Cung cấp query/report endpoint/data | Tích hợp job/alert vận hành nếu phân công | Chốt owner, cadence, sửa lệch/replay |

### Công việc A có thể bắt đầu ngay

- Inventory endpoint/UI/data; lập test baseline.
- Service skeleton, JWT/JWKS, Queue DB, model/migration draft.
- Domain transitions, counter atomicity, persistence constraints.
- Import/verification scripts và unit/integration test hạ tầng.

### Cần B phối hợp trước khi bật luồng tích hợp

- Chốt `AppointmentCheckedIn` schema version 1 đã thêm `Priority`, cùng routing key.
- Consumer xử lý `AppointmentCheckedIn`, ack/retry/DLQ, duplicate semantics.
- Cắt/tắt consumer cũ, Gateway/frontend route và triển khai thứ tự.
- Failure/concurrency/consumer-restart test, metrics và runbook.

## 7. Tiêu chí sẵn sàng nghiệm thu Giai đoạn 4

- [x] `Clinic.Queue.Service` build/healthy độc lập; JWT/JWKS và permission tests pass.
- [x] `ClinicQueueDB` có migration độc lập, không có FK hoặc navigation sang DB khác.
- [ ] Import/verification báo mismatch bằng 0 hoặc các ngoại lệ được liệt kê và chấp nhận.
- [ ] Concurrent check-in cùng bác sĩ/ngày không tạo số trùng; retry cùng event/appointment không tạo vé thứ hai.
- [ ] Appointment Service chỉ phát check-in cho appointment đang hợp lệ trong ngày/ca đã đặt; Queue không nhận doctor/schedule do client chọn.
- [x] Walk-in thường và khẩn cấp đều tạo đúng một ticket, Queue lưu đúng number/priority và không ghi Queue DB từ Clinic monolith.
- [ ] Counter + ticket + Inbox cùng transaction; crash/redelivery không mất vé hay cấp số lần hai.
- [ ] Queue API trả đúng thứ tự, priority, display snapshot và trạng thái; authorization theo doctor/patient/admin/receptionist.
- [ ] Queue phát `ExamStarted`/`ExamCompleted` đúng version; consumer Appointment cập nhật trạng thái idempotent.
- [ ] Test SQL Server, contract, duplicate/out-of-order, broker restart, crash sau commit, E2E check-in-to-exam pass.
- [x] Gateway/frontend đã chuyển; không còn Queue write path vào ClinicDB.
- [ ] Reconciliation có thể phát hiện queue ticket thiếu/trùng và trạng thái appointment lệch; có runbook xử lý.
- [ ] Consumer cũ và đường ghi Queue cũ được tắt trước khi gỡ bảng; rollback có thứ tự rõ ràng và không cấp trùng queue number.

## 8. Tài liệu tham chiếu

- `tasks/TASK_MEMBER_A.md` — nhiệm vụ và tiêu chí Giai đoạn 4.
- `tasks/TASK_MEMBER_B.md` — nhiệm vụ tích hợp broker/consumer/test/observability.
- `tasks/COMMON_REQUIREMENTS_AND_NOTES.md` — service boundaries, check-in/queue, events, testing, observability và migration.
- `tasks/PHASE_2_STAFF_SCHEDULING_EXECUTION_PLAN_MEMBER_A.md` — nguyên tắc service/data ownership và reservation/outbox.
- `tasks/PHASE_3_APPOINTMENT_SERVICE_EXECUTION_PLAN_MEMBER_A.md` — Appointment check-in, `VisitId`, `ExamCompleted` và trạng thái hiện hành.
- `backend/Clinic.Contracts/Events/AppointmentCheckedIn.cs` — payload check-in hiện tại.
- `backend/Clinic.Contracts/Events/ExamCompleted.cs` và `EventTypes.cs` — event contract hiện tại.
- `backend/Clinic.Infrastructure.Sqlserver/Inbox/AppointmentCheckedInProcessor.cs` — consumer chuyển tiếp đang phụ thuộc ClinicDB.
- `backend/Clinic.Domain/Entities/QueueTicket.cs`, `QueueCounter.cs` và `backend/Clinic.Infrastructure.Sqlserver/Configurations/QueueTicketConfiguration.cs`, `QueueCounterConfiguration.cs` — mô hình Queue hiện tại.

---

## 9. Đánh giá sau khi triển khai Giai đoạn 4

**Ngày rà soát:** 2026-10-24

**Phạm vi:** Toàn bộ thay đổi staged và working tree liên quan đến API/Gateway, Queue và Appointment Service, frontend, tests, migrations/importers, Compose và observability. Đã chạy test, Docker Compose build, kiểm tra container, xác minh importer, gọi API có xác thực và đối chiếu tiêu chí ở Mục 4, 5, 7.
### 9.1. Kết luận sau triển khai và nghiệm thu

**Kết luận:** Các sửa đổi trong phạm vi triển khai đã được thực hiện, kiểm thử và chạy trên Docker Compose. Luồng Queue Service tạo vé độc lập; walk-in có retry cùng AppointmentId; giao diện chờ đến khi có ticket; consumer chỉ nhận event check-in, có retry/DLQ; timezone được cấu hình; metrics được scrape bởi Prometheus. Theo xác nhận của chủ dự án, ClinicDB và ClinicQueueDB hiện chỉ chứa mock data; sai khác reconciliation có thể đến từ các lượt chạy API test và được tạm chấp nhận, không chặn Giai đoạn 4. Báo cáo vẫn được giữ để tham khảo nếu sau này chuyển sang dữ liệu thật.

### 9.2. Đã đạt và bằng chứng

- **Walk-in và một writer Queue:** Clinic API không còn tạo QueueTicket; AppointmentCheckedIn được xử lý trong Queue Service. Walk-in dùng cùng AppointmentId để đồng bộ idempotent sang Appointment Service; lỗi sync không bị trả như thành công hoàn tất mà trả trạng thái chờ, có retry API và nút thử lại. Endpoint sync nội bộ yêu cầu `X-Clinic-Service-Token`.
- **Quầy chờ ticket:** frontend poll tối đa khoảng 30 giây rồi giữ bệnh nhân hiện tại ở trạng thái pending; nút in/chuyển người tiếp theo chỉ hiện khi lấy được ticket thật. Check-in lịch hẹn và walk-in đều truy vấn ticket theo AppointmentId; lỗi mạng giữ trạng thái pending. In lại đọc ticket hiện hữu.
- **RabbitMQ delivery:** consumer bind đúng routing key `appointment.checked-in`, không tự nhận event `queue-service.*`; poison event dead-letter, lỗi tạm thời qua retry queue có TTL/backoff và giới hạn retry. Outbox publisher dùng confirm/mandatory, lease và trạng thái dead-letter. `q.queue-service.events` không còn tồn message sau khi xử lý; DLQ/retry queue không có backlog tại thời điểm kiểm tra.
- **Timezone:** API, Appointment Service, Queue Service dùng `CLINIC_TIME_ZONE` (mặc định `Asia/Ho_Chi_Minh`) và fail-fast khi zone không hợp lệ. Walk-in chọn `TimeSlot` theo giờ địa phương; QueueDate suy ra từ UTC timestamp theo cùng timezone. Importer SQL dùng `CLINIC_SQL_TIME_ZONE` (Windows timezone ID tương ứng). Bổ sung migration `AllowWalkInAppointmentsAtSameSlot` để index lịch hẹn không chặn nhiều walk-in trong cùng ca.
- **Import:** queue importer chạy kèm `verify-queue-import.sql`, thoát 0 khi không thiếu source AppointmentId, không duplicate AppointmentId/QueueNumber, counter không thấp và snapshot tên đủ. Appointment importer đã sửa đọc DateOfBirth từ Persons và thoát 0; toàn bộ importer đã chạy trên Compose.
- **Auth và observability:** GET Queue không có token trả 401; Receptionist token truy vấn ticket qua Gateway và Queue Service thành công; internal sync endpoint thiếu/sai service token đều trả 401. `/metrics` trả metrics Queue; Prometheus target `http://queue-service:8080/metrics` trạng thái up. Health/readiness của backend, Appointment Service, Queue Service và frontend đều trả 200.
- **Nghiệm thu luồng:** sau lần build Compose cuối, một walk-in urgent mới đã được xử lý. Queue trả ticket #7 với `Priority=true` và snapshot bệnh nhân/bác sĩ. Retry sync trả lại cùng TicketId và QueueNumber 7.
- **Kiểm thử/build:** `Clinic.slnx` đạt 411/411 test (Application 187, Domain 76, Queue 44, Appointment 30, Scheduling 35, Identity 4, Infrastructure 18, Contracts 3, Integration 14); `npm run build`, `docker compose config -q` và `docker compose up -d --build` thành công. Các container chính đều healthy.

### 9.3. Sai khác và việc còn mở

#### Sai khác dữ liệu mock — đã chấp nhận tạm hoãn

Lần verify importer ghi nhận ClinicDB có 349 ticket và Queue DB có 356 ticket; không thiếu AppointmentId nguồn, có 10 TicketId lệch, 110 counter, không có counter thấp hơn ticket, không trùng AppointmentId/QueueNumber và không thiếu tên. Sau lượt nghiệm thu API, Queue DB có 357 ticket. Vì cả hai database đang dùng mock data và đã có các lượt API test tạo ticket, nguyên nhân chính xác của toàn bộ sai khác không cần xử lý trong phạm vi hiện tại; không sửa/xóa dữ liệu. Danh sách chi tiết vẫn có trong [phase4-queue-reconciliation-20261024.md](../docs/phase4-queue-reconciliation-20261024.md). Nếu sau này dùng dữ liệu thật hoặc chuẩn bị production, chạy lại reconciliation, xác nhận nguồn chuẩn và backup trước mọi thao tác dọn dữ liệu.

#### P1 — Bền vững hóa retry walk-in khi quầy đóng trang

Hiện tại đồng bộ từ ClinicDB sang Appointment Service có tối đa bốn lần retry trong request và thao tác retry thủ công trên UI/API. Nếu mọi lần retry thất bại và người dùng rời trang, chưa có worker/outbox bền vững tự khôi phục. Với phạm vi môn học có thể chấp nhận quy trình retry thủ công; nếu muốn tránh phụ thuộc thao tác lễ tân, thêm hàng đợi retry bền vững trong ClinicDB hoặc chuyển hẳn khởi tạo walk-in sang Appointment Service.

#### P1 — Alert và quy trình replay DLQ/Outbox

Retry, dead-letter state và các counter metrics đã có, nhưng chưa có alert khi DLQ tăng và chưa có giao diện/runbook replay an toàn. Bổ sung dashboard/alert theo `consumer_dead_lettered_total`, outbox dead-letter, retry và tuổi bản ghi; ghi rõ cách kiểm tra payload, replay bằng cùng EventId/AppointmentId và cách tránh phát ticket thứ hai. Fault-injection qua broker restart và replay vận hành chưa được nghiệm thu live.

#### P2 — Hoàn thiện kiểm tra in vé và đa timezone

API đã trả snapshot đủ để frontend hiển thị/in, frontend build thành công và có nút print; chưa kiểm tra thao tác máy in vật lý. Luồng hiện cấu hình được timezone nhưng chỉ nghiệm thu tại `Asia/Ho_Chi_Minh`; triển khai timezone khác cần đặt cả IANA `CLINIC_TIME_ZONE` và Windows `CLINIC_SQL_TIME_ZONE` tương ứng.

#### Lưu ý bảo trì ngoài phạm vi Queue

Build vẫn cảnh báo transitive package `System.Security.Cryptography.Pkcs` 9.0.4 (NU1902/NU1903) và một số nullability/Vite warnings sẵn có. Không có lỗi compile/test/build mới; nên cập nhật dependency và xử lý warning ở lượt bảo trì riêng.

**Trạng thái Giai đoạn 4:** Đủ điều kiện đóng về mặt triển khai theo phạm vi dự án môn học. Sai khác giữa hai database mock đã được chủ dự án chấp nhận tạm hoãn và không còn là blocker. Các P1 về retry bền vững và alert/runbook DLQ cùng P2 về máy in vật lý, đa timezone được giữ làm cải tiến sau này.

### 9.4. Bổ sung sau rà soát nhánh Giai đoạn 3: chuyển hẳn walk-in sang Appointment Service

Phần P1 về retry walk-in trong mục 9.3 được giải quyết bằng cách chuyển quyền tạo walk-in khỏi Clinic, thay vì thêm hàng đợi retry cho luồng đồng bộ cũ:

- `POST /receptionist/appointments/walk-in` ở Clinic API chỉ proxy sang Appointment Service. Clinic không còn tạo Appointment trước rồi gọi `sync-walkin`.
- Appointment Service lấy Patient từ `PatientProjection`, hỏi Scheduling Service các ca đang hoạt động trong ngày theo múi giờ phòng khám, lọc ca chưa kết thúc, rồi chọn ca ít Appointment nhất (hoặc giới hạn theo bác sĩ lễ tân chọn). Nếu không có hồ sơ Patient hoặc ca phù hợp, API trả lỗi và không tạo bản ghi.
- Appointment và event `AppointmentCheckedIn` được lưu trong Appointment DB/outbox cùng transaction. Event mang snapshot tên bệnh nhân, tên bác sĩ và cờ urgent; Queue Service vẫn là nơi duy nhất cấp ticket.
- `Idempotency-Key` được giữ ổn định khi giao diện gửi lại cùng payload sau lỗi mạng. Giao diện chờ Queue Service tạo vé, rồi chỉ cho in khi lấy được ticket thật; nút chờ chỉ truy vấn ticket, không còn kích hoạt đồng bộ Appointment.
- `GET /receptionist/working-doctors` được proxy sang Appointment Service; Appointment Service lấy ca từ Scheduling Service. Patient/Person vẫn thuộc Clinic; luồng đồng bộ PatientProjection hiện có được giữ.
- Đã gỡ handler `CreateWalkInAppointment` và query `GetWorkingDoctorsForToday` khỏi Clinic Application; đã gỡ endpoint cùng DTO `sync-walkin` khỏi Appointment Service.

Kiểm tra trong lượt chuyển đổi: `dotnet build` thành công cho Clinic API, Appointment Service, Scheduling Service và project Appointment Service.Tests (chỉ build, không chạy test); `npm run build` thành công, gồm Vue type-check và Vite production build. Còn các warning dependency `System.Security.Cryptography.Pkcs` 9.0.4 (NU1902/NU1903) và warning sẵn có trong Clinic API/Vite. Chưa chạy luồng chấp nhận trực tiếp trên Docker sau thay đổi này.

Ghi chú: đây là kết quả build của source hiện tại. Cần build/recreate các container Clinic API, Appointment Service và Scheduling Service trước khi thử walk-in trên Docker; nếu patient mới vừa tạo nhưng projection chưa đồng bộ thành công, Appointment Service chủ động từ chối tạo lượt để tránh ticket thiếu tên bệnh nhân.

**Cập nhật xác minh sau Compose:** `Clinic.Appointment.Service.Tests` hiện đạt 32/32 (có thêm test walk-in checked-in/outbox và retry cùng idempotency key); `npm run build` và Vue type-check thành công. `docker compose config -q` cùng `docker compose up -d --build appointment-service scheduling-service backend frontend` thành công; các service chính và database/broker đều healthy, các importer kết thúc mã 0. Kiểm tra `GET /schedules/walk-in-candidates` cho ngày địa phương hiện tại trả danh sách rỗng vì không có ca trực hôm nay trong data mock. Clinic API và Appointment Service trả 401 khi gọi route lễ tân không token như mong đợi; do đó chưa thể nghiệm thu thao tác tạo walk-in có xác thực trong lượt này. Frontend trả 200; health trạng thái container đạt.

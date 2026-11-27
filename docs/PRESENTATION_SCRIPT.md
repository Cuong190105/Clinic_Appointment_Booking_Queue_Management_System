# Kịch bản thuyết trình dự án — 15 phút

> Thời lượng mục tiêu: **15 phút**, 17 slide (bản 27/11/2026 bổ sung slide 11 SLA/SLO/QoS theo từng dịch vụ và slide 12 kiểm chứng QoS trên hệ thống chạy; thời lượng các slide khác được rút gọn tương ứng). Kịch bản cập nhật theo các lượt benchmark microservices ngày 17/11 và lượt đối chứng monolith ngày 22/11/2026. Đây là số liệu thực nghiệm trên Docker Compose phục vụ đồ án, làm rõ ở khía cạnh nào mỗi kiến trúc tốt hơn hệ thống kia.

## Phân bổ thời gian

| Slide | Nội dung | Thời lượng |
|---|---|---:|
| 1 | Đề tài và mục tiêu | 0:40 |
| 2 | Bài toán và người dùng | 0:45 |
| 3 | Kiến trúc monolith | 0:40 |
| 4 | Cơ sở dữ liệu cũ | 0:50 |
| 5 | Lý do phân rã | 0:40 |
| 6 | Service boundaries | 1:00 |
| 7 | Kiến trúc microservices | 1:00 |
| 8 | Booking orchestration và SAGA | 1:15 |
| 9 | Check-in, Queue và choreography | 1:00 |
| 10 | OpenAPI, kiểm thử và vận hành | 0:45 |
| 11 | SLA/SLO/QoS cho từng dịch vụ | 1:00 |
| 12 | Kiểm chứng SLA/QoS trên hệ thống chạy | 1:00 |
| 13 | Cách đánh giá hiệu năng | 0:45 |
| 14 | Quá trình cải tiến microservices | 0:50 |
| 15 | Kết quả tải full-duration trên Microservices | 1:00 |
| 16 | So sánh với monolith | 1:00 |
| 17 | Kết luận và phần còn lại | 0:50 |
| **Tổng** |  | **15:00** |

## Slide 1 — Hệ thống đặt lịch khám và quản lý phòng khám (40 giây)

**Trên slide:** Tên đề tài; monolith → microservices; API, phối hợp dịch vụ, QoS và kiểm thử tải.

**Lời dẫn:**
“Xin chào thầy cô và các bạn. Đề tài của nhóm là hệ thống đặt lịch khám, quản lý hàng đợi và hồ sơ y tế. Nhóm bắt đầu từ một ứng dụng monolith có thể chạy được, sau đó phân rã theo nghiệp vụ thành các microservice. Mục tiêu không chỉ là chia backend thành nhiều tiến trình: sản phẩm cần có API được mô tả bằng OpenAPI, có luồng phối hợp dịch vụ theo orchestration hoặc choreography, có chỉ tiêu SLA/SLO/QoS và có kiểm thử tải đối chiếu hai kiến trúc. Trong phần trình bày, em sẽ đi từ hệ thống cũ, giải thích kiến trúc mới và các luồng quan trọng, rồi kết thúc bằng kết quả so sánh thực nghiệm giữa microservices và monolith.”

## Slide 2 — Bài toán và các vai trò sử dụng (45 giây)

**Trên slide:** Patient; Receptionist; Doctor; Admin; các luồng tìm ca, đặt lịch, check-in, khám, hồ sơ và thông báo.

**Lời dẫn:**
“Ứng dụng mô phỏng hoạt động của một phòng khám. Bệnh nhân tìm bác sĩ hoặc chuyên khoa, xem ca còn chỗ, đặt và theo dõi lịch hẹn. Lễ tân tiếp nhận bệnh nhân, có thể tạo lượt walk-in, check-in và gọi số. Bác sĩ xem queue của mình, điều khiển lượt khám và cập nhật hồ sơ y tế gắn với appointment. Quản trị viên quản lý tài khoản, nhân sự và lịch làm việc. Sau các hành động, hệ thống có thể tạo thông báo và lưu lịch sử để người dùng xem lại.
Điểm cần lưu ý là đây không phải các màn hình độc lập. Ví dụ một lượt đặt khám liên quan đến slot của bác sĩ, appointment của bệnh nhân, queue khi check-in và notification sau khi trạng thái thay đổi. Vì vậy kiến trúc phải vừa giữ được ranh giới nghiệp vụ, vừa xử lý được các bước liên quan.”

## Slide 3 — Hệ thống monolith cũ (40 giây)

**Trên slide:** Sơ đồ từ báo cáo dự án, mục 3.1: Frontend → Clinic.API → Application/Domain/Infrastructure → ClinicDB.

**Lời dẫn:**
“Phiên bản cũ triển khai backend thành một ứng dụng Clinic.API. Bên trong có các lớp API, Application, Domain và Infrastructure; các tính năng gọi nhau trong cùng tiến trình và dùng chung một ApplicationDbContext. Dữ liệu được lưu ở một SQL Server database.
Cách tổ chức này có ưu điểm rõ: gọi nội bộ nhanh, transaction đa bảng đơn giản hơn và chỉ cần vận hành một backend. Với một sản phẩm nhỏ hoặc giai đoạn MVP, đây là lựa chọn hợp lý. Nhưng khi muốn tách ownership và phát hành từng miền, các module dùng chung database khiến ranh giới chỉ nằm trên cấu trúc code. Một thay đổi hay migration có thể ảnh hưởng đến nhiều nghiệp vụ cùng lúc. Đây là xuất phát điểm của quá trình chuyển đổi.”

## Slide 4 — Cơ sở dữ liệu monolith (50 giây)

**Trên slide:** ERD khái quát mục 3.2 của báo cáo; tô màu nhóm Person/User/Employee, Scheduling, Appointment/Queue, Patient/Medical Record.

**Lời dẫn:**
“Database cũ gom nhiều nhóm bảng vào một schema. Nhóm tài khoản gồm User, Role, Permission và các bảng nối. Nhóm nhân sự gồm Employee, Doctor, Specialty và lịch làm việc. Patient nối với Appointment; Appointment lại là mốc liên kết tới queue ticket và medical report. Ngoài ra còn có snapshot, amendment, audit, notification và refresh token.
Mô hình dùng chung giúp truy vấn qua nhiều miền và áp dụng ràng buộc khóa ngoại thuận tiện. Ví dụ, có thể đi từ appointment đến patient, ca làm việc, queue và hồ sơ khám trong cùng database context. Đổi lại, quyền sở hữu dữ liệu chưa độc lập: service hay module nào cũng có nguy cơ đọc trực tiếp bảng của miền khác.
Khi chuyển đổi, nhóm không bê nguyên database này cho tất cả service. Các miền được tách dần, và một số dữ liệu cần cho luồng khác được truyền bằng event hoặc snapshot. Sơ đồ trên slide là ERD nghiệp vụ khái quát, không phải sơ đồ đầy đủ mọi cột và index.”

## Slide 5 — Vì sao phân rã theo nghiệp vụ (40 giây)

**Trên slide:** Ownership; triển khai độc lập; mở rộng theo tải; lỗi cục bộ; đi kèm network, eventual consistency và vận hành phức tạp hơn.

**Lời dẫn:**
“Lý do chuyển sang microservices là làm rõ service nào sở hữu quy tắc và dữ liệu, cũng như tạo khả năng triển khai và mở rộng theo từng miền. Nhưng cách này không tự động làm hệ thống tốt hơn ở mọi khía cạnh. Nó thêm giao tiếp qua mạng, lỗi một phần, trạng thái cập nhật trễ và chi phí quan sát nhiều tiến trình. Vì vậy nhóm đánh giá kết quả bằng cả kiến trúc lẫn thực nghiệm chịu tải để chỉ rõ ở khía cạnh nào monolith lợi thế hơn và ở khía cạnh nào microservices vượt trội hơn.”

## Slide 6 — Phân rã service và quyền sở hữu (1 phút)

**Trên slide:** Bảng 6 service cùng Gateway.

**Lời dẫn:**
“Hệ thống mới có Clinic API làm Gateway và sáu service nghiệp vụ. Identity sở hữu tài khoản, credential, token và quyền. Staff & Scheduling sở hữu nhân sự, bác sĩ, chuyên khoa, ca làm và reservation slot. Appointment sở hữu vòng đời đặt, đổi, hủy và check-in. Queue sở hữu ticket, counter, số thứ tự và trạng thái lượt. Medical Records sở hữu Patient, encounter context, medical report, amendment và audit. Notification sở hữu inbox và trạng thái gửi.
Frontend chỉ gọi Gateway; Gateway xác thực và định tuyến đến service sở hữu endpoint. Mỗi service có database logic riêng. Trong cấu hình môn học, các database này vẫn chạy trên cùng một SQL Server container, nên “database per service” ở đây có nghĩa là tách schema/ownership và connection, không phải mỗi service có một máy chủ SQL vật lý riêng. Cách nói chính xác này giúp tránh phóng đại mức độ cô lập hạ tầng.”

## Slide 7 — Kiến trúc tổng thể mới (1 phút)

**Trên slide:** Sơ đồ tổng quan mục 4.1 của báo cáo dự án: Frontend → Gateway → services; Appointment ↔ Scheduling; services ↔ RabbitMQ; Prometheus/Jaeger.

**Lời dẫn:**
“Trong sơ đồ, frontend đi qua Gateway. Những thao tác cần phản hồi đồng bộ, như kiểm tra và giữ slot, đi qua API giữa Appointment và Scheduling. Những thay đổi cần được nhiều miền biết đến được phát thành event qua RabbitMQ. Mỗi service xử lý event trong phạm vi dữ liệu của mình.
Prometheus thu metrics để quan sát tốc độ và lỗi; Jaeger nhận distributed trace để theo dõi request qua nhiều service. Docker Compose khởi chạy Gateway, các service, SQL Server, RabbitMQ và công cụ quan sát. Đây là một hệ thống chạy được trong môi trường đồ án. Nó chưa phải cụm production phân tán nhiều node, cũng chưa có tính sẵn sàng cao ở mức triển khai nhiều vùng. Sơ đồ mô tả quan hệ logic, không ngụ ý mỗi service đang ở một máy riêng.”

## Slide 8 — Booking orchestration và SAGA (1 phút 15 giây)

**Trên slide:** Sequence: Patient → Gateway → Appointment → Scheduling reserve (HTTP) → Appointment DB/Outbox → `AppointmentConfirmationWorker` gọi Scheduling confirm (HTTP) → Outbox phát `AppointmentConfirmed` qua RabbitMQ → Notification.

**Lời dẫn:**
“Luồng đặt lịch là ví dụ về orchestration lai. Bệnh nhân gửi request cùng idempotency key qua Gateway. Appointment Service kiểm tra request và gọi Scheduling qua HTTP để giữ slot. Bước reserve đồng bộ quan trọng vì nếu slot vừa được người khác đặt, API cần trả kết quả contention ngay thay vì tạo hai lịch cùng chỗ.
Sau khi giữ chỗ, Appointment ghi trạng thái `Pending` và outbox trong transaction cục bộ của nó. Tiếp đó, `AppointmentConfirmationWorker` thăm dò định kỳ trên `AppointmentDB` (`NextConfirmationAttemptAt`) và gọi Scheduling qua HTTP để xác nhận reservation — bước xác nhận này không đi qua RabbitMQ. Khi xác nhận thành công, Appointment chuyển sang `Confirmed` và ghi event `AppointmentConfirmed` vào Outbox để publisher phát qua RabbitMQ cho Notification cùng các consumer liên quan; Outbox bảo đảm không mất sự kiện ngay cả khi broker tạm gián đoạn.
Đây không phải một transaction ACID xuyên hai database. Mỗi service commit dữ liệu của mình, còn workflow tiến triển qua retry và trạng thái. Idempotency key chặn request retry của client tạo booking thứ hai. Nếu một bước thất bại, hệ thống phải retry hoặc chạy hành động bù phù hợp. Appointment điều phối phần quan trọng của quy trình, trong khi các service khác phản ứng với event. Cách kết hợp này vừa giữ được quy tắc booking trung tâm, vừa giảm phụ thuộc gọi đồng bộ cho mọi thông báo.”

## Slide 9 — Check-in, Queue và choreography (1 phút)

**Trên slide:** Receptionist walk-in/check-in → AppointmentCheckedIn → Queue ticket → PatientCalled/ExamStarted/ExamCompleted → Medical Records/Notification.

**Lời dẫn:**
“Ở luồng check-in, Appointment vẫn là nguồn sự thật về lịch khám và phát AppointmentCheckedIn. Queue Service consume event này một cách idempotent và tạo ticket theo AppointmentId. Vì cùng event có thể được RabbitMQ giao lại, consumer phải ngăn ticket trùng. Số queue được cấp trong phạm vi bác sĩ và ngày khám.
Lễ tân có thể gọi số, bỏ qua số chưa gọi hoặc đặt ưu tiên theo quyền. Bác sĩ có thể gọi lượt tiếp theo và bỏ qua lượt đã gọi nhưng chưa bắt đầu khám. Ticket bị skip hiện lại để lễ tân retry thủ công. Các event trạng thái được các service liên quan tiếp nhận; Medical Records dùng AppointmentId làm khóa encounter, không tạo VisitId thứ hai.
Đây là choreography: service phát event, consumer cập nhật mô hình do mình sở hữu. Đổi lại sự tách biệt, chúng ta cần xử lý event trùng, event đến sai thứ tự, retry và dead-letter. Queue không gọi ngược Appointment để dựng toàn bộ dữ liệu; dữ liệu snapshot tối thiểu cần cho ticket được đưa vào event.”

## Slide 10 — OpenAPI, kiểm thử và vận hành (45 giây)

**Trên slide:** OpenAPI snapshots; drift tests; unit/integration/failure tests; health checks; metrics/traces; Compose.

**Lời dẫn:**
“Các API được mô tả bằng OpenAPI snapshots cho Gateway và các service. Contract drift test giúp phát hiện khi implementation thay đổi mà tài liệu API không được cập nhật. Ngoài unit và integration tests còn có kiểm thử failure workflow, QoS, idempotency và các invariant như không overbook hoặc tạo queue ticket trùng.
Hệ thống có health/readiness endpoint, metrics và tracing để quan sát vận hành. Báo cáo acceptance trước đó ghi nhận các lượt test backend, contract và QoS đã chạy thành công. Khi thuyết trình, nhóm nên mở kết quả validator và báo cáo test đúng commit đang demo; số lượng test là bằng chứng về phạm vi kiểm tra, không thay thế cho kiểm tra nghiệp vụ trên runtime.”

## Slide 11 — SLA, SLO và QoS cho từng dịch vụ (1 phút)

**Trên slide:** Bảng 7 dòng (Gateway + 6 service): tầng quan trọng, availability, độ trễ/workflow p95, QoS chính, RPO/RTO; dải SLA mô phỏng; quy tắc đo (409/429 không tính lỗi, không cộng percentile).

**Lời dẫn:**
“SLI là điều đo, SLO là mục tiêu nội bộ, còn SLA là cam kết với người dùng; với đồ án, nhóm mô phỏng SLA trên cửa sổ 30 ngày giờ phòng khám. Thay vì một con số chung, mỗi dịch vụ có mục tiêu riêng theo mức quan trọng. Tầng 1 gồm Identity, Scheduling, Appointment và Queue vì chặn trực tiếp việc đặt lịch và tiếp nhận bệnh nhân, nên availability 99,7 đến 99,9%; Medical Records ở tầng 2 với 99,5%; Notification ở tầng 3 với 99%, vì thông báo trễ vài phút không làm hỏng nghiệp vụ. Vì hành trình đặt lịch đi qua nhiều thành phần nối tiếp, availability đầu–cuối xấp xỉ tích các thành phần, nên cam kết hành trình là 99%.
Độ trễ cũng tách theo loại thao tác: đọc p95 ≤ 300 ms, ghi ≤ 500 ms, đăng nhập ≤ 1 giây vì BCrypt cố ý tốn CPU, và các workflow bất đồng bộ như đặt lịch đến Confirmed ≤ 5 giây. Cột QoS cho biết cơ chế bảo vệ từng dịch vụ: rate limit theo tầng người dùng, deadline và circuit breaker, idempotency, retry có giới hạn rồi DLQ. Cột cuối là RPO/RTO: mất tối đa bao nhiêu dữ liệu và khôi phục trong bao lâu.”

## Slide 12 — Kiểm chứng SLA/QoS trên hệ thống đang chạy (1 phút)

**Trên slide:** Sáu kết quả đo ngày 27/11/2026: chính sách QoS 10/10 + 333 test; cô lập lỗi 100%; năng lực đăng nhập 2–3 → ~10 lượt/s; đột biến mở lịch 40 lượt/s chưa đạt (p95 ~10 s trong ~25 s, nghẽn khóa theo ca); diễn tập khôi phục 7/7 trong 2–9 giây; 0 vi phạm bất biến.

**Lời dẫn:**
“Chính sách chỉ có giá trị khi được kiểm chứng trên hệ thống chạy thật. Bài kiểm tra QoS chạy trên stack Docker đạt 10/10: rate limit theo tầng, `Retry-After`, deadline trả 504 và circuit breaker trả 503; toàn bộ 333 test .NET đạt. Khi nhóm chủ động dừng Scheduling giữa lúc có tải, đặt lịch mới thất bại nhanh bằng 502/503/504 nhưng xem lịch hẹn vẫn hoạt động 100%; dừng Notification thì đặt lịch và hàng đợi không bị ảnh hưởng.
Kiểm thử tải còn tìm ra một điểm nghẽn thật: Identity ban đầu chỉ xử lý 2–3 lượt đăng nhập mỗi giây và quá tải thành timeout. Nhóm giảm BCrypt cost về mức OWASP khuyến nghị, thêm bulkhead giới hạn số phép băm đồng thời và trả 503 kèm `Retry-After` khi quá tải. Kết quả khoảng 10 lượt mỗi giây, quá tải gấp đôi vẫn giữ p95 dưới 1 giây và không còn lỗi 504. Kịch bản đột biến mở lịch chưa đạt và nhóm ghi nhận minh bạch: khi 40 lượt đặt mỗi giây dồn vào 4 ca của 2 bác sĩ, mọi lượt giữ chỗ phải xếp hàng trên khóa của cùng một bản ghi ca, nên trong khoảng 25 giây p95 lên khoảng 10 giây và 11,6% request lỗi. Hết đợt, p95 trở về dưới 25 ms và không có lịch nào bị đặt vượt chỗ; hướng sửa là rút ngắn đoạn giữ khóa bằng bộ đếm chỗ cập nhật nguyên tử.
Về dữ liệu, diễn tập khôi phục cả sáu database và bản off-site đều đạt, mỗi database khôi phục trong 2–9 giây so với RTO một giờ; khôi phục tới đúng một thời điểm và khôi phục tại chỗ có tail-log không mất bản ghi nào. Các bất biến như không overbooking, không vé trùng và DLQ rỗng đều giữ nguyên sau các bài test lỗi.”

## Slide 13 — Cách đánh giá hiệu năng (45 giây)

**Trên slide:** Hai kịch bản đo ngày 27/11/2026: Booking High-Load (20 lượt đặt/giây trong 120 giây, chạy cả đăng nhập lạnh và phiên làm nóng) và Clinic-Peak (500 người dùng theo vai trò, 14 phút, think time 3–15 s), kèm `load.js` 10 lượt/giây; các metric: p95 đọc/ghi/đăng nhập, POST→Confirmed, availability và 5xx.

**Lời dẫn:**
“Nhóm đánh giá hiệu năng bằng hai kiểu tải. Thứ nhất là Booking High-Load: tải đến với tốc độ cố định 20 lượt đặt lịch mỗi giây trong 120 giây, dồn áp lực vào giao dịch ghi để đo tranh chấp giữ chỗ và thời gian hoàn tất SAGA. Thứ hai là Clinic-Peak: 500 người dùng đóng vai bệnh nhân, lễ tân và bác sĩ, tăng dần trong 3 phút rồi giữ 10 phút, mỗi người nghỉ 3 đến 15 giây giữa các thao tác như người thật; kèm một bài tải cố định 10 lượt mỗi giây.
Với mỗi kịch bản, nhóm đo p95 của thao tác đọc, ghi, đăng nhập và thời gian từ POST đến Confirmed. Phản hồi 409 do hết slot được tính là xung đột nghiệp vụ hợp lệ, tách khỏi tỷ lệ lỗi máy chủ 5xx và availability.”

## Slide 14 — Quá trình cải tiến microservices (50 giây)

**Trên slide:** Đường thời gian: bản thô 1,9–2,35 s → tối ưu và phân bổ tài nguyên → run 20 VU 411 ms → benchmark đồng tài nguyên ghi nhận micro 594,7 ms → reserve query optimization (median 449 ms) → high-load 30 VU (253,6 ms) và mixed profiles.

**Lời dẫn:**
“Quá trình tối ưu microservices có cả kết quả tốt và kết quả không đạt được ghi nhận minh bạch. Các lượt đầu tiên có p95 đặt lịch khoảng 1,9 đến 2,35 giây, vượt xa mục tiêu. Sau khi tinh chỉnh luồng gọi dịch vụ và phân bổ tài nguyên container, một lượt đo ở 20 VU giảm xuống khoảng 411 ms. Tuy nhiên, trong lượt đối chiếu đồng tài nguyên 12,5 CPU ở 20 VU, monolith đạt 293,4 ms còn microservices là 594,7 ms, tức vẫn vượt ngưỡng 500 ms.
Khi phân tích sâu, nhóm phát hiện truy vấn kiểm tra sức chứa của Scheduling tải toàn bộ danh sách slot về bộ nhớ ứng dụng, nên đã tối ưu thành truy vấn gom nhóm aggregate trực tiếp trong SQL Server. Nhờ đó, bộ đo lặp 3 lượt ở 20 VU giảm median p95 xuống 449 ms. Tiếp theo, nhóm mở rộng sang kịch bản ép tải 30 VU với 2.400 slot và các cấu hình tải hỗn hợp 5 phút để đối chiếu toàn diện với monolith.”

## Slide 15 — Kết quả tải full-duration trên Microservices (1 phút)

**Trên slide:** Ba KPI (Clinic-Peak p95 đọc/ghi/toàn trình 46,4/147,6/480 ms; 100% availability, 0% 5xx; 95 × 503 khi đăng nhập lạnh) và bảng 3 dòng Clinic-Peak, Load, Booking High-Load (đăng nhập lạnh / phiên làm nóng).

**Lời dẫn:**
“Đây là các lượt chạy đủ thời lượng ngày 27/11 trên microservices. Ở Clinic-Peak 500 người dùng trong 14 phút, hệ thống xử lý hơn 65 nghìn request, tạo thành công 7.347 trên 7.352 lịch, p95 đọc 46 ms và ghi 148 ms, cách xa ngưỡng 300 và 500 ms, với 0% lỗi 5xx. Bài tải cố định 10 lượt mỗi giây cũng đạt, 662 trên 662 lịch.
Booking High-Load cho một bài học đáng chú ý. Lần đầu, cả 228 người dùng ảo đăng nhập cùng lúc ở giây đầu tiên; bulkhead của Identity chủ động từ chối 95 lượt bằng 503 kèm `Retry-After`, các lượt thử lại sau đó đều thành công, nhưng tỷ lệ 503 vượt ngưỡng 1% nên bài test không đạt. Khi làm nóng phiên trước, giống người dùng thật đăng nhập rải rác, cùng tải đặt lịch đạt 100% availability, p95 ghi 82 ms. Hai lượt cho thấy điểm nghẽn là đăng nhập dồn cùng lúc, không phải luồng đặt lịch. Khoảng 19% lượt đặt nhận 409 vì tranh cùng slot; đó là xung đột nghiệp vụ, không phải lỗi.”

## Slide 16 — Đối chiếu với monolith: Hệ thống nào tốt hơn ở khía cạnh nào? (1 phút)

**Trên slide:** Biểu đồ cột p95 (ms) Monolith và Microservices ở Clinic-Peak, Load và High-Load ngày 27/11; bảng đối chiếu, ô xanh là bên tốt hơn; chú thích điều kiện đo (1 lượt mỗi kịch bản; Clinic-Peak và High-Load dùng cùng pool 500 tài khoản, High-Load cùng làm nóng phiên).

**Lời dẫn:**
“Sau khi sửa lỗi walk-in của monolith, nhóm chạy lại cùng các kịch bản trên hai hệ thống, mỗi lần chỉ bật một hệ thống, cùng ngân sách 12,5 CPU. Kết quả khác với lượt 22/11: trên một máy, monolith nhanh hơn ở gần như mọi chỉ số. Ở Clinic-Peak, p95 đọc là 18 so với 46 ms, ghi 50 so với 148 ms, và đặt lịch toàn trình 68 so với 480 ms. Lý do là mỗi thao tác của microservices phải đi qua Gateway, gọi HTTP giữa các service và chờ outbox xác nhận bất đồng bộ, còn monolith gọi hàm và commit trong cùng một giao dịch.
Ở lượt 22/11, microservices đọc nhanh hơn ở mức 20 VU; nhưng lượt cao điểm của monolith hôm đó hỏng do lỗi walk-in, nên con số nhanh hơn 5,6 lần trước đây không còn được dùng. Hôm nay microservices chỉ nhanh hơn ở đăng nhập tải đều, 107 so với 165 ms. Điểm quan trọng là cả hai đều đạt SLO với biên an toàn lớn. Lưu ý mỗi kịch bản mới có một lượt, nhưng hai bên chạy cùng điều kiện: cùng 500 tài khoản và cùng cách làm nóng phiên.”

## Slide 17 — Kết luận và hướng tiếp theo (50 giây)

**Trên slide:** Tổng kết 3 khối: Kết quả kiến trúc (6 dịch vụ và Gateway, OpenAPI + SAGA/choreography, SLA/SLO/QoS đã kiểm chứng); Kết quả đối chiếu (cả hai đạt SLO ở 500 người dùng; Monolith nhanh hơn trên một máy; Microservices đổi độ trễ lấy cô lập lỗi và kiểm soát quá tải); Hướng tiếp theo (sửa điểm nóng đặt lịch, lặp ≥ 3 lượt cùng điều kiện, đo CPU/RAM đồng nhất).

**Lời dẫn:**
“Tóm lại, đồ án đã chuyển hệ thống sang microservices chạy được, với OpenAPI, SAGA, choreography và SLA/SLO/QoS cho từng dịch vụ, được kiểm chứng bằng fault injection, kiểm thử tải và diễn tập khôi phục dữ liệu.
Kết quả đo cho thấy rõ đánh đổi. Trên một máy, monolith có độ trễ thấp hơn và vận hành đơn giản hơn. Microservices chậm hơn nhưng vẫn đạt SLO dưới tải 500 người dùng, và đổi lại có những khả năng monolith không có: dịch vụ quá tải tự từ chối có kiểm soát thay vì kéo cả hệ thống, một dịch vụ dừng thì các luồng khác vẫn chạy, và mỗi dịch vụ được cấp tài nguyên riêng.
Bước tiếp theo là sửa điểm nóng khóa ca làm việc khi đặt lịch đột biến, chạy lặp ít nhất ba lượt với cùng snapshot dữ liệu và cùng pool tài khoản, và đo CPU, RAM đồng nhất trên hai kiến trúc. Em xin cảm ơn thầy cô và các bạn, rất mong nhận được câu hỏi và góp ý.”

## Gợi ý trình chiếu và demo

- Dùng ERD ở mục 3.2 và sơ đồ kiến trúc/luồng ở mục 4 của [báo cáo dự án](PROJECT_REPORT.md); không đưa toàn bộ sơ đồ dày chữ lên một slide.
- Có thể demo Swagger/OpenAPI, một lượt booking, dashboard metrics và Jaeger trace. Nếu demo nằm trong 15 phút, rút ngắn lời dẫn ở slide 3–5 để giữ tổng thời lượng.
- Dùng evidence đã sanitize; không mở raw k6 JSON có setup_data, token hoặc thông tin tài khoản.
- Khi trình bày p95, nói rõ workload, số VU, thời gian, metric được đo và tỷ lệ 5xx; phân biệt rõ kịch bản ép tải ghi đơn miền với kịch bản tải hỗn hợp đa tác vụ.

## Phụ lục số liệu đối chiếu dùng khi hỏi đáp — 22/11/2026

- **Ép tải đặt lịch đơn miền (High-load booking, 30 VU, 2.400 slot, median 3 lượt)**:
  - **Monolith**: p95 API chung **190,2 ms** (122,3–253,5), p95 tạo lịch thành công **214,6 ms** (148,9–287,4), p95 POST→Confirmed **235 ms** (166–316), thông lượng xác nhận **29,84 lịch/giây**, 5xx **0%**, idempotency **100%**.
  - **Microservices**: p95 API chung **253,6 ms** (172,5–398,3), p95 tạo lịch thành công **387,8 ms** (347,7–513,0), p95 POST→Confirmed **762 ms** (656–797), thông lượng xác nhận **29,72 lịch/giây**, 5xx **0%**, idempotency **100%**.
  - *Kết luận khía cạnh 1*: Monolith nhanh hơn ở giao dịch ghi đặt lịch đơn lẻ và xác nhận tức thì; thông lượng xác nhận tương đương nhau.
- **Tải hỗn hợp đa tác vụ — Mức thường (Mixed Normal, 20 VU, 5 phút giữ tải)**:
  - **Microservices**: 18.108 lượt đọc (50,1 req/s) / 7.290 lượt ghi (20,2 req/s), tổng 25.398 requests; **p95 Đọc 26,8 ms**, **p95 Ghi tổng hợp 91,3 ms**, **p95 HTTP chung 65,5 ms**, p95 POST tạo lịch **107,2 ms**, hoàn tất **2.430 lịch Confirmed**, 5xx **0%**.
  - **Monolith**: 15.879 lượt đọc (44,0 req/s) / 7.218 lượt ghi (20,0 req/s), tổng 23.097 requests; **p95 Đọc 82,2 ms**, **p95 Ghi tổng hợp 111,3 ms**, **p95 HTTP chung 92,4 ms**, p95 POST tạo lịch **50,3 ms**, hoàn tất **2.406 lịch Confirmed**, 5xx **0%**.
  - *Kết luận khía cạnh 2*: Ở tải hỗn hợp thường, Microservices có p95 Đọc nhanh gấp **3,1 lần**, p95 Ghi tổng hợp thấp hơn **18%**, p95 HTTP chung thấp hơn **29%** và tổng thông lượng request cao hơn **+10%**.
- **Tải hỗn hợp đa tác vụ — Mức cao điểm (Mixed Peak, 30 VU, 5 phút giữ tải)**:
  - **Microservices**: 32.139 lượt đọc (88,2 req/s) / 15.312 lượt ghi (42,0 req/s); **p95 Đọc 49,7 ms**, **p95 Ghi tổng hợp 187,8 ms**, **p95 HTTP chung 112,7 ms**, p95 POST tạo lịch **343,8 ms**, hoàn tất **3.432 lịch Confirmed**, 5xx **0%**.
  - **Monolith**: 19.086 lượt đọc (52,6 req/s) / 10.732 lượt ghi (29,6 req/s); **p95 Đọc 276,7 ms**, **p95 Ghi tổng hợp 362,4 ms**, **p95 HTTP chung 307,3 ms**, p95 POST tạo lịch **287,8 ms**.
  - *Lưu ý*: lượt Monolith này hỏng do lỗi walk-in (2,90% 5xx, 0/865 walk-in), nên không dùng để kết luận; xem số liệu chạy lại ngày 27/11 bên dưới.
  - *Kết luận khía cạnh 3 (đã thay thế)*: Khi tải hỗn hợp tăng lên mức cao điểm 30 VU, khả năng phân tán tải của Microservices giúp p95 Đọc nhanh hơn **5,6 lần** (49,7 ms vs 276,7 ms) và p95 Ghi tổng hợp nhanh hơn **1,9 lần** (187,8 ms vs 362,4 ms).
- Evidence sanitize và run details: [báo cáo Phase 9 mục 19, 21, 22](../tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md#22-chạy-các-profile-tương-ứng-trên-monolith--22112026).

## Phụ lục số liệu full-duration dùng khi hỏi đáp — 27/11/2026

- **Điều kiện:** sau khi sửa lỗi walk-in của monolith; chạy từng hệ thống riêng (tắt hệ thống còn lại), cùng ngân sách 12,5 CPU; mỗi kịch bản 1 lượt; trên Microservices tạm nâng hạn mức rate limit đăng nhập/ẩn danh trong lúc đo rồi khôi phục. Clinic-Peak và High-Load dùng cùng pool 500 tài khoản trên cả hai hệ thống.
- **Clinic-Peak (500 người dùng, 3 phút tăng + 10 phút giữ):** Monolith (pool 500) p95 đọc/ghi/đăng nhập 18,15/50,31/180,60 ms, đặt lịch toàn trình 68 ms, 58.427 request, 7.622/7.622 lịch (lượt đầu với 30 tài khoản seed: 12,53/41,76/188,75 ms, 57 ms); Microservices 46,36/147,59/229,19 ms, toàn trình 480 ms, 65.218 request, 7.347/7.352 lịch. Cả hai 100% availability, 0% 5xx, 0% throttled.
- **Load (10 lượt/s, 5 phút ổn định):** Monolith đọc/ghi/đăng nhập 14,61/46,51/164,59 ms, toàn trình 56 ms, 655/655 lịch; Microservices 46,71/66,52/107,00 ms, toàn trình 344 ms, 662/662 lịch. Cả hai 0% 5xx.
- **Booking High-Load (20 lượt/s, 120 s, 228 VU):** Monolith đăng nhập lạnh vẫn đạt: ghi p95 30,45 ms, toàn trình 38 ms, 1.968/2.400 lịch (phần còn lại là 409 trùng/hết slot); bản làm nóng phiên, cùng điều kiện với Microservices, cũng đạt: ghi p95 30,05 ms, toàn trình 39 ms, 1.892/2.401 lịch. Microservices đăng nhập lạnh **không đạt**: 95 × 503 do bulkhead Identity (availability 98,55%, 5xx 1,34%) và 61 × 429 khi dọn dữ liệu bằng một tài khoản admin; bản làm nóng phiên (228 phiên đăng nhập trước ở 4 lượt/s) **đạt**: availability 100%, ghi p95 82,04 ms, toàn trình 367 ms, 1.944/2.401 lịch, 0 dropped.
- **Lưu ý khi trả lời:** p95 đăng nhập 112 ms của bản làm nóng được đo trong pha `setup()` ở 4 lượt/s, không phải dưới tải đặt lịch. Evidence: `tests/load/evidence/*-latest.json`; chi tiết ở [báo cáo dự án §6.4](PROJECT_REPORT.md).

## Phụ lục số liệu kiểm chứng SLA/QoS dùng khi hỏi đáp — 27/11/2026

- **Năng lực đăng nhập (`login-capacity.js`, ramp 2 → 20 lượt/s):** ban đầu (BCrypt cost 11, Identity 0,5 CPU) ~2–3 lượt/s, quá tải thành 504 sau ~9 s. Hiện tại (cost 10, bulkhead 1 phép băm + hàng chờ 4, Identity 1,5 CPU lấy từ frontend và Scheduling): ~10 lượt/s trong SLO; quá tải gấp đôi p95 lượt nhận ≤ 756 ms, lượt bị từ chối p95 14 ms kèm `Retry-After`, 0 lỗi 504.
- **Đột biến mở lịch (`spike.js`, 3 → 40 lượt/s, 2 bác sĩ):** đăng nhập p95 233 ms; ~25 s đầu đợt p95 đọc/ghi ~9,5/9,9 s và 11,6% lỗi do khóa `UPDLOCK` trên bản ghi ca làm việc; pha phục hồi p95 18/23 ms; 0 overbooking. Hướng sửa: bộ đếm chỗ cập nhật nguyên tử + giới hạn đồng thời route đặt lịch.
- **Cô lập lỗi:** dừng Scheduling → đặt lịch lỗi nhanh 502/503/504, xem lịch hẹn 100%; dừng Notification → đặt lịch/hàng đợi 100%.
- **Sao lưu/khôi phục:** full 7 ngày + diff 4–12 giờ + log 5–15 phút, off-site gzip + sha256; diễn tập 7/7 (restore 2–9 s, `DBCC CHECKDB` sạch, cả bản off-site); PITR khớp đúng 2.261 lịch hẹn tại thời điểm *T*; khôi phục tại chỗ có tail-log 2.269 → 2.269 bản ghi.
- **Toàn vẹn:** `check-slo-invariants.ps1` 11/11 sau toàn bộ test. Chi tiết: [báo cáo dự án §6.6](PROJECT_REPORT.md), [BACKUP_RECOVERY.md §7.1](BACKUP_RECOVERY.md).

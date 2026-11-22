# Kịch bản thuyết trình dự án — 15 phút

> Thời lượng mục tiêu: **15 phút**, 16 slide. Kịch bản cập nhật theo các lượt benchmark microservices ngày 17/11 và lượt đối chứng monolith ngày 22/11/2026. Đây là số liệu thực nghiệm trên Docker Compose phục vụ đồ án, làm rõ ở khía cạnh nào mỗi kiến trúc tốt hơn hệ thống kia.

## Phân bổ thời gian

| Slide | Nội dung | Thời lượng |
|---|---|---:|
| 1 | Đề tài và mục tiêu | 0:40 |
| 2 | Bài toán và người dùng | 0:55 |
| 3 | Kiến trúc monolith | 0:50 |
| 4 | Cơ sở dữ liệu cũ | 1:00 |
| 5 | Lý do phân rã | 0:40 |
| 6 | Service boundaries | 1:00 |
| 7 | Kiến trúc microservices | 1:00 |
| 8 | Booking orchestration và SAGA | 1:15 |
| 9 | Check-in, Queue và choreography | 1:00 |
| 10 | OpenAPI, kiểm thử và vận hành | 0:50 |
| 11 | SLA/SLO/QoS | 0:45 |
| 12 | Cách đánh giá hiệu năng | 0:55 |
| 13 | Quá trình cải tiến microservices | 1:05 |
| 14 | Kết quả high-load và mixed-load | 1:10 |
| 15 | So sánh với monolith | 1:00 |
| 16 | Kết luận và phần còn lại | 0:55 |
| **Tổng** |  | **15:00** |

## Slide 1 — Hệ thống đặt lịch khám và quản lý phòng khám (40 giây)

**Trên slide:** Tên đề tài; monolith → microservices; API, phối hợp dịch vụ, QoS và kiểm thử tải.

**Lời dẫn:**
“Xin chào thầy cô và các bạn. Đề tài của nhóm là hệ thống đặt lịch khám, quản lý hàng đợi và hồ sơ y tế. Nhóm bắt đầu từ một ứng dụng monolith có thể chạy được, sau đó phân rã theo nghiệp vụ thành các microservice. Mục tiêu không chỉ là chia backend thành nhiều tiến trình: sản phẩm cần có API được mô tả bằng OpenAPI, có luồng phối hợp dịch vụ theo orchestration hoặc choreography, có chỉ tiêu SLA/SLO/QoS và có kiểm thử tải đối chiếu hai kiến trúc. Trong phần trình bày, em sẽ đi từ hệ thống cũ, giải thích kiến trúc mới và các luồng quan trọng, rồi kết thúc bằng kết quả so sánh thực nghiệm giữa microservices và monolith.”

## Slide 2 — Bài toán và các vai trò sử dụng (55 giây)

**Trên slide:** Patient; Receptionist; Doctor; Admin; các luồng tìm ca, đặt lịch, check-in, khám, hồ sơ và thông báo.

**Lời dẫn:**
“Ứng dụng mô phỏng hoạt động của một phòng khám. Bệnh nhân tìm bác sĩ hoặc chuyên khoa, xem ca còn chỗ, đặt và theo dõi lịch hẹn. Lễ tân tiếp nhận bệnh nhân, có thể tạo lượt walk-in, check-in và gọi số. Bác sĩ xem queue của mình, điều khiển lượt khám và cập nhật hồ sơ y tế gắn với appointment. Quản trị viên quản lý tài khoản, nhân sự và lịch làm việc. Sau các hành động, hệ thống có thể tạo thông báo và lưu lịch sử để người dùng xem lại.
Điểm cần lưu ý là đây không phải các màn hình độc lập. Ví dụ một lượt đặt khám liên quan đến slot của bác sĩ, appointment của bệnh nhân, queue khi check-in và notification sau khi trạng thái thay đổi. Vì vậy kiến trúc phải vừa giữ được ranh giới nghiệp vụ, vừa xử lý được các bước liên quan.”

## Slide 3 — Hệ thống monolith cũ (50 giây)

**Trên slide:** Sơ đồ từ báo cáo dự án, mục 3.1: Frontend → Clinic.API → Application/Domain/Infrastructure → ClinicDB.

**Lời dẫn:**
“Phiên bản cũ triển khai backend thành một ứng dụng Clinic.API. Bên trong có các lớp API, Application, Domain và Infrastructure; các tính năng gọi nhau trong cùng tiến trình và dùng chung một ApplicationDbContext. Dữ liệu được lưu ở một SQL Server database.
Cách tổ chức này có ưu điểm rõ: gọi nội bộ nhanh, transaction đa bảng đơn giản hơn và chỉ cần vận hành một backend. Với một sản phẩm nhỏ hoặc giai đoạn MVP, đây là lựa chọn hợp lý. Nhưng khi muốn tách ownership và phát hành từng miền, các module dùng chung database khiến ranh giới chỉ nằm trên cấu trúc code. Một thay đổi hay migration có thể ảnh hưởng đến nhiều nghiệp vụ cùng lúc. Đây là xuất phát điểm của quá trình chuyển đổi.”

## Slide 4 — Cơ sở dữ liệu monolith (1 phút)

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

**Trên slide:** Sequence: Patient → Gateway → Appointment → Scheduling reserve → Appointment DB/Outbox → RabbitMQ → confirm → AppointmentConfirmed → Notification.

**Lời dẫn:**
“Luồng đặt lịch là ví dụ về orchestration lai. Bệnh nhân gửi request cùng idempotency key qua Gateway. Appointment Service kiểm tra request và gọi Scheduling để giữ slot. Bước reserve đồng bộ quan trọng vì nếu slot vừa được người khác đặt, API cần trả kết quả contention ngay thay vì tạo hai lịch cùng chỗ.
Sau khi giữ chỗ, Appointment ghi trạng thái chờ xử lý và outbox trong transaction cục bộ của nó. Outbox giải quyết tình huống database đã ghi nhưng RabbitMQ tạm thời không nhận được event: publisher có thể gửi lại sau khi broker phục hồi. Worker tiếp tục phối hợp bước xác nhận reservation; khi thành công, appointment được cập nhật và event xác nhận được phát cho Notification cùng các consumer liên quan.
Đây không phải một transaction ACID xuyên hai database. Mỗi service commit dữ liệu của mình, còn workflow tiến triển qua retry và trạng thái. Idempotency key chặn request retry của client tạo booking thứ hai. Nếu một bước thất bại, hệ thống phải retry hoặc chạy hành động bù phù hợp. Appointment điều phối phần quan trọng của quy trình, trong khi các service khác phản ứng với event. Cách kết hợp này vừa giữ được quy tắc booking trung tâm, vừa giảm phụ thuộc gọi đồng bộ cho mọi thông báo.”

## Slide 9 — Check-in, Queue và choreography (1 phút)

**Trên slide:** Receptionist walk-in/check-in → AppointmentCheckedIn → Queue ticket → PatientCalled/ExamStarted/ExamCompleted → Medical Records/Notification.

**Lời dẫn:**
“Ở luồng check-in, Appointment vẫn là nguồn sự thật về lịch khám và phát AppointmentCheckedIn. Queue Service consume event này một cách idempotent và tạo ticket theo AppointmentId. Vì cùng event có thể được RabbitMQ giao lại, consumer phải ngăn ticket trùng. Số queue được cấp trong phạm vi bác sĩ và ngày khám.
Lễ tân có thể gọi số, bỏ qua số chưa gọi hoặc đặt ưu tiên theo quyền. Bác sĩ có thể gọi lượt tiếp theo và bỏ qua lượt đã gọi nhưng chưa bắt đầu khám. Ticket bị skip hiện lại để lễ tân retry thủ công. Các event trạng thái được các service liên quan tiếp nhận; Medical Records dùng AppointmentId làm khóa encounter, không tạo VisitId thứ hai.
Đây là choreography: service phát event, consumer cập nhật mô hình do mình sở hữu. Đổi lại sự tách biệt, chúng ta cần xử lý event trùng, event đến sai thứ tự, retry và dead-letter. Queue không gọi ngược Appointment để dựng toàn bộ dữ liệu; dữ liệu snapshot tối thiểu cần cho ticket được đưa vào event.”

## Slide 10 — OpenAPI, kiểm thử và vận hành (50 giây)

**Trên slide:** OpenAPI snapshots; drift tests; unit/integration/failure tests; health checks; metrics/traces; Compose.

**Lời dẫn:**
“Các API được mô tả bằng OpenAPI snapshots cho Gateway và các service. Contract drift test giúp phát hiện khi implementation thay đổi mà tài liệu API không được cập nhật. Ngoài unit và integration tests còn có kiểm thử failure workflow, QoS, idempotency và các invariant như không overbook hoặc tạo queue ticket trùng.
Hệ thống có health/readiness endpoint, metrics và tracing để quan sát vận hành. Báo cáo acceptance trước đó ghi nhận các lượt test backend, contract và QoS đã chạy thành công. Khi thuyết trình, nhóm nên mở kết quả validator và báo cáo test đúng commit đang demo; số lượng test là bằng chứng về phạm vi kiểm tra, không thay thế cho kiểm tra nghiệp vụ trên runtime.”

## Slide 11 — SLA, SLI, SLO và QoS (45 giây)

**Trên slide:** Availability Gateway ≥99%; API p95 ≤500 ms, p99 ≤1.500 ms; 5xx ≤1%; event lag p95 ≤5 s; không overbooking/duplicate ticket; rate limit 100 req/phút/partition.

**Lời dẫn:**
“Nhóm định nghĩa SLI là điều đo, như độ trễ p95 hoặc tỷ lệ lỗi server; SLO là mục tiêu nội bộ cho chỉ số đó. SLA là cam kết với người sử dụng, còn các con số của đồ án chưa phải cam kết production. Ma trận đặt mục tiêu Gateway availability tối thiểu 99%, API p95 500 ms và p99 1.500 ms, lỗi 5xx không quá 1%, event lag p95 không quá 5 giây, đồng thời không overbook và không tạo ticket trùng.
QoS còn gồm timeout, retry có giới hạn, rate limit và cách phân biệt lỗi nghiệp vụ. HTTP 409 do slot hết chỗ hay 429 do giới hạn truy cập không nên bị tính như 5xx. Các ngưỡng được đo theo từng boundary; không cộng percentile của các hop để tạo ra một con số p95 tổng.”

## Slide 12 — Cách đánh giá hiệu năng (55 giây)

**Trên slide:** Phân biệt high-load booking (ép tải ghi đơn miền) và mixed workload (tải hỗn hợp Đọc + Ghi đồng thời); các metric: request p95, create p95, E2E POST→Confirmed, read/write p95, thông lượng và 5xx.

**Lời dẫn:**
“Nhóm đánh giá hiệu năng trên hai nhóm kịch bản đại diện cho hai chế độ vận hành khác nhau. Thứ nhất là Booking High-Load với 30 VU tranh chấp trên 2.400 slot: kịch bản này dồn toàn bộ áp lực vào một giao dịch ghi đặt lịch duy nhất để đo khả năng xử lý xung đột giữ chỗ và độ trễ hoàn tất SAGA. Thứ hai là Mixed Workload kéo dài 5 phút ở mức tải thường 20 VU và cao điểm 30 VU: kịch bản này chạy đồng thời luồng tra cứu Đọc với luồng giao dịch Ghi đặt và hủy lịch để đánh giá khả năng phân tán tải khi nhiều miền cùng hoạt động.
Với đặt lịch, nhóm bóc tách độ trễ của mọi phản hồi API, độ trễ riêng của lượt tạo thành công HTTP 201 và thời gian toàn trình từ POST đến Confirmed. Với tải hỗn hợp, nhóm đo riêng độ trễ p95 của thao tác Đọc, thao tác Ghi tổng hợp và bước tạo lịch hẹn. Đồng thời, các phản hồi xung đột slot HTTP 409 hợp lệ được tách khỏi tỷ lệ lỗi máy chủ 5xx để phản ánh đúng độ tin cậy kỹ thuật.”

## Slide 13 — Quá trình cải tiến microservices (1 phút 5 giây)

**Trên slide:** Đường thời gian: bản thô 1,9–2,35 s → tối ưu và phân bổ tài nguyên → run 20 VU 411 ms → benchmark đồng tài nguyên ghi nhận micro 594,7 ms → reserve query optimization (median 449 ms) → high-load 30 VU (253,6 ms) và mixed profiles.

**Lời dẫn:**
“Quá trình tối ưu microservices có cả kết quả tốt và kết quả không đạt được ghi nhận minh bạch. Các lượt đầu tiên có p95 đặt lịch khoảng 1,9 đến 2,35 giây, vượt xa mục tiêu. Sau khi tinh chỉnh luồng gọi dịch vụ và phân bổ tài nguyên container, một lượt đo ở 20 VU giảm xuống khoảng 411 ms. Tuy nhiên, trong lượt đối chiếu đồng tài nguyên 12,5 CPU ở 20 VU, monolith đạt 293,4 ms còn microservices là 594,7 ms, tức vẫn vượt ngưỡng 500 ms.
Khi phân tích sâu, nhóm phát hiện truy vấn kiểm tra sức chứa của Scheduling tải toàn bộ danh sách slot về bộ nhớ ứng dụng, nên đã tối ưu thành truy vấn gom nhóm aggregate trực tiếp trong SQL Server. Nhờ đó, bộ đo lặp 3 lượt ở 20 VU giảm median p95 xuống 449 ms. Tiếp theo, nhóm mở rộng sang kịch bản ép tải 30 VU với 2.400 slot và các cấu hình tải hỗn hợp 5 phút để đối chiếu toàn diện với monolith.”

## Slide 14 — Kết quả high-load và mixed-load mới nhất trên Microservices (1 phút 10 giây)

**Trên slide:** Bảng gọn 3 dòng cho Booking High-Load (30 VU), Mixed Normal (20 VU) và Mixed Peak (30 VU) với khối lượng request Đọc/Ghi, số lịch Confirmed, độ trễ p95 và 0% lỗi 5xx.

**Lời dẫn:**
“Trên hệ thống microservices sau tối ưu, cả ba kịch bản đều vượt qua toàn bộ ngưỡng mục tiêu và đạt 0% lỗi máy chủ 5xx. Ở kịch bản ép tải đặt lịch 30 VU trên 2.400 slot, qua 3 lượt chạy hệ thống xử lý 34.398 yêu cầu, tạo và xác nhận thành công 7.133 lịch hẹn với median p95 phản hồi API chung là 253,6 ms, p95 tạo lịch thành công là 387,8 ms và từ POST đến Confirmed là 762 ms.
Ở hai cấu hình tải hỗn hợp kéo dài 5 phút, kịch bản tải thường 20 VU thực hiện hơn 18.100 lượt đọc và 7.290 lượt ghi, xác nhận và dọn dẹp sạch 2.430 lịch hẹn với p95 đọc chỉ 26,8 ms, ghi tổng hợp 91,3 ms và tạo lịch 107,2 ms. Khi nâng lên tải cao điểm 30 VU, hệ thống xử lý hơn 32.100 lượt đọc và 15.300 lượt ghi, xác nhận 3.432 lịch hẹn mà vẫn giữ p95 đọc ở mức 49,7 ms và ghi tổng hợp 187,8 ms. Những con số này là cơ sở để đặt lên bàn cân đối chiếu trực tiếp với monolith ở slide tiếp theo.”

## Slide 15 — Đối chiếu với monolith: Hệ thống nào tốt hơn ở khía cạnh nào? (1 phút)

**Trên slide:** Biểu đồ cột nhóm so sánh trực tiếp độ trễ p95 (ms) của Monolith và Microservices ở hai chế độ: Tải hỗn hợp đa tác vụ (Đọc & Ghi tổng hợp ở 20 VU và 30 VU) so với Ép tải đặt lịch đơn miền (API chung, Tạo lịch 201, POST→Confirmed); bên phải là bảng phân định rõ khía cạnh Monolith tốt hơn, khía cạnh Microservices tốt hơn và khía cạnh tương đương.

**Lời dẫn:**
“Khi đặt kết quả của hai hệ thống cạnh nhau trên cùng kịch bản và ngân sách tài nguyên 12,5 CPU, bức tranh thực nghiệm chỉ rõ mỗi kiến trúc vượt trội ở một khía cạnh khác nhau.
Thứ nhất, **Monolith tốt hơn ở độ trễ của giao dịch ghi đơn lẻ và thời gian xác nhận tức thì**. Khi ép tải thuần túy vào luồng đặt lịch 30 VU, Monolith có p95 tạo lịch thành công thấp hơn (214,6 ms so với 387,8 ms) và thời gian từ POST đến Confirmed ngắn hơn rõ rệt (235 ms so với 762 ms). Nguyên nhân là Monolith gọi hàm trong cùng tiến trình và commit ACID một bước, không tốn chi phí gọi qua mạng từ Gateway sang Appointment và Scheduling, cũng không phải chờ chu trình SAGA bất đồng bộ qua RabbitMQ.
Thứ hai, **Microservices lại vượt trội rõ rệt về độ trễ Đọc và Ghi tổng hợp khi hệ thống chịu tải hỗn hợp đa tác vụ**. Khi các luồng tra cứu Đọc và giao dịch Ghi chạy đồng thời suốt 5 phút, ở mức 20 VU Microservices có p95 Đọc nhanh gấp 3,1 lần (26,8 ms so với 82,2 ms) và p95 Ghi tổng hợp thấp hơn 18% (91,3 ms so với 111,3 ms). Khi lên tải cao điểm 30 VU, ưu thế phân tán tải càng rõ: p95 Đọc của Microservices nhanh gấp 5,6 lần (49,7 ms so với 276,7 ms), p95 Ghi tổng hợp nhanh gấp 1,9 lần (187,8 ms so với 362,4 ms) và tổng lượng request xử lý cao hơn gần 60%. Lý do là Microservices chia tải tra cứu và cập nhật về từng service và CSDL logic riêng, tránh được hiện tượng nghẽn cổ chai trên một tiến trình và một DbContext dùng chung như Monolith.
Cuối cùng, về **thông lượng đặt lịch xác nhận thực tế**, hai hệ thống đạt năng lực ngang nhau, xấp xỉ 29,8 và 29,7 lịch mỗi giây với 0% lỗi 5xx trên các luồng đặt lịch và tra cứu.”

## Slide 16 — Kết luận và hướng tiếp theo (55 giây)

**Trên slide:** Tổng kết 3 khối: Khi nào Monolith tốt hơn (giao dịch ghi đơn luồng, xác nhận ACID tức thì, vận hành gọn); Khi nào Microservices tốt hơn (tải hỗn hợp đa miền, độ trễ Đọc nhanh hơn 3,1–5,6×, cô lập lỗi và mở rộng độc lập); Công việc hoàn thiện tiếp theo (khôi phục snapshot CSDL, lặp ≥3 lượt tải hỗn hợp, đo CPU/RAM đồng nhất).

**Lời dẫn:**
“Tóm lại, đồ án không chỉ hoàn thành việc chuyển đổi sang kiến trúc microservices chạy được với OpenAPI, SAGA, choreography và khung SLI/SLO/QoS, mà qua kiểm thử tải còn định lượng rõ ranh giới đánh đổi giữa hai kiến trúc.
Monolith là lựa chọn tối ưu khi ưu tiên độ trễ phản hồi tức thì của một giao dịch ghi đơn lẻ và sự đơn giản trong vận hành. Ngược lại, Microservices phát huy ưu thế vượt trội khi hệ thống vận hành dưới tải hỗn hợp nhiều miền nghiệp vụ cùng lúc — giúp giảm độ trễ đọc từ 3 đến hơn 5 lần, giảm độ trễ ghi tổng hợp tới gần một nửa ở tải cao điểm, đồng thời cho phép cô lập lỗi qua Outbox và mở rộng tài nguyên độc lập cho từng dịch vụ nóng.
Để bộ đối chứng hoàn chỉnh hơn nữa trong tương lai, nhóm đề xuất khôi phục cùng một snapshot CSDL sạch trước mỗi lượt chạy để loại bỏ ảnh hưởng tích lũy dữ liệu lịch sử, chạy lặp tối thiểu ba lượt cho các kịch bản tải hỗn hợp và thu thập chuỗi CPU/RAM đồng nhất trên hai kiến trúc. Em xin cảm ơn thầy cô và các bạn, rất mong nhận được câu hỏi và góp ý.”

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
  - *Kết luận khía cạnh 3*: Khi tải hỗn hợp tăng lên mức cao điểm 30 VU, khả năng phân tán tải của Microservices giúp p95 Đọc nhanh hơn **5,6 lần** (49,7 ms vs 276,7 ms) và p95 Ghi tổng hợp nhanh hơn **1,9 lần** (187,8 ms vs 362,4 ms).
- Evidence sanitize và run details: [báo cáo Phase 9 mục 19, 21, 22](../tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md#22-chạy-các-profile-tương-ứng-trên-monolith--22112026).

# Đối chiếu Queue ClinicDB và ClinicQueueDB — 2026-10-24

## Phạm vi

Báo cáo đọc từ hai database sau khi Compose build và chạy `verify-queue-import.sql`. Không cập nhật/xóa dữ liệu. Ticket ID, Appointment ID, số queue, status và priority được dùng để xác định bản ghi; không đưa tên, số điện thoại hoặc thông tin sức khỏe vào báo cáo. Chủ dự án xác nhận ClinicDB và ClinicQueueDB hiện chỉ chứa mock data; các lượt API test có thể đã tạo thêm ticket. Vì vậy sai khác dưới đây được tạm chấp nhận, không chặn Giai đoạn 4, và không cần dọn trong phạm vi hiện tại. Nguyên nhân cụ thể của từng sai khác chưa được chứng minh.

## Kết quả import trước các lượt acceptance

- ClinicDB QueueTickets: 349.
- ClinicQueueDB QueueTickets: 352.
- AppointmentId có ở ClinicDB nhưng thiếu ở ClinicQueueDB: 0.
- 10 AppointmentId có ở cả hai DB nhưng TicketId khác nhau.
- 3 ticket chỉ có ở ClinicQueueDB.
- Không có duplicate AppointmentId hoặc duplicate số trong phạm vi bác sĩ/ngày; 110 counter không thấp hơn số đã cấp; không thiếu PatientName/DoctorName.
- Lượt nghiệm thu API ban đầu thêm bốn ticket test (#3–#6) vào ClinicQueueDB, nâng tổng lên 356. Sau lần build Compose cuối, một walk-in urgent tạo ticket #7; ClinicQueueDB hiện có 357 ticket. Các ticket test này tách biệt với những sai khác dữ liệu import lịch sử.

Theo quyết định thiết kế hiện hành, ClinicQueueDB là nguồn chuẩn cho ticket và trạng thái sau cutover. Với mock data hiện tại, giữ nguyên cả hai bộ dữ liệu để demo và truy vết; không cần thực hiện backup/read-only window hoặc cleanup để đóng Giai đoạn 4. Nếu chuyển sang dữ liệu thật, phải chạy lại đối chiếu và xác nhận quy trình backup/rollback trước khi dọn bảng cũ.

## Mười cặp có TicketId khác nhau

`CheckInTime` của từng cặp giống nhau ở hai DB. Status/priority dưới đây ghi theo thứ tự ClinicDB → ClinicQueueDB.

| AppointmentId | Clinic TicketId / # | Queue TicketId / # | Status | Priority |
|---|---|---|---|---|
| CA24A0D9-1F04-4588-9881-090C0C71ED84 | 1A38C622-3CFE-4231-953A-99E77CA0B129 / 14 | 3C4EA8DE-D78E-48C9-B58F-93F21A32E3FE / 15 | Waiting → Skipped | 0 → 1 |
| 814889CB-9929-4054-99CF-2E40F63A9A7B | 0A5146C5-7A42-42E3-8C5B-24F1AB1B5BDE / 7 | 7F37334D-9AB2-402A-82E0-983C811508B7 / 7 | Waiting → Completed | 1 → 1 |
| 8E45C613-05D6-4069-A89C-342AE81FE6D0 | EECC8CFF-5429-4724-B2D6-67EF0313A1CC / 5 | B00EE2C4-B3BC-4023-A5A2-8C1A5D262999 / 5 | Waiting → Completed | 1 → 1 |
| 092B79CE-E320-4F56-A801-73AE491A64B0 | 3C330AD9-D91F-489E-A89A-DB537B60F74E / 11 | A0551220-DC6A-4014-83DE-509343C70B07 / 11 | Waiting → Waiting | 1 → 1 |
| 245C63FF-35B6-4835-9C7C-85C11AB04858 | 7624CDDB-BDCE-4B63-97EA-EAF43294997A / 9 | 5BAEFB08-585B-4FB3-85B9-C4A0BCE5DC57 / 9 | Waiting → Completed | 1 → 1 |
| E9B71CCD-43B0-44EE-8B17-A952B68C1CD9 | 09BEFD52-9C8C-4BB1-8DE5-E8F237019611 / 13 | 54050D9A-95E3-4515-9DBC-9C34BB8AFD8B / 14 | Waiting → Waiting | 1 → 1 |
| D8BF6C47-1DDF-446D-B664-B4D5B0DD2A2A | E8D8B393-A3C1-47D5-B69A-59EBEAADD556 / 12 | DB90E2CA-C3D3-420C-AC3D-ACE5D82377A8 / 12 | Waiting → Skipped | 0 → 1 |
| D6557B14-3A23-4E24-A129-B7F6771C3CA8 | 0E71F81A-EE0D-4D44-9304-EE1E70A33916 / 6 | 215C48FA-FB38-493F-911E-BE0A76731471 / 6 | Waiting → Completed | 1 → 1 |
| 8003674D-F1FE-4A8A-BFDF-CEDC7BD0A8CD | 2B61B878-6970-4250-B4CD-BAB2BDA6E73C / 10 | 15ABD521-1CB7-4219-BD06-C5038CB6E7B5 / 10 | Waiting → Waiting | 1 → 1 |
| 11799724-44DD-4057-A7DE-EF51242A97A8 | 2975B1B7-E46A-481C-873F-9B4FDE516B00 / 8 | 085E6086-B41E-45D5-A5AE-6CB4FF9A280A / 8 | Waiting → Completed | 1 → 1 |

## Ba ticket chỉ có ở ClinicQueueDB trước acceptance

| AppointmentId | Queue TicketId / # | Status | Priority |
|---|---|---|---|
| A0000009-0000-0000-0000-000000000046 | 1D72209F-740A-49E7-BEAA-051633A95BA4 / 13 | Completed | 1 |
| E0EE592C-E005-459E-BBBD-4AB9898ECEBF | 9DB5DDAE-5477-448D-A7E3-0F074B37C12E / 1 | Completed | 1 |
| 05852258-064B-4EAC-9309-DF747104256C | 48868923-C64E-4F2F-811A-642BFE168726 / 2 | Waiting | 1 |

## Hướng xử lý đã chốt cho dữ liệu mock

1. Tạm chấp nhận số liệu reconciliation vì hai DB chỉ chứa mock data và đã có API test; không thay TicketId/QueueNumber và không replay `AppointmentCheckedIn` từ các cặp lệch.
2. Giữ nguyên dữ liệu hiện tại làm dữ liệu demo/truy vết; không cleanup để đóng Giai đoạn 4.
3. Khi chuẩn bị dùng dữ liệu thật, xác nhận lại nguồn chuẩn, chạy reconciliation mới, rồi mới quyết định backup, rollback window và dọn Queue tables legacy.
4. Năm ticket test trong lượt nghiệm thu ngày 2026-10-24 vẫn nằm trong ClinicQueueDB, queue number 3–7. Ticket #7 được tạo sau khi build contract cuối; retry xác nhận TicketId/QueueNumber không đổi.

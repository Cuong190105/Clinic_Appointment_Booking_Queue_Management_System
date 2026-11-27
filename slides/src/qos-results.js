'use strict';

// Kết quả kiểm chứng SLA/QoS trên stack đang chạy (27/11/2026), dùng chung cho bản nộp và bản trình chiếu.
// Nguồn: tests/load/README.md §5, tests/load/evidence/*-summary.json, tests/load/evidence/backup-drill.json,
// docs/BACKUP_RECOVERY.md §7. Chỉ ghi số liệu đã đo; không suy diễn thêm.
const { theme } = require('./helpers');

const qosResults = [
  {
    metric: '10/10 · 333/333',
    title: 'Chính sách QoS được thực thi',
    accent: theme.colors.teal,
    body: 'qos-policy.js trên stack thật: rate limit theo tầng, Retry-After, correlation ID.\nTest .NET: deadline → 504, breaker → 503, không retry POST.',
    live: 'Rate limit, Retry-After, deadline 504,\nbreaker 503 · 333 test .NET đạt',
  },
  {
    metric: '100%',
    title: 'Cô lập lỗi phụ thuộc',
    accent: theme.colors.navy,
    body: 'Dừng Scheduling: đặt lịch lỗi nhanh 502/503/504, xem lịch hẹn vẫn 100%.\nDừng Notification: đặt lịch & hàng đợi vẫn 100% sẵn sàng.',
    live: 'Dừng Scheduling: xem lịch vẫn 100%\nDừng Notification: lõi vẫn 100%',
  },
  {
    metric: '2–3 → ~10/s',
    title: 'Năng lực đăng nhập',
    accent: theme.colors.teal,
    body: 'Cost 10 + bulkhead + 1,5 CPU. Quá tải gấp đôi: p95 lượt nhận ≤ 756 ms,\nlượt bị từ chối 14 ms kèm Retry-After, 0 lỗi timeout 504.',
    live: 'Quá tải: p95 ≤ 756 ms, từ chối 14 ms\nkèm Retry-After · 0 lỗi 504',
  },
  {
    metric: 'p95 ~10 s ✗',
    title: 'Đột biến mở lịch (40 lượt/s)',
    accent: theme.colors.red,
    body: 'Điểm nghẽn thật: 40 lượt/s dồn vào 4 ca của 2 bác sĩ, khóa UPDLOCK theo ca\n→ p95 ~10 s, 11,6% lỗi trong ~25 s. Hết đợt: p95 < 25 ms, 0 overbooking.',
    live: 'Khóa theo ca → p95 ~10 s trong ~25 s\nSau đợt: p95 < 25 ms · 0 overbooking',
  },
  {
    metric: '7/7 · 2–9 s',
    title: 'Diễn tập khôi phục dữ liệu',
    accent: theme.colors.green,
    body: 'Restore cả 6 DB và bản off-site, DBCC CHECKDB sạch (RTO mục tiêu 60 phút).\nKhôi phục tới thời điểm chính xác; tail-log: 0 bản ghi bị mất.',
    live: 'Restore 6 DB + off-site, CHECKDB sạch\nPITR chính xác · tail-log mất 0 bản ghi',
  },
  {
    metric: '0 vi phạm',
    title: 'Bất biến dữ liệu',
    accent: theme.colors.green,
    body: 'Tranh chấp 1 slot: đúng 1 thắng, 19 × 409.\n0 overbooking, 0 vé trùng, DLQ = 0 sau các bài test lỗi.',
    live: '1 slot: 1 thắng, 19 × 409\n0 overbooking · 0 vé trùng · DLQ = 0',
  },
];

module.exports = { qosResults };

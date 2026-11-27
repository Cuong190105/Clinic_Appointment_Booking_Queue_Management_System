'use strict';

const { addCard, addPill, addSlideBase, notesFor, rect, text, theme } = require('../helpers');
const { qosResults } = require('../qos-results');

function buildSlides11To17(pptx) {
  // =========================================================================
  // SLIDE 11 — SLA/SLO/QoS theo từng dịch vụ
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Chỉ tiêu chất lượng dịch vụ',
      title: 'SLA, SLO và QoS được định nghĩa cho từng dịch vụ',
      source: '[Q] §2–§5  ·  [P] §5  ·  [R] §4  ·  [S] Slide 11',
      number: 11,
    });

    const head = (t) => ({ text: t, options: { bold: true, color: theme.colors.white, fill: { color: theme.colors.navy } } });
    const tierCell = (t, color) => ({ text: t, options: { bold: true, color, align: 'center' } });
    const rows = [
      [head('Dịch vụ'), head('Tầng'), head('Sẵn sàng'), head('Độ trễ / workflow (p95)'), head('QoS chính'), head('RPO / RTO')],
      ['API Gateway', tierCell('—', theme.colors.slate), '99,9%', 'Đọc ≤ 300 ms · Ghi ≤ 500 ms', 'Rate limit theo tầng (BN 100, NV 600/phút); deadline 5 s; circuit breaker 503 + Retry-After', '—'],
      ['Identity', tierCell('1', theme.colors.red), '99,9%', 'Đăng nhập ≤ 1 s · Refresh ≤ 300 ms', 'Đăng nhập 10/phút/IP; bulkhead băm mật khẩu, quá tải → 503 nhanh', '24 giờ / 1 giờ'],
      ['Staff & Scheduling', tierCell('1', theme.colors.red), '99,7%', 'Đọc ≤ 300 ms', 'Gọi từ Appointment: deadline 3 s, retry 1 lần (lệnh idempotent)', '1 giờ / 1 giờ'],
      ['Appointment', tierCell('1', theme.colors.red), '99,7%', 'Ghi ≤ 500 ms · Confirmed ≤ 5 s', 'Idempotency-Key; 10 lượt đặt/phút/BN; lỗi Scheduling không lan', '1 giờ / 1 giờ'],
      ['Queue', tierCell('1', theme.colors.red), '99,7%', 'Check-in → số ≤ 3 s · lag ≤ 5 s', 'Consumer idempotent; retry 5 s → 30 s → 2 phút rồi DLQ', '1 giờ / 1 giờ'],
      ['Medical Records', tierCell('2', theme.colors.amber), '99,5%', 'Ghi ≤ 500 ms · encounter ≤ 5 s', 'Audit 100%; sự kiện chờ trong queue khi gián đoạn', '15 phút / 1 giờ'],
      ['Notification', tierCell('3', theme.colors.teal), '99,0%', 'Sự kiện → inbox ≤ 10 s', 'Gián đoạn không ảnh hưởng Tier 1; 3 retry rồi DLQ', '24 giờ / 4 giờ'],
    ];
    slide.addTable(rows, {
      x: 0.58, y: 1.52, w: 12.15,
      colW: [1.75, 0.62, 0.95, 2.6, 4.68, 1.55],
      fontFace: theme.font, fontSize: 11, color: theme.colors.ink,
      border: { type: 'solid', pt: 0.6, color: theme.colors.line },
      fill: { color: theme.colors.white },
      rowH: 0.5, valign: 'middle', margin: 0.06,
    });

    rect(slide, 0.58, 5.66, 12.15, 0.5, theme.colors.sky, 'B9D4DC');
    text(slide, 'SLA (mô phỏng):', 0.78, 5.77, 1.75, 0.28, { fontSize: 12.4, bold: true, color: theme.colors.navy });
    text(slide, 'Hành trình đặt lịch / check-in / hồ sơ ≥ 99,0%, đăng nhập ≥ 99,5% · 30 ngày giờ phòng khám · error budget ≈ 200 phút · cảnh báo burn-rate 14,4× / 6×', 2.5, 5.77, 10.1, 0.28, { fontSize: 11.6, color: theme.colors.ink });

    rect(slide, 0.58, 6.26, 12.15, 0.62, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Quy tắc đo:', 0.8, 6.42, 1.3, 0.28, { fontSize: 12.6, bold: true, color: theme.colors.amber });
    text(slide, '5xx/timeout là sự cố; 409 (hết chỗ), 429 (giới hạn) không tính vào lỗi · percentile không cộng qua các chặng · mục tiêu đồ án trên Docker Compose.', 2.1, 6.42, 10.45, 0.28, { fontSize: 11.8, color: theme.colors.ink });

    slide.addNotes(notesFor(11));
  }

  // =========================================================================
  // SLIDE 12 — Kiểm chứng SLA/QoS trên hệ thống chạy
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Kiểm chứng chất lượng dịch vụ',
      title: 'Kiểm chứng SLA/QoS trên hệ thống đang chạy (27/11/2026)',
      source: '[T] §5  ·  [Q] §6  ·  [R] §7  ·  [S] Slide 12',
      number: 12,
    });

    const pos = [[0.58, 1.56], [4.72, 1.56], [8.86, 1.56], [0.58, 3.86], [4.72, 3.86], [8.86, 3.86]];
    const results = qosResults.map((r, i) => ({ ...r, x: pos[i][0], y: pos[i][1] }));

    results.forEach((t) => {
      rect(slide, t.x, t.y, 3.87, 2.1, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: t.x, y: t.y + 0.02, w: 0.07, h: 2.06,
        fill: { color: t.accent }, line: { color: t.accent, transparency: 100 },
      });
      text(slide, t.metric, t.x + 0.22, t.y + 0.12, 3.5, 0.44, { fontSize: 22, bold: true, color: t.accent });
      text(slide, t.title, t.x + 0.22, t.y + 0.6, 3.5, 0.3, { fontSize: 13.6, bold: true, color: theme.colors.ink });
      text(slide, t.body.replace(/\n/g, ' '), t.x + 0.22, t.y + 0.96, 3.5, 1.04, { fontSize: 10.6, color: theme.colors.slate, valign: 'top' });
    });

    rect(slide, 0.58, 6.22, 12.15, 0.62, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Lưu ý:', 0.8, 6.38, 0.8, 0.28, { fontSize: 12.6, bold: true, color: theme.colors.amber });
    text(slide, 'Các lượt chạy ngắn để kiểm chứng cơ chế trên Docker Compose (một máy); chưa phải số liệu benchmark lặp lại nhiều lượt.', 1.6, 6.38, 10.95, 0.28, { fontSize: 12.2, color: theme.colors.ink });

    slide.addNotes(notesFor(12));
  }

  // =========================================================================
  // SLIDE 13 — Cách đánh giá hiệu năng
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Phương pháp kiểm thử tải',
      title: 'Cách đánh giá hiệu năng: Kịch bản tải và hệ thống chỉ số đo',
      source: '[B] §§19, 21, 22  ·  [S] Slide 13',
      number: 13,
    });

    text(slide, 'Hai kịch bản kiểm thử tải', 0.58, 1.5, 5.8, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    addCard(slide, {
      x: 0.58, y: 1.86, w: 5.75, h: 1.98,
      title: '1 · Ép tải đặt lịch (Booking High-Load)',
      body: '• Quy mô: 30 VU · 60 giây giữ tải · 2.400 slot (10 bác sĩ × 5 ngày).\n• Mục tiêu: Đo tốc độ xử lý giao dịch đặt lịch, tranh chấp giữ chỗ (contention) và thời gian hoàn tất SAGA.',
      accent: theme.colors.navy, titleSize: 15, bodySize: 12.4, titleHeight: 0.36,
    });
    addPill(slide, '30 VU · 2.400 SLOT · 3 LƯỢT ĐO LẶP', 0.78, 3.46, 2.85, theme.colors.sky, theme.colors.navy, { h: 0.24, fontSize: 8.6 });

    addCard(slide, {
      x: 0.58, y: 4.06, w: 5.75, h: 1.98,
      title: '2 · Tải hỗn hợp Đọc & Ghi (Mixed Workload)',
      body: '• Quy mô: Duy trì 5 phút ở mức Tải thường (20 VU) và Cao điểm (30 VU) với ~70% request Đọc và ~30% request Ghi chạy đồng thời.\n• Mục tiêu: Đo độ trễ và thông lượng khi nhiều miền cùng hoạt động.',
      accent: theme.colors.teal, titleSize: 15, bodySize: 12.4, titleHeight: 0.36,
    });
    addPill(slide, '20 VU & 30 VU · 5 PHÚT DUY TRÌ TẢI', 0.78, 5.66, 2.85, theme.colors.sky, theme.colors.teal, { h: 0.24, fontSize: 8.6 });

    text(slide, 'Các nhóm chỉ số đo tách biệt', 6.68, 1.5, 5.8, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    const metricGroups = [
      {
        y: 1.86,
        title: 'Chỉ số kịch bản Ép tải đặt lịch',
        accent: theme.colors.navy,
        body: '• Độ trễ p95 phản hồi API chung (gộp 201 Created và 409 Conflict)\n• Độ trễ p95 tạo lịch thành công (201) và toàn trình POST → Confirmed',
      },
      {
        y: 3.3,
        title: 'Chỉ số kịch bản Tải hỗn hợp',
        accent: theme.colors.teal,
        body: '• Độ trễ p95 thao tác Đọc (Read) và thao tác Ghi tổng hợp (Write)\n• Độ trễ p95 tạo lịch (Booking create) và thông lượng (requests/giây)',
      },
      {
        y: 4.74,
        title: 'Phân tách xung đột 409 và lỗi máy chủ 5xx',
        accent: theme.colors.amber,
        body: '• Phản hồi HTTP 409 do trùng slot được ghi nhận riêng là xung đột hợp lệ.\n• Độ tin cậy kỹ thuật được đo bằng tỷ lệ lỗi 5xx và số lịch Confirmed.',
      },
    ];

    metricGroups.forEach((m) => {
      addCard(slide, {
        x: 6.68, y: m.y, w: 6.05, h: 1.3,
        title: m.title, body: m.body, accent: m.accent,
        titleSize: 14, bodySize: 11.8, titleHeight: 0.32,
      });
    });

    rect(slide, 0.58, 6.24, 12.15, 0.62, theme.colors.sky, 'B9D4DC');
    text(slide, 'Ngân sách đối chiếu:', 0.8, 6.4, 2.15, 0.28, { fontSize: 13, bold: true, color: theme.colors.navy });
    text(slide, 'Cả hai hệ thống Monolith và Microservices đều được cấp cùng ngân sách tài nguyên 12,5 CPU / 5,06 GiB RAM trên Docker Compose.', 2.98, 6.4, 9.55, 0.28, { fontSize: 12.6, color: theme.colors.ink });

    slide.addNotes(notesFor(13));
  }

  // =========================================================================
  // SLIDE 14 — Quá trình cải tiến microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Lịch sử tối ưu hóa',
      title: 'Quá trình cải tiến microservices qua các mốc đo kiểm',
      source: '[B] §§14, 18, 19  ·  [P] §§6.2, 6.5  ·  [S] Slide 14',
      number: 14,
    });

    text(slide, 'Các mốc đo kiểm kỹ thuật (bao gồm cả lượt đối chiếu chưa đạt ngưỡng 500 ms)', 0.58, 1.5, 10.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    slide.addShape('line', {
      x: 1.2, y: 2.18, w: 10.9, h: 0,
      line: { color: theme.colors.teal, width: 2.2 },
    });

    const milestones = [
      {
        x: 0.58,
        step: 'Mốc 1 · Bản thô đầu',
        figure: '1,9 – 2,35 s',
        context: 'Booking p95 ban đầu',
        figColor: theme.colors.red,
        accent: theme.colors.amber,
        title: 'Vượt xa mục tiêu',
        body: 'Nhiều bước gọi dịch vụ tuần tự và phân bổ CPU/RAM chưa tối ưu.',
      },
      {
        x: 3.06,
        step: 'Mốc 2 · Tinh chỉnh luồng',
        figure: '~411 ms',
        context: 'Lượt đơn 20 VU',
        figColor: theme.colors.teal,
        accent: theme.colors.teal,
        title: 'Giảm bước gọi dư',
        body: 'Rà soát luồng giữ chỗ và phân bổ lại tài nguyên container ở mức 20 VU.',
      },
      {
        x: 5.54,
        step: 'Mốc 3 · Khóa 12,5 CPU',
        figure: '594,7 ms',
        context: '20 VU (Mono: 293,4 ms)',
        figColor: theme.colors.amber,
        accent: theme.colors.amber,
        title: 'Chưa đạt < 500 ms',
        body: 'Đối chiếu đồng tài nguyên 12,5 CPU; nhóm giữ nguyên kết quả để phân tích.',
      },
      {
        x: 8.02,
        step: 'Mốc 4 · Tối ưu SQL',
        figure: '449,0 ms',
        context: 'Median 3 lượt (Mono: 335,6)',
        figColor: theme.colors.teal,
        accent: theme.colors.teal,
        title: 'Gom nhóm tại SQL',
        body: 'Đổi kiểm tra sức chứa sang aggregate trong SQL thay vì tải toàn bộ slot về bộ nhớ.',
      },
      {
        x: 10.5,
        step: 'Mốc 5 · Tải cao & Mixed',
        figure: '253,6 ms',
        context: '30 VU · 2.400 slot & Mixed',
        figColor: theme.colors.navy,
        accent: theme.colors.navy,
        title: 'Mở rộng kịch bản đo',
        body: 'Đạt ngưỡng trên 3 lượt ép tải 2.400 slot và 2 cấu hình tải hỗn hợp 5 phút.',
      },
    ];

    milestones.forEach((m) => {
      addPill(slide, m.step, m.x + 0.16, 1.98, 1.92, theme.colors.navy, theme.colors.white, { h: 0.36, fontSize: 9.2 });

      rect(slide, m.x, 2.54, 2.24, 3.36, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: m.x, y: 2.54, w: 2.24, h: 0.07,
        fill: { color: m.accent }, line: { color: m.accent, transparency: 100 },
      });

      text(slide, m.figure, m.x + 0.14, 2.74, 1.96, 0.42, {
        fontSize: 20, bold: true, color: m.figColor,
      });
      addPill(slide, m.context, m.x + 0.14, 3.22, 1.96, theme.colors.sky, theme.colors.navy, { h: 0.26, fontSize: 8.6 });

      text(slide, m.title, m.x + 0.14, 3.64, 1.96, 0.32, {
        fontSize: 13.5, bold: true, color: theme.colors.ink,
      });
      text(slide, m.body, m.x + 0.14, 4.02, 1.96, 1.72, {
        fontSize: 11.8, color: theme.colors.slate, valign: 'top',
      });
    });

    rect(slide, 0.58, 6.18, 12.15, 0.66, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Lưu ý phương pháp:', 0.8, 6.36, 1.95, 0.28, { fontSize: 12.8, bold: true, color: theme.colors.amber });
    text(slide, 'Các mốc trên thuộc các cấu hình tải và kích thước mẫu khác nhau (20 mẫu/lượt so với 2.400 slot và tải hỗn hợp 5 phút), không ghép thành một chuỗi so sánh trực tiếp.', 2.78, 6.36, 9.75, 0.28, { fontSize: 12.2, color: theme.colors.ink });

    slide.addNotes(notesFor(14));
  }

  // =========================================================================
  // SLIDE 15 — Kết quả high-load và mixed-load mới nhất trên Microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Kết quả Microservices mới nhất',
      title: 'Kết quả high-load và mixed-load mới nhất trên Microservices',
      source: '[B] §§19, 21  ·  [E3]  ·  [S] Slide 15',
      number: 15,
    });

    const kpis = [
      {
        x: 0.58,
        val: '253,6 ms  ·  387,8 ms  ·  762 ms',
        label: 'High-load p95: API chung · Tạo lịch 201 · POST→Confirmed',
        accent: theme.colors.navy,
      },
      {
        x: 4.72,
        val: '26,8 ms (20 VU)  ·  49,7 ms (30 VU)',
        label: 'Độ trễ Đọc p95 dưới tải hỗn hợp 5 phút (Ghi: 91,3 · 187,8 ms)',
        accent: theme.colors.teal,
      },
      {
        x: 8.86,
        val: '12.995 lịch Confirmed  ·  0% 5xx',
        label: 'Tổng lịch xác nhận thành công (7.133 + 2.430 + 3.432) · 0 lỗi máy chủ',
        accent: theme.colors.green,
      },
    ];

    kpis.forEach((k) => {
      rect(slide, k.x, 1.56, 3.87, 0.96, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: k.x, y: 1.58, w: 0.07, h: 0.92,
        fill: { color: k.accent }, line: { color: k.accent, transparency: 100 },
      });
      text(slide, k.val, k.x + 0.2, 1.66, 3.55, 0.36, { fontSize: 15, bold: true, color: k.accent });
      text(slide, k.label, k.x + 0.2, 2.04, 3.55, 0.34, { fontSize: 10.8, color: theme.colors.slate });
    });

    const tx = 0.58;
    const ty = 2.74;
    const cols = [
      { label: 'Kịch bản tải', x: 0.58, w: 2.35 },
      { label: 'Cấu hình & Thời lượng', x: 2.93, w: 2.25 },
      { label: 'Khối lượng Đọc / Ghi & Đặt lịch', x: 5.18, w: 2.85 },
      { label: 'Độ trễ p95 chính', x: 8.03, w: 2.85 },
      { label: 'Kết quả & 5xx', x: 10.88, w: 1.85 },
    ];

    rect(slide, tx, ty, 12.15, 0.48, theme.colors.navy, theme.colors.navy, false);
    cols.forEach((c) => {
      text(slide, c.label, c.x + 0.12, ty + 0.1, c.w - 0.24, 0.28, {
        fontSize: 11.5, bold: true, color: theme.colors.white,
      });
    });

    const rows = [
      {
        profile: 'Booking High-Load\n(Ép tải ghi đặt lịch)',
        scale: '30 VU · 60s giữ tải\n2.400 slot · 3 lượt',
        volume: '34.398 yêu cầu đặt lịch\n7.133 lịch Confirmed (29,72/s)',
        latency: 'API chung: 253,6 ms\nTạo lịch: 387,8 ms · E2E: 762 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
        statusColor: theme.colors.green,
      },
      {
        profile: 'Mixed Normal\n(Tải hỗn hợp 20 VU)',
        scale: '20 VU · 5 phút giữ tải\n71,3% đọc / 28,7% ghi',
        volume: '18.108 Đọc (50,1/s) · 7.290 Ghi\n2.430 lịch Confirmed & hủy sạch',
        latency: 'Đọc: 26,8 ms · Ghi: 91,3 ms\nTạo lịch: 107,2 ms · HTTP: 65,5 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
        statusColor: theme.colors.green,
      },
      {
        profile: 'Mixed Peak\n(Tải hỗn hợp 30 VU)',
        scale: '30 VU · 5 phút giữ tải\n67,7% đọc / 32,3% ghi',
        volume: '32.139 Đọc (88,2/s) · 15.312 Ghi\n3.432 lịch Confirmed & hủy sạch',
        latency: 'Đọc: 49,7 ms · Ghi: 187,8 ms\nTạo lịch: 343,8 ms · HTTP: 112,7 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
        statusColor: theme.colors.green,
      },
    ];

    rows.forEach((r, i) => {
      const y = ty + 0.48 + i * 0.94;
      const bg = i % 2 === 1 ? theme.colors.paper : theme.colors.white;
      rect(slide, tx, y, 12.15, 0.94, bg, theme.colors.line, false);

      text(slide, r.profile, cols[0].x + 0.12, y + 0.16, cols[0].w - 0.24, 0.62, { fontSize: 12, bold: true, color: theme.colors.ink });
      text(slide, r.scale, cols[1].x + 0.12, y + 0.16, cols[1].w - 0.24, 0.62, { fontSize: 11.5, color: theme.colors.slate });
      text(slide, r.volume, cols[2].x + 0.12, y + 0.16, cols[2].w - 0.24, 0.62, { fontSize: 11.5, color: theme.colors.ink });
      text(slide, r.latency, cols[3].x + 0.12, y + 0.16, cols[3].w - 0.24, 0.62, { fontSize: 11.5, color: theme.colors.ink });
      text(slide, r.status, cols[4].x + 0.12, y + 0.16, cols[4].w - 0.24, 0.62, { fontSize: 11.8, bold: true, color: r.statusColor });
    });

    rect(slide, 0.58, 6.26, 12.15, 0.56, 'EAF0F4', 'EAF0F4', false);
    text(slide, 'Cấu hình đo: Cụm Microservices 12,5 CPU / 5,06 GiB RAM (Docker Compose) · 100% Idempotency · Cleanup 100% sau mỗi lượt.', 0.78, 6.42, 11.75, 0.24, {
      fontSize: 11, color: theme.colors.slate, align: 'center',
    });

    slide.addNotes(notesFor(15));
  }

  // =========================================================================
  // SLIDE 16 — Đối chiếu trực quan số liệu Monolith và Microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Đối chiếu số liệu thực nghiệm',
      title: 'Đối chiếu hiệu năng Monolith và Microservices (12,5 CPU)',
      source: '[B] §§19, 21, 22  ·  [E1–E3]  ·  [S] Slide 16',
      number: 16,
    });

    text(slide, 'Biểu đồ độ trễ p95 (ms) · Cột thấp hơn là nhanh hơn', 0.58, 1.48, 6.2, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    slide.addChart(pptx.ChartType.bar, [
      {
        name: 'Monolith',
        labels: [
          'Đọc (20 VU)',
          'Ghi (20 VU)',
          'Đọc (30 VU)',
          'Ghi (30 VU)',
          'API Đặt lịch',
          'Tạo lịch 201',
        ],
        values: [82.2, 111.3, 276.7, 362.4, 190.2, 214.6],
      },
      {
        name: 'Microservices',
        labels: [
          'Đọc (20 VU)',
          'Ghi (20 VU)',
          'Đọc (30 VU)',
          'Ghi (30 VU)',
          'API Đặt lịch',
          'Tạo lịch 201',
        ],
        values: [26.8, 91.3, 49.7, 187.8, 253.6, 387.8],
      },
    ], {
      x: 0.52, y: 1.8, w: 6.25, h: 4.42,
      catAxisLabelFontFace: theme.font, catAxisLabelFontSize: 9.8,
      valAxisLabelFontFace: theme.font, valAxisLabelFontSize: 9.5,
      valAxisMinVal: 0, valAxisMaxVal: 450, valAxisMajorUnit: 150,
      valAxisTitle: 'Mili giây (ms)', valAxisTitleFontFace: theme.font, valAxisTitleFontSize: 10,
      chartColors: [theme.colors.navy, theme.colors.teal],
      showLegend: true, legendPos: 'b', legendFontFace: theme.font, legendFontSize: 10.5,
      showTitle: false, showValue: true, dataLabelPosition: 'outEnd', dataLabelColor: theme.colors.ink, dataLabelFormatCode: '0',
      showValAxisTitle: true, showValAxis: true, showCatAxis: true,
      showBorder: false,
      valGridLine: { color: theme.colors.line, size: 0.8 },
      catGridLine: { style: 'none' },
      chartArea: { fill: { color: theme.colors.paper }, border: { color: theme.colors.paper, pt: 0 } },
      plotArea: { fill: { color: theme.colors.paper }, border: { color: theme.colors.paper, pt: 0 } },
      showLeaderLines: false,
      showPercent: false,
      showSerName: false,
    });

    text(slide, 'Bảng đối chiếu chi tiết', 6.98, 1.48, 3.2, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });
    addPill(slide, 'Ô MÀU XANH: SỐ LIỆU TỐT HƠN', 10.18, 1.48, 2.55, 'E3F5EC', theme.colors.green, {
      h: 0.26, fontSize: 8.6, border: 'B8E2CD',
    });

    const tx = 6.98;
    const ty = 1.84;
    const tw = 5.75;

    // Table header
    rect(slide, tx, ty, tw, 0.42, theme.colors.navy, theme.colors.navy, false);
    text(slide, 'Kịch bản & Chỉ số đo', tx + 0.14, ty + 0.08, 2.85, 0.26, { fontSize: 11, bold: true, color: theme.colors.white });
    text(slide, 'Monolith', tx + 3.05, ty + 0.08, 1.25, 0.26, { fontSize: 11, bold: true, color: theme.colors.white, align: 'center' });
    text(slide, 'Microservices', tx + 4.38, ty + 0.08, 1.28, 0.26, { fontSize: 11, bold: true, color: theme.colors.white, align: 'center' });

    // Table rows with winner highlight ('mono' | 'micro' | 'tie')
    const compRows = [
      { label: 'High-Load · API Đặt lịch chung (p95)', mono: '190,2 ms', micro: '253,6 ms', better: 'mono' },
      { label: 'High-Load · Tạo lịch 201 (p95)', mono: '214,6 ms', micro: '387,8 ms', better: 'mono' },
      { label: 'High-Load · POST → Confirmed (p95)', mono: '235 ms', micro: '762 ms', better: 'mono' },
      { label: 'High-Load · Lịch Confirmed / giây', mono: '29,84 / s', micro: '29,72 / s', better: 'tie' },
      { label: 'Mixed 20 VU · Độ trễ Đọc (p95)', mono: '82,2 ms', micro: '26,8 ms', better: 'micro' },
      { label: 'Mixed 20 VU · Độ trễ Ghi (p95)', mono: '111,3 ms', micro: '91,3 ms', better: 'micro' },
      { label: 'Mixed 20 VU · Độ trễ HTTP chung (p95)', mono: '92,4 ms', micro: '65,5 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Độ trễ Đọc (p95)', mono: '276,7 ms', micro: '49,7 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Độ trễ Ghi (p95)', mono: '362,4 ms', micro: '187,8 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Thông lượng Đọc', mono: '52,6 req/s', micro: '88,2 req/s', better: 'micro' },
    ];

    const rowH = 0.395;
    compRows.forEach((r, i) => {
      const y = ty + 0.42 + i * rowH;
      const bg = i % 2 === 1 ? theme.colors.paper : theme.colors.white;
      rect(slide, tx, y, tw, rowH, bg, theme.colors.line, false);

      text(slide, r.label, tx + 0.12, y + 0.07, 2.88, rowH - 0.14, {
        fontSize: 10.6, bold: true, color: theme.colors.ink,
      });

      // Monolith cell
      if (r.better === 'mono') {
        addPill(slide, r.mono, tx + 3.06, y + 0.06, 1.22, 'E3F5EC', theme.colors.green, {
          h: rowH - 0.12, fontSize: 10.6, border: 'B8E2CD',
        });
      } else {
        text(slide, r.mono, tx + 3.05, y + 0.07, 1.25, rowH - 0.14, {
          fontSize: 10.6, align: 'center', color: theme.colors.slate, bold: r.better === 'tie',
        });
      }

      // Microservices cell
      if (r.better === 'micro') {
        addPill(slide, r.micro, tx + 4.4, y + 0.06, 1.24, 'E3F5EC', theme.colors.green, {
          h: rowH - 0.12, fontSize: 10.6, border: 'B8E2CD',
        });
      } else {
        text(slide, r.micro, tx + 4.38, y + 0.07, 1.28, rowH - 0.14, {
          fontSize: 10.6, align: 'center', color: theme.colors.slate, bold: r.better === 'tie',
        });
      }
    });

    rect(slide, 0.58, 6.38, 12.15, 0.48, 'EAF0F4', 'EAF0F4', false);
    text(slide, 'Ngân sách tổng 12,5 CPU (~7,5 backend Mono vs ~4 đường booking Micro) · High-Load: trung vị 3 lượt, trần 2.400 slot · Đo trước bản vá RCSI 27/11.', 0.74, 6.5, 11.82, 0.24, {
      fontSize: 10.3, color: theme.colors.slate, align: 'center',
    });

    slide.addNotes(notesFor(16));
  }

  // =========================================================================
  // SLIDE 17 — Kết luận và hướng tiếp theo
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Kết luận & hướng tiếp theo',
      title: 'Kết luận tổng hợp và các bước hoàn thiện tiếp theo',
      source: '[S] Slide 17  ·  [P] §§6.3–6.4  ·  [B] §§19, 21, 22',
      number: 17,
    });

    const cols = [
      {
        x: 0.58,
        pill: '1 · KẾT QUẢ KIẾN TRÚC',
        accent: theme.colors.teal,
        title: 'Hoàn thành chuyển đổi hệ thống',
        items: [
          { h: 'Phân rã 6 dịch vụ & Gateway', d: 'Tách quyền sở hữu nghiệp vụ và 6 CSDL logic trên Docker Compose.' },
          { h: 'Chuẩn hóa Hợp đồng & Phối hợp', d: 'OpenAPI drift test, SAGA đặt lịch lai và Choreography sự kiện.' },
          { h: 'SLA/SLO/QoS đã kiểm chứng', d: 'Rate limit, deadline/breaker, load shedding, cô lập lỗi, backup PITR + diễn tập khôi phục.' },
        ],
      },
      {
        x: 4.72,
        pill: '2 · KẾT QUẢ ĐO KIỂM CHÍNH',
        accent: theme.colors.navy,
        title: 'Số liệu đối chiếu thực nghiệm',
        items: [
          { h: 'Ép tải đặt lịch (30 VU · 2.400 slot)', d: 'Monolith: 190,2 / 214,6 / 235 ms  ·  Micro: 253,6 / 387,8 / 762 ms.' },
          { h: 'Tải hỗn hợp Đọc & Ghi (20 & 30 VU)', d: 'Đọc p95 Micro: 26,8 & 49,7 ms (so với Monolith: 82,2 & 276,7 ms).' },
          { h: 'Thông lượng & Độ tin cậy', d: 'Confirmed ~29,8 vs ~29,7 lịch/giây · 100% Idempotency · 0% lỗi 5xx.' },
        ],
      },
      {
        x: 8.86,
        pill: '3 · HƯỚNG HOÀN THIỆN TIẾP THEO',
        accent: theme.colors.amber,
        title: 'Chuẩn hóa thực nghiệm sâu hơn',
        items: [
          { h: 'Khôi phục cùng Snapshot CSDL', d: 'Reset CSDL về trạng thái sạch trước mỗi lượt chạy để loại bỏ tích lũy lịch sử.' },
          { h: 'Lặp tối thiểu 3 lượt Tải hỗn hợp', d: 'Đo lặp nhiều lượt cho cả 20 VU và 30 VU để báo cáo trung vị & phương sai.' },
          { h: 'Đo chuỗi CPU / RAM đồng nhất', d: 'Thu thập mức tiêu thụ tài nguyên theo cùng chu kỳ trên cả hai kiến trúc.' },
        ],
      },
    ];

    cols.forEach((c) => {
      rect(slide, c.x, 1.66, 3.87, 4.38, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: c.x, y: 1.68, w: 0.07, h: 4.34,
        fill: { color: c.accent }, line: { color: c.accent, transparency: 100 },
      });
      text(slide, c.title, c.x + 0.2, 1.8, 3.48, 0.34, {
        fontSize: 15, bold: true, color: theme.colors.ink,
      });

      c.items.forEach((item, idx) => {
        const iy = 2.28 + idx * 1.05;
        rect(slide, c.x + 0.18, iy, 3.5, 0.92, theme.colors.paper, theme.colors.line);
        text(slide, item.h, c.x + 0.3, iy + 0.08, 3.26, 0.26, {
          fontSize: 12.4, bold: true, color: c.accent,
        });
        text(slide, item.d, c.x + 0.3, iy + 0.36, 3.26, 0.5, {
          fontSize: 11, color: theme.colors.slate, valign: 'top',
        });
      });

      addPill(slide, c.pill, c.x + 0.2, 5.62, 2.75, theme.colors.sky, theme.colors.navy, { h: 0.26, fontSize: 8.4 });
    });

    rect(slide, 0.58, 6.24, 12.15, 0.62, theme.colors.sky, 'B9D4DC');
    text(slide, 'Tổng kết đồ án:', 0.82, 6.4, 1.85, 0.28, { fontSize: 13, bold: true, color: theme.colors.navy });
    text(slide, 'Hoàn thành kiến trúc Microservices chạy thực tế và bộ số liệu đối chiếu trực diện với Monolith trên cả hai chế độ tải đơn miền và hỗn hợp.', 2.7, 6.4, 9.85, 0.28, { fontSize: 12.5, color: theme.colors.ink });

    slide.addNotes(notesFor(17));
  }

  // =========================================================================
  // SLIDE 18 — Tài liệu tham khảo (Phụ lục ngoài thời lượng 15 phút)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Phụ lục · Nguồn đối chiếu (Không tính vào 15 phút thuyết trình)',
      title: 'Tài liệu tham khảo và bằng chứng kiểm thử dự án',
      source: 'Danh mục nguồn đối chiếu nội bộ dự án [S, P, Q, B, R, T, C, E1–E3]',
      number: 18,
    });

    const leftRefs = [
      {
        code: '[S]',
        path: 'docs/PRESENTATION_SCRIPT.md',
        desc: 'Kịch bản thuyết trình 15 phút: thứ tự 17 slide, lời dẫn và phân tích đối chiếu hai kiến trúc.',
      },
      {
        code: '[P]',
        path: 'docs/PROJECT_REPORT.md',
        desc: 'Báo cáo tổng hợp dự án: kiến trúc/ERD Monolith, phân rã 6 dịch vụ, SAGA và QoS.',
      },
      {
        code: '[Q]',
        path: 'tasks/SLI_SLO_MATRIX_MEMBER_A.md',
        desc: 'Ma trận chỉ số SLI, mục tiêu SLO nội bộ, Rate Limit và quy tắc đo theo ranh giới.',
      },
      {
        code: '[B]',
        path: 'tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md (§§14–22)',
        desc: 'Báo cáo kiểm thử tải: lịch sử tối ưu, ép tải 2.400 slot (§19), tải hỗn hợp (§21) và đối chiếu Monolith (§22).',
      },
      {
        code: '[R]',
        path: 'docs/BACKUP_RECOVERY.md',
        desc: 'Thiết kế sao lưu full + differential + log, off-site, diễn tập khôi phục và runbook RPO/RTO.',
      },
    ];

    const rightRefs = [
      {
        code: '[C]',
        path: 'tasks/contracts/ & ApiContractDriftTests.cs',
        desc: 'Bản chụp đặc tả OpenAPI của Gateway + 6 dịch vụ và bộ kiểm thử chống lệch chuẩn API.',
      },
      {
        code: '[E1]',
        path: 'tests/load/evidence/highload-monolith-2026-11-22*.json',
        desc: 'Bằng chứng đã làm sạch của 3 lượt ép tải đặt lịch nghiệm thu trên Monolith (0126, 0131, 0139).',
      },
      {
        code: '[E2]',
        path: 'tests/load/evidence/mixed-*-monolith-2026-11-22.json',
        desc: 'Bằng chứng đã làm sạch của 2 kịch bản tải hỗn hợp (Normal 20 VU và Peak 30 VU) trên Monolith.',
      },
      {
        code: '[E3]',
        path: 'tests/load/evidence/*-2026-11-17*.json',
        desc: 'Bằng chứng đã làm sạch của 3 lượt ép tải đặt lịch và 2 kịch bản tải hỗn hợp trên Microservices.',
      },
      {
        code: '[T]',
        path: 'tests/load/README.md (§5) & tests/load/evidence/*-summary.json',
        desc: 'Bộ kiểm thử SLO/QoS: qos-policy, fault-isolation, login-capacity, spike và backup-drill (27/11/2026).',
      },
    ];

    const renderRefColumn = (items, xBase) => {
      items.forEach((item, idx) => {
        const y = 1.5 + idx * 0.93;
        rect(slide, xBase, y, 5.92, 0.84, theme.colors.white, theme.colors.line);
        addPill(slide, item.code, xBase + 0.16, y + 0.11, 0.62, theme.colors.navy, theme.colors.white, { h: 0.26, fontSize: 9.5 });
        text(slide, item.path, xBase + 0.88, y + 0.11, 4.92, 0.26, {
          fontSize: 11.2, bold: true, color: theme.colors.teal,
        });
        text(slide, item.desc, xBase + 0.16, y + 0.4, 5.6, 0.4, {
          fontSize: 11.4, color: theme.colors.slate, valign: 'top',
        });
      });
    };

    renderRefColumn(leftRefs, 0.58);
    renderRefColumn(rightRefs, 6.81);

    rect(slide, 0.58, 6.22, 12.15, 0.62, theme.colors.sky, 'B9D4DC');
    text(slide, 'Truy xuất nguồn gốc:', 0.82, 6.38, 2.15, 0.28, { fontSize: 12.8, bold: true, color: theme.colors.navy });
    text(slide, 'Mọi sơ đồ và con số trên slide đều khớp với báo cáo dự án và tệp bằng chứng JSON đã làm sạch (không chứa token/dữ liệu nhạy cảm).', 3.0, 6.38, 9.5, 0.28, { fontSize: 12.4, color: theme.colors.ink });

    slide.addNotes(notesFor(18));
  }
}

module.exports = { buildSlides11To17 };

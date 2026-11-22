'use strict';

const { addArrow, notesFor, rect, text, theme } = require('../helpers');
const { addLiveBase, addLiveCard, addLivePill } = require('./live-01-08');

function buildLiveSlides09To16(pptx) {
  // =========================================================================
  // SLIDE 9 — Check-in, Queue và choreography
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Luồng hàng đợi & sự kiện',
      title: 'Check-in, Queue và choreography: Phối hợp hướng sự kiện',
      number: 9,
    });

    addLivePill(slide, 'Sự kiện: AppointmentCheckedIn', 2.25, 1.66, 2.75, theme.colors.sky, theme.colors.teal, { h: 0.36, fontSize: 11.5 });
    addLivePill(slide, 'Sự kiện: PatientCalled / Exam*', 8.28, 1.66, 2.68, theme.colors.sky, theme.colors.teal, { h: 0.36, fontSize: 11.5 });

    // Step 1
    addLiveCard(slide, {
      x: 0.58, y: 2.18, w: 2.65, h: 1.98,
      title: '1 · Appointment',
      body: 'Xác nhận Check-in\nNguồn sự thật lịch hẹn\nGhi trạng thái & Outbox',
      accent: theme.colors.navy, titleSize: 18, bodySize: 14.8, titleHeight: 0.38,
    });

    addArrow(slide, 3.27, 3.16, 3.75, 3.16, { color: theme.colors.teal, dash: 'dash', width: 2.2 });

    // Step 2
    addLiveCard(slide, {
      x: 3.8, y: 2.18, w: 2.75, h: 1.98,
      title: '2 · Queue Service',
      body: 'Tiêu thụ sự kiện lũy đẳng\nTạo đúng 1 phiếu chờ\nCấp số theo bác sĩ / ngày',
      accent: theme.colors.teal, titleSize: 18, bodySize: 14.8, titleHeight: 0.38,
    });

    addArrow(slide, 6.59, 3.16, 6.95, 3.16, { color: theme.colors.navy, width: 2 });

    // Step 3
    addLiveCard(slide, {
      x: 7.0, y: 2.18, w: 2.65, h: 1.98,
      title: '3 · Điều phối khám',
      body: 'Lễ tân: gọi số, ưu tiên, retry\nBác sĩ: gọi kế tiếp, bỏ qua,\nbắt đầu & hoàn tất khám',
      accent: theme.colors.navy, titleSize: 18, bodySize: 14.5, titleHeight: 0.38,
    });

    addArrow(slide, 9.69, 2.68, 10.15, 2.68, { color: theme.colors.teal, dash: 'dash', width: 2 });
    addArrow(slide, 9.69, 3.68, 10.15, 3.68, { color: theme.colors.teal, dash: 'dash', width: 2 });

    // Step 4a & 4b
    addLiveCard(slide, {
      x: 10.2, y: 2.16, w: 2.53, h: 0.96,
      title: '4a · Medical Records',
      body: 'Mở hồ sơ ca khám',
      accent: theme.colors.teal, titleSize: 16, bodySize: 13.8, titleHeight: 0.32,
    });
    addLiveCard(slide, {
      x: 10.2, y: 3.22, w: 2.53, h: 0.96,
      title: '4b · Notification',
      body: 'Thông báo gọi số',
      accent: theme.colors.amber, titleSize: 16, bodySize: 13.8, titleHeight: 0.32,
    });

    const safeguards = [
      {
        x: 0.58,
        title: 'Snapshot trong sự kiện',
        body: 'Đính kèm sẵn dữ liệu tối thiểu;\nkhông cần gọi ngược Appointment',
        accent: theme.colors.teal,
      },
      {
        x: 4.72,
        title: 'Chống trùng & Chốt trạng thái',
        body: 'Xử lý lũy đẳng theo AppointmentId;\nkiểm tra khi sự kiện sai thứ tự',
        accent: theme.colors.navy,
      },
      {
        x: 8.86,
        title: 'Thử lại & Hàng đợi lỗi (DLQ)',
        body: 'Giới hạn số lần retry tự động;\ncô lập sự kiện lỗi vào DLQ',
        accent: theme.colors.amber,
      },
    ];

    safeguards.forEach((s) => {
      addLiveCard(slide, {
        x: s.x, y: 4.66, w: 3.87, h: 1.82,
        title: s.title, body: s.body, accent: s.accent,
        titleSize: 16.5, bodySize: 15, titleHeight: 0.4,
      });
    });

    slide.addNotes(notesFor(9));
  }

  // =========================================================================
  // SLIDE 10 — OpenAPI, kiểm thử và vận hành
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Hợp đồng API & vận hành',
      title: 'OpenAPI, kiểm thử tự động và công cụ quan sát vận hành',
      number: 10,
    });

    const blocks = [
      {
        x: 0.58, y: 1.72,
        title: '1 · Hợp đồng OpenAPI & Kiểm tra lệch chuẩn',
        tag: 'OpenAPI · Swagger · Contract Drift',
        accent: theme.colors.navy,
        body: '• Đặc tả OpenAPI cho Gateway và 6 dịch vụ\n• Tự động phát hiện khi mã nguồn lệch tài liệu API',
      },
      {
        x: 6.81, y: 1.72,
        title: '2 · Kiểm thử Đa tầng & Bất biến nghiệp vụ',
        tag: 'Unit · Integration · Failure · QoS',
        accent: theme.colors.teal,
        body: '• Kiểm thử luồng lỗi SAGA, mất kết nối broker & QoS\n• Đảm bảo 0 đặt vượt chỗ & 0 trùng phiếu hàng đợi',
      },
      {
        x: 0.58, y: 4.26,
        title: '3 · Giám sát Sức khỏe & Docker Compose',
        tag: '/healthz · /readyz · Docker Compose',
        accent: theme.colors.teal,
        body: '• Endpoint kiểm tra tiến trình (/healthz) & kết nối (/readyz)\n• Tự động chạy migration và khởi tạo toàn cụm dịch vụ',
      },
      {
        x: 6.81, y: 4.26,
        title: '4 · Đo lường (Metrics) & Truy vết (Tracing)',
        tag: 'Prometheus · Jaeger · OTLP',
        accent: theme.colors.amber,
        body: '• Prometheus thu thập độ trễ, tỷ lệ lỗi và cảnh báo\n• Jaeger thu nhận trace OTLP xuyên qua các dịch vụ',
      },
    ];

    blocks.forEach((b) => {
      addLiveCard(slide, {
        x: b.x, y: b.y, w: 5.92, h: 2.28,
        title: b.title, body: b.body, accent: b.accent,
        titleSize: 18.5, bodySize: 15.5, titleHeight: 0.44,
      });
      addLivePill(slide, b.tag, b.x + 0.22, b.y + 1.74, 3.65, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });
    });

    slide.addNotes(notesFor(10));
  }

  // =========================================================================
  // SLIDE 11 — SLA, SLI, SLO và QoS
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Chỉ tiêu chất lượng dịch vụ',
      title: 'Khung đo lường SLI, mục tiêu SLO và chính sách QoS',
      number: 11,
    });

    const targets = [
      {
        x: 0.58, y: 1.72,
        metric: '≥ 99,0%',
        title: 'Độ sẵn sàng tại Gateway',
        accent: theme.colors.teal,
        body: 'Tính lỗi 5xx là sự cố;\nloại trừ xung đột 409 & giới hạn 429',
      },
      {
        x: 4.72, y: 1.72,
        metric: '≤ 500 ms',
        title: 'Độ trễ API p95',
        accent: theme.colors.navy,
        body: 'p99 ≤ 1.500 ms;\nđo riêng tại từng ranh giới dịch vụ',
      },
      {
        x: 8.86, y: 1.72,
        metric: '≤ 1,0%',
        title: 'Tỷ lệ lỗi máy chủ (5xx)',
        accent: theme.colors.teal,
        body: 'Đo trên tập mẫu hợp lệ;\ntách biệt khỏi từ chối nghiệp vụ',
      },
      {
        x: 0.58, y: 4.26,
        metric: '≤ 5 giây',
        title: 'Độ trễ xử lý sự kiện (p95)',
        accent: theme.colors.navy,
        body: 'Từ lúc phát lên RabbitMQ\nđến khi dịch vụ đích xử lý xong',
      },
      {
        x: 4.72, y: 4.26,
        metric: '0 vi phạm',
        title: 'Bất biến dữ liệu & DLQ',
        accent: theme.colors.green,
        body: '0 đặt vượt chỗ, 0 trùng phiếu;\n0 thông điệp hợp lệ kẹt ở DLQ',
      },
      {
        x: 8.86, y: 4.26,
        metric: '100 req/phút',
        title: 'Giới hạn tốc độ (Rate Limit)',
        accent: theme.colors.amber,
        body: 'Cửa sổ cố định theo tài khoản/IP;\nkết hợp Timeout, Retry & Idempotency',
      },
    ];

    targets.forEach((t) => {
      rect(slide, t.x, t.y, 3.87, 2.28, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: t.x, y: t.y + 0.02, w: 0.08, h: 2.24,
        fill: { color: t.accent }, line: { color: t.accent, transparency: 100 },
      });
      text(slide, t.metric, t.x + 0.24, t.y + 0.18, 3.45, 0.54, {
        fontSize: 28, bold: true, color: t.accent,
      });
      text(slide, t.title, t.x + 0.24, t.y + 0.8, 3.45, 0.38, {
        fontSize: 17.5, bold: true, color: theme.colors.ink,
      });
      text(slide, t.body, t.x + 0.24, t.y + 1.28, 3.45, 0.82, {
        fontSize: 14.5, color: theme.colors.slate, valign: 'top',
      });
    });

    slide.addNotes(notesFor(11));
  }

  // =========================================================================
  // SLIDE 12 — Cách đánh giá hiệu năng
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Phương pháp kiểm thử tải',
      title: 'Cách đánh giá hiệu năng: Kịch bản tải và chỉ số đo lường',
      number: 12,
    });

    addLiveCard(slide, {
      x: 0.58, y: 1.72, w: 5.75, h: 2.28,
      title: '1 · Ép tải đặt lịch (Booking High-Load)',
      body: '• 30 VU tranh chấp trên 2.400 slot (10 bác sĩ × 5 ngày)\n• Đo tốc độ giao dịch ghi đơn luồng & hoàn tất SAGA',
      accent: theme.colors.navy, titleSize: 19, bodySize: 15.5, titleHeight: 0.44,
    });
    addLivePill(slide, '30 VU · 2.400 SLOT · 3 LƯỢT ĐO LẶP', 0.8, 3.46, 3.55, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });

    addLiveCard(slide, {
      x: 0.58, y: 4.26, w: 5.75, h: 2.28,
      title: '2 · Tải hỗn hợp Đọc & Ghi (Mixed)',
      body: '• Duy trì 5 phút ở mức 20 VU và 30 VU (~70% Đọc / ~30% Ghi)\n• Đo khả năng chia tải khi nhiều miền cùng hoạt động',
      accent: theme.colors.teal, titleSize: 19, bodySize: 15.5, titleHeight: 0.44,
    });
    addLivePill(slide, '20 VU & 30 VU · 5 PHÚT DUY TRÌ TẢI', 0.8, 6.0, 3.55, theme.colors.sky, theme.colors.teal, { h: 0.36, fontSize: 12 });

    const metricGroups = [
      {
        y: 1.72,
        title: 'Chỉ số Ép tải đặt lịch (p95)',
        accent: theme.colors.navy,
        body: '• Phản hồi API chung  ·  Tạo lịch thành công (201)\n• Thời gian toàn trình POST → Confirmed',
      },
      {
        y: 3.32,
        title: 'Chỉ số Tải hỗn hợp (p95 & RPS)',
        accent: theme.colors.teal,
        body: '• Độ trễ thao tác Đọc (Read) & thao tác Ghi (Write)\n• Độ trễ tạo lịch hẹn & Thông lượng (requests/giây)',
      },
      {
        y: 4.92,
        title: 'Điều kiện đối chiếu công bằng',
        accent: theme.colors.amber,
        body: '• Cùng ngân sách 12,5 CPU / 5,06 GiB RAM trên Compose\n• Tách xung đột giữ chỗ 409 khỏi tỷ lệ lỗi máy chủ 5xx',
      },
    ];

    metricGroups.forEach((m) => {
      addLiveCard(slide, {
        x: 6.68, y: m.y, w: 6.05, h: 1.44,
        title: m.title, body: m.body, accent: m.accent,
        titleSize: 18, bodySize: 15, titleHeight: 0.38,
      });
    });

    slide.addNotes(notesFor(12));
  }

  // =========================================================================
  // SLIDE 13 — Quá trình cải tiến microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Lịch sử tối ưu hóa',
      title: 'Quá trình cải tiến Microservices qua 5 mốc đo kiểm',
      number: 13,
    });

    slide.addShape('line', {
      x: 1.2, y: 2.02, w: 10.9, h: 0,
      line: { color: theme.colors.teal, width: 2.5 },
    });

    const milestones = [
      {
        x: 0.58,
        step: 'Mốc 1 · Bản thô',
        figure: '1,9 – 2,35 s',
        context: 'Booking p95 đầu',
        figColor: theme.colors.red,
        accent: theme.colors.amber,
        title: 'Vượt ngưỡng',
        body: 'Gọi tuần tự nhiều bước &\nchưa tối ưu CPU/RAM',
      },
      {
        x: 3.06,
        step: 'Mốc 2 · Tinh chỉnh',
        figure: '~411 ms',
        context: 'Lượt đơn 20 VU',
        figColor: theme.colors.teal,
        accent: theme.colors.teal,
        title: 'Giảm bước gọi',
        body: 'Tối ưu luồng giữ chỗ &\nphân bổ lại container',
      },
      {
        x: 5.54,
        step: 'Mốc 3 · 12,5 CPU',
        figure: '594,7 ms',
        context: 'Mono: 293,4 ms',
        figColor: theme.colors.amber,
        accent: theme.colors.amber,
        title: 'Chưa đạt < 500 ms',
        body: 'Đối chiếu 12,5 CPU;\ngiữ kết quả để phân tích',
      },
      {
        x: 8.02,
        step: 'Mốc 4 · Tối ưu SQL',
        figure: '449,0 ms',
        context: 'Mono: 335,6 ms',
        figColor: theme.colors.teal,
        accent: theme.colors.teal,
        title: 'Gom nhóm tại SQL',
        body: 'Đổi kiểm tra sức chứa\nsang SQL Aggregate',
      },
      {
        x: 10.5,
        step: 'Mốc 5 · Tải cao',
        figure: '253,6 ms',
        context: '30 VU · 2.400 slot',
        figColor: theme.colors.navy,
        accent: theme.colors.navy,
        title: 'Đạt ngưỡng SLO',
        body: 'Vượt ngưỡng trên cả\nHigh-Load & Mixed 5p',
      },
    ];

    milestones.forEach((m) => {
      addLivePill(slide, m.step, m.x + 0.12, 1.8, 2.0, theme.colors.navy, theme.colors.white, { h: 0.42, fontSize: 12 });

      rect(slide, m.x, 2.48, 2.24, 4.1, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: m.x, y: 2.48, w: 2.24, h: 0.08,
        fill: { color: m.accent }, line: { color: m.accent, transparency: 100 },
      });

      text(slide, m.figure, m.x + 0.12, 2.74, 2.0, 0.5, {
        fontSize: 22.5, bold: true, color: m.figColor,
      });
      addLivePill(slide, m.context, m.x + 0.12, 3.34, 2.0, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 11.5 });

      text(slide, m.title, m.x + 0.12, 3.94, 2.0, 0.38, {
        fontSize: 15.2, bold: true, color: theme.colors.ink,
      });
      text(slide, m.body, m.x + 0.14, 4.46, 1.96, 1.8, {
        fontSize: 14.5, color: theme.colors.slate, valign: 'top',
      });
    });

    slide.addNotes(notesFor(13));
  }

  // =========================================================================
  // SLIDE 14 — Kết quả high-load và mixed-load mới nhất trên Microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Kết quả Microservices mới nhất',
      title: 'Kết quả High-Load và Mixed-Load trên Microservices',
      number: 14,
    });

    const kpis = [
      {
        x: 0.58,
        val: '253,6  ·  387,8  ·  762 ms',
        label: 'High-Load p95: API · Tạo lịch · E2E',
        accent: theme.colors.navy,
      },
      {
        x: 4.72,
        val: '26,8 ms  ·  49,7 ms',
        label: 'Đọc p95 tải hỗn hợp (20 VU · 30 VU)',
        accent: theme.colors.teal,
      },
      {
        x: 8.86,
        val: '12.995 lịch Confirmed · 0% 5xx',
        label: 'Hoàn tất 100% mục tiêu trên cả 3 kịch bản',
        accent: theme.colors.green,
      },
    ];

    kpis.forEach((k) => {
      rect(slide, k.x, 1.62, 3.87, 1.18, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: k.x, y: 1.64, w: 0.08, h: 1.14,
        fill: { color: k.accent }, line: { color: k.accent, transparency: 100 },
      });
      text(slide, k.val, k.x + 0.22, 1.74, 3.52, 0.42, { fontSize: 16.2, bold: true, color: k.accent });
      text(slide, k.label, k.x + 0.22, 2.24, 3.52, 0.38, { fontSize: 13.8, color: theme.colors.slate });
    });

    const tx = 0.58;
    const ty = 3.06;
    const cols = [
      { label: 'Kịch bản tải', x: 0.58, w: 2.45 },
      { label: 'Quy mô & Thời gian', x: 3.03, w: 2.35 },
      { label: 'Khối lượng hoàn tất', x: 5.38, w: 2.65 },
      { label: 'Độ trễ p95 chính', x: 8.03, w: 2.85 },
      { label: 'Kết quả & 5xx', x: 10.88, w: 1.85 },
    ];

    rect(slide, tx, ty, 12.15, 0.56, theme.colors.navy, theme.colors.navy, false);
    cols.forEach((c) => {
      text(slide, c.label, c.x + 0.14, ty + 0.12, c.w - 0.28, 0.32, {
        fontSize: 14, bold: true, color: theme.colors.white,
      });
    });

    const rows = [
      {
        profile: 'Booking High-Load\n(Ép tải đặt lịch)',
        scale: '30 VU · 60s giữ tải\n2.400 slot · 3 lượt',
        volume: '34.398 yêu cầu\n7.133 lịch Confirmed',
        latency: 'API: 253,6 ms · Tạo: 387,8 ms\nPOST → Confirmed: 762 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
      },
      {
        profile: 'Mixed Normal\n(Tải hỗn hợp thường)',
        scale: '20 VU · 5 phút\n71% Đọc / 29% Ghi',
        volume: '18.108 Đọc · 7.290 Ghi\n2.430 lịch Confirmed',
        latency: 'Đọc: 26,8 ms · Ghi: 91,3 ms\nTạo lịch: 107,2 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
      },
      {
        profile: 'Mixed Peak\n(Tải hỗn hợp cao điểm)',
        scale: '30 VU · 5 phút\n68% Đọc / 32% Ghi',
        volume: '32.139 Đọc · 15.312 Ghi\n3.432 lịch Confirmed',
        latency: 'Đọc: 49,7 ms · Ghi: 187,8 ms\nTạo lịch: 343,8 ms',
        status: 'Đạt ngưỡng\n0% lỗi 5xx',
      },
    ];

    rows.forEach((r, i) => {
      const y = ty + 0.56 + i * 1.04;
      const bg = i % 2 === 1 ? theme.colors.paper : theme.colors.white;
      rect(slide, tx, y, 12.15, 1.04, bg, theme.colors.line, false);

      text(slide, r.profile, cols[0].x + 0.14, y + 0.16, cols[0].w - 0.28, 0.72, { fontSize: 14.5, bold: true, color: theme.colors.ink });
      text(slide, r.scale, cols[1].x + 0.14, y + 0.16, cols[1].w - 0.28, 0.72, { fontSize: 13.8, color: theme.colors.slate });
      text(slide, r.volume, cols[2].x + 0.14, y + 0.16, cols[2].w - 0.28, 0.72, { fontSize: 13.8, color: theme.colors.ink });
      text(slide, r.latency, cols[3].x + 0.14, y + 0.16, cols[3].w - 0.28, 0.72, { fontSize: 13.8, color: theme.colors.ink });
      text(slide, r.status, cols[4].x + 0.14, y + 0.16, cols[4].w - 0.28, 0.72, { fontSize: 14.2, bold: true, color: theme.colors.green });
    });

    slide.addNotes(notesFor(14));
  }

  // =========================================================================
  // SLIDE 15 — Đối chiếu trực quan số liệu Monolith và Microservices
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Đối chiếu số liệu thực nghiệm',
      title: 'Đối chiếu hiệu năng Monolith và Microservices (12,5 CPU)',
      number: 15,
    });

    text(slide, 'Độ trễ p95 (ms) · Thấp hơn là nhanh hơn', 0.58, 1.5, 6.1, 0.34, {
      fontSize: 15, bold: true, color: theme.colors.slate,
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
      x: 0.52, y: 1.9, w: 6.15, h: 4.88,
      catAxisLabelFontFace: theme.font, catAxisLabelFontSize: 11.5,
      valAxisLabelFontFace: theme.font, valAxisLabelFontSize: 11,
      valAxisMinVal: 0, valAxisMaxVal: 450, valAxisMajorUnit: 150,
      valAxisTitle: 'Mili giây (ms)', valAxisTitleFontFace: theme.font, valAxisTitleFontSize: 11.5,
      chartColors: [theme.colors.navy, theme.colors.teal],
      showLegend: true, legendPos: 'b', legendFontFace: theme.font, legendFontSize: 12.5,
      showTitle: false, showValue: true, dataLabelPosition: 'outEnd', dataLabelColor: theme.colors.ink, dataLabelFormatCode: '0',
      dataLabelFontSize: 11.5,
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

    text(slide, 'Bảng số liệu đối chiếu', 6.88, 1.5, 3.0, 0.34, {
      fontSize: 15, bold: true, color: theme.colors.slate,
    });
    addLivePill(slide, 'Ô XANH: TỐT HƠN', 10.35, 1.48, 2.38, 'E3F5EC', theme.colors.green, {
      h: 0.36, fontSize: 11.5, border: 'B8E2CD',
    });

    const tx = 6.88;
    const ty = 1.94;
    const tw = 5.85;

    // Table header
    rect(slide, tx, ty, tw, 0.5, theme.colors.navy, theme.colors.navy, false);
    text(slide, 'Chỉ số đo', tx + 0.14, ty + 0.1, 2.85, 0.3, { fontSize: 13.5, bold: true, color: theme.colors.white });
    text(slide, 'Monolith', tx + 3.02, ty + 0.1, 1.32, 0.3, { fontSize: 13.5, bold: true, color: theme.colors.white, align: 'center' });
    text(slide, 'Microservices', tx + 4.4, ty + 0.1, 1.38, 0.3, { fontSize: 13.5, bold: true, color: theme.colors.white, align: 'center' });

    const compRows = [
      { label: 'High-Load · API Đặt lịch (p95)', mono: '190,2 ms', micro: '253,6 ms', better: 'mono' },
      { label: 'High-Load · Tạo lịch 201 (p95)', mono: '214,6 ms', micro: '387,8 ms', better: 'mono' },
      { label: 'High-Load · POST → Confirmed', mono: '235 ms', micro: '762 ms', better: 'mono' },
      { label: 'High-Load · Lịch Confirmed / s', mono: '29,84 / s', micro: '29,72 / s', better: 'tie' },
      { label: 'Mixed 20 VU · Độ trễ Đọc (p95)', mono: '82,2 ms', micro: '26,8 ms', better: 'micro' },
      { label: 'Mixed 20 VU · Độ trễ Ghi (p95)', mono: '111,3 ms', micro: '91,3 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Độ trễ Đọc (p95)', mono: '276,7 ms', micro: '49,7 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Độ trễ Ghi (p95)', mono: '362,4 ms', micro: '187,8 ms', better: 'micro' },
      { label: 'Mixed 30 VU · Thông lượng Đọc', mono: '52,6 req/s', micro: '88,2 req/s', better: 'micro' },
    ];

    const rowH = 0.485;
    compRows.forEach((r, i) => {
      const y = ty + 0.5 + i * rowH;
      const bg = i % 2 === 1 ? theme.colors.paper : theme.colors.white;
      rect(slide, tx, y, tw, rowH, bg, theme.colors.line, false);

      text(slide, r.label, tx + 0.14, y + 0.08, 2.86, rowH - 0.16, {
        fontSize: 13, bold: true, color: theme.colors.ink,
      });

      if (r.better === 'mono') {
        addLivePill(slide, r.mono, tx + 3.04, y + 0.06, 1.28, 'E3F5EC', theme.colors.green, {
          h: rowH - 0.12, fontSize: 13, border: 'B8E2CD',
        });
      } else {
        text(slide, r.mono, tx + 3.02, y + 0.08, 1.32, rowH - 0.16, {
          fontSize: 13, align: 'center', color: theme.colors.slate, bold: r.better === 'tie',
        });
      }

      if (r.better === 'micro') {
        addLivePill(slide, r.micro, tx + 4.44, y + 0.06, 1.3, 'E3F5EC', theme.colors.green, {
          h: rowH - 0.12, fontSize: 13, border: 'B8E2CD',
        });
      } else {
        text(slide, r.micro, tx + 4.4, y + 0.08, 1.38, rowH - 0.16, {
          fontSize: 13, align: 'center', color: theme.colors.slate, bold: r.better === 'tie',
        });
      }
    });

    slide.addNotes(notesFor(15));
  }

  // =========================================================================
  // SLIDE 16 — Kết luận và hướng phát triển
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Kết luận & hướng phát triển',
      title: 'Tổng kết kết quả đạt được và hướng hoàn thiện',
      number: 16,
    });

    const cols = [
      {
        x: 0.58,
        accent: theme.colors.teal,
        title: '1 · Kết quả Kiến trúc',
        items: [
          { h: 'Phân rã 6 dịch vụ & Gateway', d: 'Tách quyền sở hữu và 6 CSDL logic' },
          { h: 'Hợp đồng OpenAPI & SAGA', d: 'SAGA đặt lịch lai & Choreography' },
          { h: 'Khung SLI / SLO / QoS', d: 'Rate limit, Idempotency, Prometheus, Jaeger' },
        ],
      },
      {
        x: 4.72,
        accent: theme.colors.navy,
        title: '2 · Kết quả Đối chiếu Tải',
        items: [
          { h: 'Ép tải đặt lịch đơn luồng', d: 'Monolith nhanh hơn (214,6 vs 387,8 ms)' },
          { h: 'Tải hỗn hợp Đọc & Ghi', d: 'Microservices đọc nhanh gấp 3,1 – 5,6 lần' },
          { h: 'Thông lượng & Độ tin cậy', d: '~29,8 lịch/giây · 100% Idempotency · 0% 5xx' },
        ],
      },
      {
        x: 8.86,
        accent: theme.colors.amber,
        title: '3 · Hướng phát triển',
        items: [
          { h: 'Khôi phục cùng Snapshot CSDL', d: 'Loại bỏ ảnh hưởng tích lũy dữ liệu' },
          { h: 'Lặp ≥ 3 lượt Tải hỗn hợp', d: 'Báo cáo trung vị và phương sai' },
          { h: 'Tách cụm CSDL vật lý độc lập', d: 'Triển khai 6 instance CSDL riêng biệt' },
        ],
      },
    ];

    cols.forEach((c) => {
      rect(slide, c.x, 1.68, 3.87, 4.98, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: c.x, y: 1.7, w: 0.08, h: 4.94,
        fill: { color: c.accent }, line: { color: c.accent, transparency: 100 },
      });
      text(slide, c.title, c.x + 0.22, 1.84, 3.48, 0.42, {
        fontSize: 19, bold: true, color: theme.colors.ink,
      });

      c.items.forEach((item, idx) => {
        const iy = 2.46 + idx * 1.32;
        rect(slide, c.x + 0.18, iy, 3.5, 1.14, theme.colors.paper, theme.colors.line);
        text(slide, item.h, c.x + 0.3, iy + 0.14, 3.26, 0.34, {
          fontSize: 16, bold: true, color: c.accent,
        });
        text(slide, item.d, c.x + 0.3, iy + 0.56, 3.26, 0.44, {
          fontSize: 14, color: theme.colors.slate, valign: 'top',
        });
      });
    });

    slide.addNotes(notesFor(16));
  }
}

module.exports = { buildLiveSlides09To16 };

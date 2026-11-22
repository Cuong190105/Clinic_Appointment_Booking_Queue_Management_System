'use strict';

const { addArrow, addCard, addPill, addSlideBase, notesFor, rect, text, theme } = require('../helpers');

function buildSlides01To05(pptx) {
  // =========================================================================
  // SLIDE 1 — Hệ thống đặt lịch khám và quản lý phòng khám
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    slide.background = { color: theme.colors.ink };

    addPill(slide, 'BÁO CÁO ĐỒ ÁN · KIẾN TRÚC PHẦN MỀM', 0.68, 0.78, 3.35, theme.colors.teal, theme.colors.white, { h: 0.32, fontSize: 10 });

    text(slide, 'Hệ thống đặt lịch khám và quản lý phòng khám', 0.68, 1.38, 7.25, 1.48, {
      fontSize: 34, bold: true, color: theme.colors.white, valign: 'top',
    });

    text(slide, 'Chuyển đổi từ Monolith sang Microservices theo miền nghiệp vụ:\nhợp đồng OpenAPI, phối hợp SAGA & sự kiện, chỉ tiêu QoS và kiểm thử tải.', 0.68, 3.05, 7.1, 0.95, {
      fontSize: 16, color: theme.colors.sky, valign: 'top',
    });

    const chips = [
      { label: '6 Microservices', w: 1.62 },
      { label: 'Hợp đồng OpenAPI', w: 1.78 },
      { label: 'SAGA & Choreography', w: 2.08 },
      { label: 'SLI / SLO / QoS', w: 1.55 },
      { label: 'Kiểm thử tải k6', w: 1.62 },
    ];
    let cx = 0.68;
    let cy = 4.45;
    chips.forEach((chip, idx) => {
      if (idx === 3) {
        cx = 0.68;
        cy = 4.9;
      }
      addPill(slide, chip.label, cx, cy, chip.w, theme.colors.navy, theme.colors.sky, {
        h: 0.34, fontSize: 10.5, border: theme.colors.teal,
      });
      cx += chip.w + 0.16;
    });

    text(slide, 'Thời lượng: 15 phút  ·  16 slide nội dung + 1 slide phụ lục tham khảo', 0.68, 5.78, 7.1, 0.32, {
      fontSize: 12.5, color: 'A9C1D9',
    });

    // Right motif panel
    rect(slide, 8.32, 0.78, 4.35, 5.65, theme.colors.navy, '2A547E');
    text(slide, 'TRỌNG TÂM BÁO CÁO', 8.62, 1.04, 3.75, 0.28, {
      fontSize: 10.5, bold: true, charSpacing: 1.1, color: theme.colors.sky,
    });

    const roadmap = [
      {
        y: 1.52,
        accent: theme.colors.amber,
        title: '1 · Hệ thống Monolith cũ',
        body: 'Một tiến trình Clinic.API · Chung ClinicDB',
      },
      {
        y: 3.12,
        accent: theme.colors.teal,
        title: '2 · Phân rã 6 Microservices',
        body: 'Gateway · CSDL logic riêng · SAGA & RabbitMQ',
      },
      {
        y: 4.72,
        accent: theme.colors.navy,
        title: '3 · Kiểm chứng & Đối chiếu tải',
        body: 'OpenAPI drift · Prometheus/Jaeger · Benchmark k6',
      },
    ];

    roadmap.forEach((node, i) => {
      rect(slide, 8.62, node.y, 3.75, 1.15, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: 8.62, y: node.y + 0.02, w: 0.08, h: 1.11,
        fill: { color: node.accent }, line: { color: node.accent, transparency: 100 },
      });
      text(slide, node.title, 8.86, node.y + 0.16, 3.35, 0.32, {
        fontSize: 14.5, bold: true, color: theme.colors.ink,
      });
      text(slide, node.body, 8.86, node.y + 0.56, 3.35, 0.42, {
        fontSize: 12, color: theme.colors.slate, valign: 'top',
      });
      if (i < roadmap.length - 1) {
        addArrow(slide, 10.49, node.y + 1.21, 10.49, node.y + 1.54, { color: theme.colors.sky, width: 1.8 });
      }
    });

    slide.addShape('line', {
      x: 0.68, y: 6.95, w: 11.99, h: 0,
      line: { color: '2A547E', width: 0.8 },
    });
    text(slide, '[S] Slide 1  ·  [P] §1–2', 0.68, 7.08, 9.0, 0.2, {
      fontSize: theme.type.footnote, color: 'A9C1D9',
    });
    text(slide, '01', 12.03, 7.08, 0.64, 0.2, {
      fontSize: theme.type.footnote, color: 'A9C1D9', align: 'right',
    });

    slide.addNotes(notesFor(1));
  }

  // =========================================================================
  // SLIDE 2 — Bài toán và các vai trò sử dụng (Reduced density, clear workflow)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Bài toán nghiệp vụ',
      title: 'Bài toán và các vai trò sử dụng trong phòng khám',
      source: '[S] Slide 2  ·  [P] §2',
      number: 2,
    });

    text(slide, 'Trách nhiệm theo vai trò', 0.58, 1.5, 5.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    const roles = [
      {
        x: 0.58,
        title: 'Bệnh nhân (Patient)',
        tag: 'ĐẶT LỊCH & THEO DÕI',
        accent: theme.colors.teal,
        body: '• Tìm bác sĩ, xem ca khám còn chỗ\n• Đặt/hủy lịch và nhận thông báo',
      },
      {
        x: 3.66,
        title: 'Lễ tân (Receptionist)',
        tag: 'TIẾP NHẬN & GỌI SỐ',
        accent: theme.colors.navy,
        body: '• Tạo lượt walk-in và làm check-in\n• Gọi số, đặt ưu tiên, thử lại (retry)',
      },
      {
        x: 6.74,
        title: 'Bác sĩ (Doctor)',
        tag: 'KHÁM & BỆNH ÁN',
        accent: theme.colors.teal,
        body: '• Điều khiển hàng đợi (queue) cá nhân\n• Khám và ghi bệnh án theo lịch hẹn',
      },
      {
        x: 9.82,
        title: 'Quản trị viên (Admin)',
        tag: 'NHÂN SỰ & LỊCH TRỰC',
        accent: theme.colors.amber,
        body: '• Quản lý tài khoản và phân quyền\n• Quản lý bác sĩ, khoa và ca làm việc',
      },
    ];

    roles.forEach((r) => {
      addCard(slide, {
        x: r.x, y: 1.86, w: 2.93, h: 1.88,
        title: r.title, body: r.body, accent: r.accent,
        titleSize: 15, bodySize: 12.6, titleHeight: 0.36,
      });
      addPill(slide, r.tag, r.x + 0.2, 3.36, 2.52, theme.colors.sky, theme.colors.navy, { h: 0.24, fontSize: 8.8 });
    });

    text(slide, 'Chuỗi quy trình nghiệp vụ xuyên qua 5 miền dữ liệu', 0.58, 4.02, 8.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    const stages = [
      { x: 0.58, step: '1 · Tìm ca trống', domain: 'Scheduling', desc: 'Xem lịch bác sĩ\n& slot khả dụng', accent: theme.colors.navy },
      { x: 3.06, step: '2 · Đặt & Giữ chỗ', domain: 'Appointment', desc: 'Giữ slot & xác nhận\nlịch hẹn khám', accent: theme.colors.teal },
      { x: 5.54, step: '3 · Check-in', domain: 'Queue', desc: 'Cấp số thứ tự\ntheo bác sĩ / ngày', accent: theme.colors.navy },
      { x: 8.02, step: '4 · Khám bệnh', domain: 'Medical Records', desc: 'Gọi khám & lưu\nhồ sơ theo lịch hẹn', accent: theme.colors.teal },
      { x: 10.5, step: '5 · Thông báo', domain: 'Notification', desc: 'Gửi thông báo\n& lưu lịch sử', accent: theme.colors.amber },
    ];

    stages.forEach((s, idx) => {
      rect(slide, s.x, 4.38, 2.24, 1.48, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: s.x, y: 4.38, w: 2.24, h: 0.07,
        fill: { color: s.accent }, line: { color: s.accent, transparency: 100 },
      });
      text(slide, s.step, s.x + 0.14, 4.52, 1.96, 0.3, {
        fontSize: 13.2, bold: true, color: theme.colors.ink,
      });
      text(slide, s.desc, s.x + 0.14, 4.88, 1.96, 0.5, {
        fontSize: 11.8, color: theme.colors.slate, valign: 'top',
      });
      addPill(slide, s.domain, s.x + 0.14, 5.5, 1.96, theme.colors.paper, s.accent, { h: 0.24, fontSize: 8.8, border: theme.colors.line });

      if (idx < stages.length - 1) {
        addArrow(slide, s.x + 2.27, 5.12, s.x + 2.45, 5.12, { color: theme.colors.teal, width: 1.6 });
      }
    });

    rect(slide, 0.58, 6.18, 12.15, 0.66, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Đặc trưng bài toán:', 0.8, 6.37, 2.0, 0.28, {
      fontSize: 13, bold: true, color: theme.colors.amber,
    });
    text(slide, 'Một lượt khám liên kết trực tiếp slot bác sĩ, lịch hẹn, hàng đợi, bệnh án và thông báo — kiến trúc phải vừa tách ranh giới miền, vừa phối hợp xuyên dịch vụ.', 2.82, 6.37, 9.7, 0.28, {
      fontSize: 12.8, color: theme.colors.ink,
    });

    slide.addNotes(notesFor(2));
  }

  // =========================================================================
  // SLIDE 3 — Hệ thống monolith cũ
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Hệ thống cũ',
      title: 'Hệ thống monolith cũ: Một ứng dụng, một CSDL dùng chung',
      source: '[P] §3.1  ·  [S] Slide 3',
      number: 3,
    });

    text(slide, 'Luồng triển khai Monolith', 0.68, 1.54, 4.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    addCard(slide, {
      x: 0.68, y: 2.46, w: 1.95, h: 1.28,
      title: 'Giao diện Web',
      body: 'Ứng dụng Vue\n(Cổng 3000)',
      accent: theme.colors.navy,
      titleSize: 15, bodySize: 12.5,
    });

    addArrow(slide, 2.68, 3.1, 3.16, 3.1);

    rect(slide, 3.22, 1.88, 5.52, 2.76, theme.colors.sky, 'B9D4DC');
    text(slide, 'Clinic.API (Cổng 5000)', 3.46, 2.04, 3.1, 0.36, {
      fontSize: 18.5, bold: true, color: theme.colors.navy,
    });
    addPill(slide, 'CÙNG TIẾN TRÌNH', 6.96, 2.08, 1.56, theme.colors.white, theme.colors.navy);

    addCard(slide, {
      x: 3.46, y: 2.58, w: 1.54, h: 1.28,
      title: 'Tầng API',
      body: 'Controllers',
      accent: theme.colors.navy,
      titleSize: 13.8, bodySize: 12,
    });
    addCard(slide, {
      x: 5.14, y: 2.58, w: 1.62, h: 1.28,
      title: 'Application',
      body: 'Xử lý nghiệp vụ',
      accent: theme.colors.teal,
      titleSize: 13.8, bodySize: 12,
    });
    addCard(slide, {
      x: 6.9, y: 2.58, w: 1.62, h: 1.28,
      title: 'Domain / Infra',
      body: 'Miền & EF Core',
      accent: theme.colors.amber,
      titleSize: 13, bodySize: 12,
    });

    text(slide, 'Gọi trực tiếp trong bộ nhớ · Dùng chung ApplicationDbContext', 3.46, 4.12, 5.2, 0.3, {
      fontSize: 11.8, color: theme.colors.navy, bold: true,
    });

    addArrow(slide, 8.8, 3.1, 9.32, 3.1);

    addCard(slide, {
      x: 9.38, y: 2.38, w: 3.25, h: 1.46,
      title: 'ClinicDB (SQL Server)',
      body: 'Một schema dùng chung\ncho toàn bộ nghiệp vụ',
      accent: theme.colors.teal,
      titleSize: 16, bodySize: 13,
    });

    text(slide, 'Ưu điểm: Gọi nội bộ nhanh  ·  Giao dịch (transaction) đa bảng đơn giản  ·  Vận hành một tiến trình', 0.72, 4.98, 11.8, 0.34, {
      fontSize: 14.5, bold: true, color: theme.colors.green,
    });

    rect(slide, 0.68, 5.54, 12.0, 0.84, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Đánh đổi:', 0.92, 5.81, 1.15, 0.3, {
      fontSize: 13.5, bold: true, color: theme.colors.amber,
    });
    text(slide, 'Quyền sở hữu dữ liệu dùng chung; ranh giới chỉ nằm ở mã nguồn nên thay đổi hoặc migration dễ ảnh hưởng nhiều miền.', 2.12, 5.81, 10.32, 0.3, {
      fontSize: 13.8, color: theme.colors.ink,
    });

    slide.addNotes(notesFor(3));
  }

  // =========================================================================
  // SLIDE 4 — Cơ sở dữ liệu monolith (Simplified conceptual ERD around Appointment)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Mô hình dữ liệu cũ',
      title: 'Cơ sở dữ liệu monolith: Bốn cụm thực thể gắn quanh lịch hẹn',
      source: '[P] §3.2  ·  [S] Slide 4',
      number: 4,
    });

    addPill(slide, 'ERD NGHIỆP VỤ KHÁI QUÁT', 10.15, 1.48, 2.58, theme.colors.sky, theme.colors.navy, { h: 0.28, fontSize: 9.5 });
    text(slide, 'Một ApplicationDbContext quản lý chung 4 cụm miền và các khóa ngoại trực tiếp', 0.58, 1.48, 8.8, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    // 4 clean domain cluster cards around a central Appointment hub
    // Top-Left: Cluster 1
    addCard(slide, {
      x: 0.58, y: 1.94, w: 4.15, h: 1.82,
      title: '1 · Tài khoản & Phân quyền',
      body: 'Thực thể chính: Person · User · Role · Permission · RefreshToken\n\nPerson là gốc nhân thân nối sang User, Employee và Patient.',
      accent: theme.colors.navy, titleSize: 15, bodySize: 12, titleHeight: 0.34,
    });

    // Top-Right: Cluster 2
    addCard(slide, {
      x: 8.58, y: 1.94, w: 4.15, h: 1.82,
      title: '2 · Nhân sự & Lịch làm việc',
      body: 'Thực thể chính: Employee · Doctor · Specialty · WorkSchedule\n\nDoctor gắn với chuyên khoa và mở các ca khám (WorkSchedule).',
      accent: theme.colors.teal, titleSize: 15, bodySize: 12, titleHeight: 0.34,
    });

    // Bottom-Left: Cluster 3
    addCard(slide, {
      x: 0.58, y: 4.14, w: 4.15, h: 1.82,
      title: '3 · Bệnh nhân & Hồ sơ y tế',
      body: 'Thực thể chính: Patient · MedicalReport · Amendment · Notification\n\nBệnh án (MedicalReport) liên kết khóa ngoại trực tiếp với lịch hẹn.',
      accent: theme.colors.amber, titleSize: 15, bodySize: 12, titleHeight: 0.34,
    });

    // Bottom-Right: Cluster 4
    addCard(slide, {
      x: 8.58, y: 4.14, w: 4.15, h: 1.82,
      title: '4 · Hàng đợi & Lịch sử lịch hẹn',
      body: 'Thực thể chính: QueueTicket · QueueCounter · AppointmentSnapshot\n\nMỗi phiếu chờ (QueueTicket) gắn 1–0..1 với một lịch hẹn khám.',
      accent: theme.colors.teal, titleSize: 15, bodySize: 12, titleHeight: 0.34,
    });

    // Center Hub: Appointment
    rect(slide, 5.18, 2.58, 2.96, 2.74, theme.colors.navy, theme.colors.navy);
    addPill(slide, 'THỰC THỂ LIÊN KẾT TRUNG TÂM', 5.42, 2.8, 2.48, theme.colors.teal, theme.colors.white, { h: 0.26, fontSize: 8.5 });
    text(slide, 'Appointment', 5.38, 3.22, 2.56, 0.42, {
      fontSize: 21, bold: true, color: theme.colors.white, align: 'center',
    });
    text(slide, 'Mốc nối trực tiếp:\n• Patient (Người đặt)\n• WorkSchedule (Ca khám)\n• QueueTicket (Phiếu chờ)\n• MedicalReport (Bệnh án)', 5.46, 3.74, 2.42, 1.38, {
      fontSize: 11.8, color: theme.colors.sky, valign: 'top',
    });

    // Arrows connecting the 4 clusters to the central hub / each other
    addArrow(slide, 2.65, 3.79, 2.65, 4.1, { color: theme.colors.navy, width: 1.6 });
    addArrow(slide, 8.54, 2.95, 8.18, 3.35, { color: theme.colors.teal, width: 1.6 });
    addArrow(slide, 4.77, 4.85, 5.14, 4.45, { color: theme.colors.amber, width: 1.6, startArrow: true });
    addArrow(slide, 8.18, 4.45, 8.54, 4.85, { color: theme.colors.teal, width: 1.6 });

    // Bottom takeaway strip
    rect(slide, 0.58, 6.22, 12.15, 0.64, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Đánh đổi mô hình cũ:', 0.8, 6.4, 2.15, 0.28, { fontSize: 13, bold: true, color: theme.colors.amber });
    text(slide, 'Truy vấn JOIN qua nhiều miền rất dễ dàng, nhưng quyền sở hữu dữ liệu không độc lập — khi tách service phải cắt khóa ngoại xuyên miền và đồng bộ bằng sự kiện / snapshot.', 2.98, 6.4, 9.55, 0.28, { fontSize: 12.6, color: theme.colors.ink });

    slide.addNotes(notesFor(4));
  }

  // =========================================================================
  // SLIDE 5 — Vì sao phân rã theo nghiệp vụ (Crisp 2-column balance)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Động lực chuyển đổi',
      title: 'Vì sao phân rã theo nghiệp vụ và những đánh đổi kiến trúc',
      source: '[P] §1.1, §4  ·  [S] Slide 5',
      number: 5,
    });

    // Left column: Benefits
    rect(slide, 0.58, 1.62, 5.55, 4.38, theme.colors.white, theme.colors.line);
    slide.addShape('rect', { x: 0.58, y: 1.62, w: 5.55, h: 0.44, fill: { color: theme.colors.teal }, line: { color: theme.colors.teal } });
    text(slide, 'Lợi ích kiến trúc hướng tới', 0.8, 1.69, 5.1, 0.3, { fontSize: 15, bold: true, color: theme.colors.white });

    const benefits = [
      {
        y: 2.26,
        title: '1 · Ranh giới sở hữu rõ ràng (Ownership)',
        body: 'Mỗi dịch vụ tự quyết định quy tắc nghiệp vụ, API và dữ liệu của miền mình.',
      },
      {
        y: 3.42,
        title: '2 · Triển khai và mở rộng độc lập theo tải',
        body: 'Nâng cấp hoặc mở rộng riêng các miền chịu tải cao (Đặt lịch, Hàng đợi).',
      },
      {
        y: 4.58,
        title: '3 · Cô lập phạm vi ảnh hưởng khi lỗi',
        body: 'Sự cố ở dịch vụ phụ trợ (như thông báo) không làm sập luồng đặt lịch chính.',
      },
    ];
    benefits.forEach((b) => {
      addCard(slide, {
        x: 0.8, y: b.y, w: 5.11, h: 0.98,
        title: b.title, body: b.body, accent: theme.colors.teal,
        fill: theme.colors.paper, titleSize: 14.2, bodySize: 12.4, titleHeight: 0.32,
      });
    });

    // Right column: Distributed systems costs
    rect(slide, 7.18, 1.62, 5.55, 4.38, theme.colors.white, theme.colors.line);
    slide.addShape('rect', { x: 7.18, y: 1.62, w: 5.55, h: 0.44, fill: { color: theme.colors.amber }, line: { color: theme.colors.amber } });
    text(slide, 'Chi phí hệ thống phân tán đi kèm', 7.4, 1.69, 5.1, 0.3, { fontSize: 15, bold: true, color: theme.colors.white });

    const costs = [
      {
        y: 2.26,
        title: '1 · Độ trễ mạng và lỗi từng phần',
        body: 'Gọi hàm nội bộ chuyển thành HTTP/Event qua mạng; dễ gặp timeout hoặc lỗi cục bộ.',
      },
      {
        y: 3.42,
        title: '2 · Nhất quán cuối cùng (Eventual Consistency)',
        body: 'Không còn ACID đa bảng; trạng thái cập nhật trễ qua Outbox, retry và bước bù.',
      },
      {
        y: 4.58,
        title: '3 · Chi phí vận hành và quan sát nhiều tiến trình',
        body: 'Phải kiểm soát lệch chuẩn OpenAPI, health check, metrics và distributed tracing.',
      },
    ];
    costs.forEach((c) => {
      addCard(slide, {
        x: 7.4, y: c.y, w: 5.11, h: 0.98,
        title: c.title, body: c.body, accent: theme.colors.amber,
        fill: theme.colors.paper, titleSize: 14.2, bodySize: 12.4, titleHeight: 0.32,
      });
    });

    slide.addShape('line', {
      x: 6.655, y: 1.72, w: 0, h: 4.18,
      line: { color: theme.colors.line, width: 1.5, dashType: 'dash' },
    });
    addPill(slide, 'ĐÁNH ĐỔI', 6.18, 3.66, 0.95, theme.colors.navy, theme.colors.white, { h: 0.34, fontSize: 9.5 });

    rect(slide, 0.58, 6.22, 12.15, 0.64, theme.colors.sky, 'B9D4DC');
    text(slide, 'Tiêu chí đánh giá:', 0.82, 6.39, 1.95, 0.28, { fontSize: 13.2, bold: true, color: theme.colors.navy });
    text(slide, 'Không đếm số lượng microservice — đồ án đánh giá bằng tính rõ ràng của ranh giới dữ liệu, hành vi khi lỗi và kết quả chịu tải thực tế.', 2.8, 6.39, 9.7, 0.28, { fontSize: 13, color: theme.colors.ink });

    slide.addNotes(notesFor(5));
  }
}

module.exports = { buildSlides01To05 };

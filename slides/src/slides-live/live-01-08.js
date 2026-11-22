'use strict';

const { addArrow, notesFor, rect, text, theme } = require('../helpers');

function addLiveBase(slide, { section, title, number }) {
  slide.background = { color: theme.colors.paper };
  text(slide, section.toUpperCase(), 0.58, 0.32, 9.5, 0.26, {
    fontSize: 12, bold: true, charSpacing: 1.2,
    color: theme.colors.teal,
  });
  text(slide, title, 0.58, 0.64, 12.15, 0.62, {
    fontSize: 30, bold: true, color: theme.colors.ink,
  });
  slide.addShape('line', {
    x: 0.58, y: 1.38, w: 12.15, h: 0,
    line: { color: theme.colors.line, width: 1.2 },
  });
  text(slide, `${String(number).padStart(2, '0')} / 16`, 11.75, 7.05, 0.98, 0.26, {
    fontSize: 12, bold: true, color: theme.colors.slate, align: 'right',
  });
}

function addLiveCard(slide, {
  x, y, w, h, title, body,
  accent = theme.colors.teal,
  fill = theme.colors.white,
  line = theme.colors.line,
  titleSize = 18.5,
  bodySize = 15.5,
  titleHeight = 0.44,
  titleColor = theme.colors.ink,
  bodyColor = theme.colors.slate,
}) {
  rect(slide, x, y, w, h, fill, line);
  slide.addShape('rect', {
    x, y: y + 0.02, w: 0.08, h: h - 0.04,
    fill: { color: accent }, line: { color: accent, transparency: 100 },
  });
  text(slide, title, x + 0.22, y + 0.14, w - 0.36, titleHeight, {
    fontSize: titleSize, bold: true, color: titleColor,
    valign: 'mid',
  });
  if (body) {
    const bodyY = y + 0.18 + titleHeight;
    text(slide, body, x + 0.22, bodyY, w - 0.36, h - (bodyY - y) - 0.14, {
      fontSize: bodySize, color: bodyColor,
      valign: 'top', breakLine: false,
    });
  }
}

function addLivePill(slide, label, x, y, w, fill, color = theme.colors.ink, opts = {}) {
  const h = opts.h || 0.36;
  slide.addShape('roundRect', {
    x, y, w, h,
    rectRadius: 0.14,
    fill: { color: fill },
    line: { color: opts.border || fill, width: opts.border ? 1 : 0, transparency: opts.border ? 0 : 100 },
  });
  text(slide, label, x + 0.08, y + 0.02, w - 0.16, h - 0.04, {
    fontSize: opts.fontSize || 12.5, color, bold: true, align: 'center',
  });
}

function buildLiveSlides01To08(pptx) {
  // =========================================================================
  // SLIDE 1 — Trang bìa
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    slide.background = { color: theme.colors.ink };

    addLivePill(slide, 'ĐỒ ÁN KIẾN TRÚC PHẦN MỀM', 0.68, 0.78, 3.45, theme.colors.teal, theme.colors.white, { h: 0.4, fontSize: 12.5 });

    text(slide, 'Hệ thống đặt lịch khám và quản lý phòng khám', 0.68, 1.45, 7.25, 1.65, {
      fontSize: 38, bold: true, color: theme.colors.white, valign: 'top',
    });

    text(slide, 'Chuyển đổi kiến trúc từ Monolith sang Microservices theo miền nghiệp vụ', 0.68, 3.25, 7.1, 0.95, {
      fontSize: 20, color: theme.colors.sky, valign: 'top',
    });

    const chips = [
      { label: '6 Microservices', w: 2.15 },
      { label: 'Hợp đồng OpenAPI', w: 2.35 },
      { label: 'SAGA & Sự kiện', w: 2.2 },
      { label: 'SLI / SLO / QoS', w: 2.15 },
      { label: 'Benchmark 12,5 CPU', w: 2.45 },
    ];
    let cx = 0.68;
    let cy = 4.55;
    chips.forEach((chip, idx) => {
      if (idx === 3) {
        cx = 0.68;
        cy = 5.12;
      }
      addLivePill(slide, chip.label, cx, cy, chip.w, theme.colors.navy, theme.colors.sky, {
        h: 0.44, fontSize: 13.5, border: theme.colors.teal,
      });
      cx += chip.w + 0.18;
    });

    // Right motif panel
    rect(slide, 8.28, 0.78, 4.4, 5.85, theme.colors.navy, '2A547E');
    text(slide, 'CẤU TRÚC TRÌNH BÀY', 8.58, 1.04, 3.8, 0.32, {
      fontSize: 13, bold: true, charSpacing: 1.1, color: theme.colors.sky,
    });

    const roadmap = [
      {
        y: 1.56,
        accent: theme.colors.amber,
        title: '1 · Hệ thống Monolith cũ',
        body: 'Clinic.API · CSDL ClinicDB chung',
      },
      {
        y: 3.18,
        accent: theme.colors.teal,
        title: '2 · Kiến trúc 6 Microservices',
        body: 'Gateway · SAGA · Sự kiện RabbitMQ',
      },
      {
        y: 4.8,
        accent: theme.colors.navy,
        title: '3 · Đo kiểm & Đối chiếu tải',
        body: 'So sánh trực diện ở 12,5 CPU',
      },
    ];

    roadmap.forEach((node, i) => {
      rect(slide, 8.58, node.y, 3.8, 1.22, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: 8.58, y: node.y + 0.02, w: 0.09, h: 1.18,
        fill: { color: node.accent }, line: { color: node.accent, transparency: 100 },
      });
      text(slide, node.title, 8.84, node.y + 0.16, 3.4, 0.38, {
        fontSize: 17.5, bold: true, color: theme.colors.ink,
      });
      text(slide, node.body, 8.84, node.y + 0.62, 3.4, 0.42, {
        fontSize: 14.5, color: theme.colors.slate, valign: 'top',
      });
      if (i < roadmap.length - 1) {
        addArrow(slide, 10.48, node.y + 1.26, 10.48, node.y + 1.58, { color: theme.colors.sky, width: 2 });
      }
    });

    text(slide, '01 / 16', 11.75, 7.05, 0.95, 0.26, {
      fontSize: 12, bold: true, color: 'A9C1D9', align: 'right',
    });

    slide.addNotes(notesFor(1));
  }

  // =========================================================================
  // SLIDE 2 — Bài toán và các vai trò sử dụng
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Bài toán nghiệp vụ',
      title: 'Bốn vai trò người dùng và chuỗi quy trình phòng khám',
      number: 2,
    });

    const roles = [
      {
        x: 0.58,
        title: 'Bệnh nhân',
        tag: 'Patient',
        accent: theme.colors.teal,
        body: 'Tìm bác sĩ & ca khám trống\nĐặt lịch & nhận thông báo',
      },
      {
        x: 3.66,
        title: 'Lễ tân',
        tag: 'Receptionist',
        accent: theme.colors.navy,
        body: 'Tiếp nhận walk-in & check-in\nGọi số, ưu tiên & thử lại',
      },
      {
        x: 6.74,
        title: 'Bác sĩ',
        tag: 'Doctor',
        accent: theme.colors.teal,
        body: 'Điều khiển hàng đợi cá nhân\nKhám & cập nhật bệnh án',
      },
      {
        x: 9.82,
        title: 'Quản trị viên',
        tag: 'Admin',
        accent: theme.colors.amber,
        body: 'Quản lý tài khoản & quyền\nQuản lý bác sĩ & lịch trực',
      },
    ];

    roles.forEach((r) => {
      addLiveCard(slide, {
        x: r.x, y: 1.72, w: 2.93, h: 2.35,
        title: r.title, body: r.body, accent: r.accent,
        titleSize: 20, bodySize: 15.5, titleHeight: 0.44,
      });
      addLivePill(slide, r.tag, r.x + 0.22, 3.52, 2.48, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 13 });
    });

    text(slide, 'Chuỗi quy trình liên thông qua 5 miền nghiệp vụ', 0.58, 4.38, 8.5, 0.34, {
      fontSize: 15, bold: true, color: theme.colors.slate,
    });

    const stages = [
      { x: 0.58, step: '1. Tìm ca trống', domain: 'Scheduling', accent: theme.colors.navy },
      { x: 3.06, step: '2. Đặt & Giữ chỗ', domain: 'Appointment', accent: theme.colors.teal },
      { x: 5.54, step: '3. Check-in', domain: 'Queue', accent: theme.colors.navy },
      { x: 8.02, step: '4. Khám bệnh', domain: 'Medical Records', accent: theme.colors.teal },
      { x: 10.5, step: '5. Thông báo', domain: 'Notification', accent: theme.colors.amber },
    ];

    stages.forEach((s, idx) => {
      rect(slide, s.x, 4.86, 2.24, 1.68, theme.colors.white, theme.colors.line);
      slide.addShape('rect', {
        x: s.x, y: 4.86, w: 2.24, h: 0.09,
        fill: { color: s.accent }, line: { color: s.accent, transparency: 100 },
      });
      text(slide, s.step, s.x + 0.14, 5.12, 1.96, 0.44, {
        fontSize: 17.5, bold: true, color: theme.colors.ink, align: 'center',
      });
      addLivePill(slide, s.domain, s.x + 0.16, 5.88, 1.92, theme.colors.sky, s.accent, {
        h: 0.42, fontSize: 13,
      });

      if (idx < stages.length - 1) {
        addArrow(slide, s.x + 2.27, 5.7, s.x + 2.45, 5.7, { color: theme.colors.teal, width: 2 });
      }
    });

    slide.addNotes(notesFor(2));
  }

  // =========================================================================
  // SLIDE 3 — Hệ thống monolith cũ
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Hệ thống cũ',
      title: 'Kiến trúc Monolith cũ: Một tiến trình, một CSDL chung',
      number: 3,
    });

    addLiveCard(slide, {
      x: 0.58, y: 2.22, w: 2.25, h: 1.76,
      title: 'Giao diện Web',
      body: 'Ứng dụng Vue\n(Frontend)',
      accent: theme.colors.navy,
      titleSize: 19.5, bodySize: 16,
    });

    addArrow(slide, 2.88, 3.1, 3.36, 3.1, { width: 2.2 });

    rect(slide, 3.42, 1.72, 5.76, 2.95, theme.colors.sky, 'B9D4DC');
    text(slide, 'Clinic.API', 3.68, 1.9, 2.8, 0.42, {
      fontSize: 23, bold: true, color: theme.colors.navy,
    });
    addLivePill(slide, 'CÙNG MỘT TIẾN TRÌNH', 6.62, 1.92, 2.32, theme.colors.white, theme.colors.navy, { h: 0.38, fontSize: 12.5 });

    addLiveCard(slide, {
      x: 3.66, y: 2.54, w: 1.64, h: 1.38,
      title: 'Tầng API',
      body: 'Controllers',
      accent: theme.colors.navy,
      titleSize: 17.5, bodySize: 15,
    });
    addLiveCard(slide, {
      x: 5.46, y: 2.54, w: 1.72, h: 1.38,
      title: 'Application',
      body: 'Nghiệp vụ',
      accent: theme.colors.teal,
      titleSize: 17.5, bodySize: 15,
    });
    addLiveCard(slide, {
      x: 7.34, y: 2.54, w: 1.62, h: 1.38,
      title: 'Domain',
      body: 'EF Core',
      accent: theme.colors.amber,
      titleSize: 17.5, bodySize: 15,
    });

    text(slide, 'Gọi trực tiếp trong bộ nhớ · Chung ApplicationDbContext', 3.66, 4.14, 5.3, 0.36, {
      fontSize: 14.5, color: theme.colors.navy, bold: true,
    });

    addArrow(slide, 9.24, 3.1, 9.72, 3.1, { width: 2.2 });

    addLiveCard(slide, {
      x: 9.78, y: 2.22, w: 2.95, h: 1.76,
      title: 'ClinicDB',
      body: 'Một CSDL SQL Server\ndùng chung mọi miền',
      accent: theme.colors.teal,
      titleSize: 20, bodySize: 16,
    });

    // Two large takeaway cards at bottom
    addLiveCard(slide, {
      x: 0.58, y: 5.1, w: 5.9, h: 1.48,
      title: 'Ưu điểm',
      body: 'Gọi nội bộ nhanh · Giao dịch ACID đa bảng đơn giản',
      accent: theme.colors.green,
      titleSize: 19, bodySize: 16,
    });

    addLiveCard(slide, {
      x: 6.84, y: 5.1, w: 5.89, h: 1.48,
      title: 'Hạn chế khi mở rộng',
      body: 'Dùng chung CSDL · Thay đổi dễ ảnh hưởng nhiều miền',
      accent: theme.colors.amber,
      titleSize: 19, bodySize: 16,
    });

    slide.addNotes(notesFor(3));
  }

  // =========================================================================
  // SLIDE 4 — Cơ sở dữ liệu monolith
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Mô hình dữ liệu cũ',
      title: 'Cơ sở dữ liệu Monolith: Bốn cụm bảng quanh Lịch hẹn',
      number: 4,
    });

    // Top-Left: Cluster 1
    addLiveCard(slide, {
      x: 0.58, y: 1.78, w: 4.18, h: 2.15,
      title: '1 · Tài khoản & Phân quyền',
      body: 'Person · User · Role · Permission\n\nPerson nối trực tiếp sang User,\nEmployee và Patient',
      accent: theme.colors.navy, titleSize: 19, bodySize: 15, titleHeight: 0.42,
    });

    // Top-Right: Cluster 2
    addLiveCard(slide, {
      x: 8.57, y: 1.78, w: 4.16, h: 2.15,
      title: '2 · Nhân sự & Lịch làm việc',
      body: 'Employee · Doctor · Specialty\nWorkSchedule · ShiftRequest\n\nBác sĩ mở các ca khám (Schedule)',
      accent: theme.colors.teal, titleSize: 19, bodySize: 15, titleHeight: 0.42,
    });

    // Bottom-Left: Cluster 3
    addLiveCard(slide, {
      x: 0.58, y: 4.38, w: 4.18, h: 2.15,
      title: '3 · Bệnh nhân & Bệnh án',
      body: 'Patient · MedicalReport\nAmendment · Notification\n\nBệnh án gắn khóa ngoại với Lịch hẹn',
      accent: theme.colors.amber, titleSize: 19, bodySize: 15, titleHeight: 0.42,
    });

    // Bottom-Right: Cluster 4
    addLiveCard(slide, {
      x: 8.57, y: 4.38, w: 4.16, h: 2.15,
      title: '4 · Hàng đợi & Lịch sử',
      body: 'QueueTicket · QueueCounter\nAppointmentSnapshot\n\nPhiếu chờ gắn 1–1 với Lịch hẹn',
      accent: theme.colors.teal, titleSize: 19, bodySize: 15, titleHeight: 0.42,
    });

    // Center Hub: Appointment
    rect(slide, 5.14, 2.38, 3.05, 3.45, theme.colors.navy, theme.colors.navy);
    addLivePill(slide, 'MỐC LIÊN KẾT TRUNG TÂM', 5.36, 2.62, 2.6, theme.colors.teal, theme.colors.white, { h: 0.36, fontSize: 11.5 });
    text(slide, 'Appointment', 5.32, 3.16, 2.68, 0.52, {
      fontSize: 25, bold: true, color: theme.colors.white, align: 'center',
    });
    text(slide, '• Patient (Bệnh nhân)\n• WorkSchedule (Ca trực)\n• QueueTicket (Phiếu chờ)\n• MedicalReport (Bệnh án)', 5.4, 3.84, 2.55, 1.75, {
      fontSize: 14.8, color: theme.colors.sky, valign: 'top',
    });

    addArrow(slide, 2.65, 3.98, 2.65, 4.32, { color: theme.colors.navy, width: 2 });
    addArrow(slide, 8.52, 2.95, 8.23, 3.35, { color: theme.colors.teal, width: 2 });
    addArrow(slide, 4.8, 5.1, 5.1, 4.65, { color: theme.colors.amber, width: 2, startArrow: true });
    addArrow(slide, 8.23, 4.65, 8.52, 5.1, { color: theme.colors.teal, width: 2 });

    slide.addNotes(notesFor(4));
  }

  // =========================================================================
  // SLIDE 5 — Vì sao phân rã theo nghiệp vụ
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Động lực chuyển đổi',
      title: 'Vì sao phân rã theo nghiệp vụ và các đánh đổi kiến trúc',
      number: 5,
    });

    // Left column: Benefits
    rect(slide, 0.58, 1.66, 5.55, 5.05, theme.colors.white, theme.colors.line);
    slide.addShape('rect', { x: 0.58, y: 1.66, w: 5.55, h: 0.54, fill: { color: theme.colors.teal }, line: { color: theme.colors.teal } });
    text(slide, 'Lợi ích hướng tới', 0.82, 1.76, 5.1, 0.36, { fontSize: 18.5, bold: true, color: theme.colors.white });

    const benefits = [
      {
        y: 2.42,
        title: '1 · Rõ quyền sở hữu (Ownership)',
        body: 'Mỗi service tự quản lý quy tắc, API và CSDL riêng',
      },
      {
        y: 3.74,
        title: '2 · Mở rộng độc lập theo tải',
        body: 'Tăng tài nguyên riêng cho miền Đặt lịch, Lịch trực',
      },
      {
        y: 5.06,
        title: '3 · Cô lập phạm vi lỗi',
        body: 'Sự cố dịch vụ phụ không làm sập luồng chính',
      },
    ];
    benefits.forEach((b) => {
      addLiveCard(slide, {
        x: 0.8, y: b.y, w: 5.11, h: 1.15,
        title: b.title, body: b.body, accent: theme.colors.teal,
        fill: theme.colors.paper, titleSize: 17.5, bodySize: 15, titleHeight: 0.38,
      });
    });

    // Right column: Costs
    rect(slide, 7.18, 1.66, 5.55, 5.05, theme.colors.white, theme.colors.line);
    slide.addShape('rect', { x: 7.18, y: 1.66, w: 5.55, h: 0.54, fill: { color: theme.colors.amber }, line: { color: theme.colors.amber } });
    text(slide, 'Chi phí hệ phân tán đi kèm', 7.42, 1.76, 5.1, 0.36, { fontSize: 18.5, bold: true, color: theme.colors.white });

    const costs = [
      {
        y: 2.42,
        title: '1 · Độ trễ giao tiếp mạng',
        body: 'Gọi hàm nội bộ chuyển thành HTTP và Event',
      },
      {
        y: 3.74,
        title: '2 · Nhất quán cuối cùng',
        body: 'Không còn ACID đa bảng; cần SAGA và Outbox',
      },
      {
        y: 5.06,
        title: '3 · Phức tạp vận hành',
        body: 'Cần kiểm soát OpenAPI, Metrics và Tracing',
      },
    ];
    costs.forEach((c) => {
      addLiveCard(slide, {
        x: 7.4, y: c.y, w: 5.11, h: 1.15,
        title: c.title, body: c.body, accent: theme.colors.amber,
        fill: theme.colors.paper, titleSize: 17.5, bodySize: 15, titleHeight: 0.38,
      });
    });

    slide.addShape('line', {
      x: 6.655, y: 1.76, w: 0, h: 4.85,
      line: { color: theme.colors.line, width: 1.8, dashType: 'dash' },
    });
    addLivePill(slide, 'ĐÁNH ĐỔI', 6.12, 3.98, 1.08, theme.colors.navy, theme.colors.white, { h: 0.42, fontSize: 12 });

    slide.addNotes(notesFor(5));
  }

  // =========================================================================
  // SLIDE 6 — Phân rã service và quyền sở hữu
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Ranh giới nghiệp vụ',
      title: 'Phân rã sáu Microservices và quyền sở hữu dữ liệu',
      number: 6,
    });

    rect(slide, 0.58, 1.56, 12.15, 0.78, theme.colors.navy, theme.colors.navy);
    text(slide, 'Clinic.API Gateway (Cổng 5000)', 0.84, 1.75, 4.2, 0.4, { fontSize: 19, bold: true, color: theme.colors.white });
    text(slide, 'Xác thực JWT  ·  Giới hạn tốc độ (Rate Limit)  ·  Định tuyến tới 6 dịch vụ', 5.15, 1.75, 7.35, 0.4, { fontSize: 15.5, color: theme.colors.sky, align: 'right' });

    const services = [
      {
        x: 0.58, y: 2.56,
        title: '1 · Identity',
        db: 'ClinicIdentityDB',
        accent: theme.colors.navy,
        body: 'Tài khoản, xác thực, cấp JWT\nVai trò (Role) & Quyền hạn',
      },
      {
        x: 4.72, y: 2.56,
        title: '2 · Staff & Scheduling',
        db: 'ClinicSchedulingDB',
        accent: theme.colors.teal,
        body: 'Nhân sự, bác sĩ, chuyên khoa\nCa làm việc & Giữ chỗ (Slot)',
      },
      {
        x: 8.86, y: 2.56,
        title: '3 · Appointment',
        db: 'ClinicAppointmentDB',
        accent: theme.colors.navy,
        body: 'Đặt, đổi, hủy lịch & Check-in\nĐiều phối SAGA & Outbox',
      },
      {
        x: 0.58, y: 4.72,
        title: '4 · Queue',
        db: 'ClinicQueueDB',
        accent: theme.colors.teal,
        body: 'Phiếu chờ & Bộ đếm theo ngày\nGọi số, ưu tiên, bỏ qua, thử lại',
      },
      {
        x: 4.72, y: 4.72,
        title: '5 · Medical Records',
        db: 'ClinicMedicalRecordsDB',
        accent: theme.colors.navy,
        body: 'Hồ sơ bệnh nhân & Ca khám\nBệnh án theo AppointmentId',
      },
      {
        x: 8.86, y: 4.72,
        title: '6 · Notification',
        db: 'ClinicNotificationDB',
        accent: theme.colors.amber,
        body: 'Hộp thư thông báo (Inbox)\nĐẩy thời gian thực qua SignalR',
      },
    ];

    services.forEach((s) => {
      addLiveCard(slide, {
        x: s.x, y: s.y, w: 3.87, h: 1.96,
        title: s.title, body: s.body, accent: s.accent,
        titleSize: 19, bodySize: 15, titleHeight: 0.42,
      });
      addLivePill(slide, s.db, s.x + 0.22, s.y + 1.44, 2.38, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });
    });

    slide.addNotes(notesFor(6));
  }

  // =========================================================================
  // SLIDE 7 — Kiến trúc tổng thể mới
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Kiến trúc tổng thể',
      title: 'Kiến trúc tổng thể mới: API đồng bộ, RabbitMQ và quan sát',
      number: 7,
    });

    addLivePill(slide, '───► Gọi HTTP đồng bộ', 7.55, 1.5, 2.48, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });
    addLivePill(slide, '- - ► Sự kiện RabbitMQ', 10.2, 1.5, 2.53, theme.colors.sky, theme.colors.teal, { h: 0.36, fontSize: 12 });

    // Left column
    addLiveCard(slide, {
      x: 0.58, y: 1.98, w: 2.35, h: 1.15,
      title: 'Người dùng',
      body: '4 vai trò phòng khám',
      accent: theme.colors.navy, titleSize: 17.5, bodySize: 14.5, titleHeight: 0.36,
    });
    addArrow(slide, 1.75, 3.16, 1.75, 3.48, { width: 2 });

    addLiveCard(slide, {
      x: 0.58, y: 3.52, w: 2.35, h: 1.15,
      title: 'Giao diện Vue',
      body: 'Gọi qua cổng Gateway',
      accent: theme.colors.teal, titleSize: 17.5, bodySize: 14.5, titleHeight: 0.36,
    });
    addArrow(slide, 1.75, 4.7, 1.75, 5.02, { width: 2 });

    rect(slide, 0.58, 5.06, 2.35, 1.65, theme.colors.navy, theme.colors.navy);
    text(slide, 'API Gateway', 0.78, 5.22, 1.98, 0.38, { fontSize: 18.5, bold: true, color: theme.colors.white });
    text(slide, 'Xác thực JWT · QoS\nĐịnh tuyến HTTP', 0.78, 5.68, 1.98, 0.85, { fontSize: 14.5, color: theme.colors.sky, valign: 'top' });

    addArrow(slide, 2.98, 5.88, 3.32, 5.88, { color: theme.colors.navy, width: 2.2 });

    // Center zone: Six domain services
    rect(slide, 3.38, 1.98, 5.52, 4.73, theme.colors.sky, 'B9D4DC');
    text(slide, '6 Dịch vụ Nghiệp vụ (Domain Services)', 3.6, 2.12, 5.1, 0.36, { fontSize: 17, bold: true, color: theme.colors.navy });

    const svcNodes = [
      { x: 3.6, y: 2.6, w: 2.42, h: 0.96, title: 'Identity', body: 'JWT & Phân quyền', accent: theme.colors.navy },
      { x: 6.26, y: 2.6, w: 2.42, h: 0.96, title: 'Scheduling', body: 'Ca trực & Giữ slot', accent: theme.colors.teal },
      { x: 3.6, y: 4.18, w: 2.42, h: 0.96, title: 'Appointment', body: 'Đặt lịch & Outbox', accent: theme.colors.navy },
      { x: 6.26, y: 4.18, w: 2.42, h: 0.96, title: 'Queue', body: 'Phiếu chờ & Gọi số', accent: theme.colors.teal },
      { x: 3.6, y: 5.52, w: 2.42, h: 0.96, title: 'Medical Records', body: 'Bệnh nhân & Bệnh án', accent: theme.colors.navy },
      { x: 6.26, y: 5.52, w: 2.42, h: 0.96, title: 'Notification', body: 'Hộp thư & SignalR', accent: theme.colors.amber },
    ];
    svcNodes.forEach((n) => {
      addLiveCard(slide, {
        x: n.x, y: n.y, w: n.w, h: n.h,
        title: n.title, body: n.body, accent: n.accent,
        titleSize: 16.5, bodySize: 13.8, titleHeight: 0.34,
      });
    });

    addLivePill(slide, 'Appointment ↔ Scheduling: HTTP giữ & xác nhận slot', 3.78, 3.68, 4.72, theme.colors.white, theme.colors.navy, {
      h: 0.36, fontSize: 12, border: theme.colors.navy,
    });

    // Right column: RabbitMQ + SQL Server + Observability
    addArrow(slide, 8.94, 2.75, 9.42, 2.75, { color: theme.colors.teal, width: 2.2, dash: 'dash', startArrow: true });
    addLiveCard(slide, {
      x: 9.48, y: 1.98, w: 3.25, h: 1.48,
      title: 'RabbitMQ Broker',
      body: 'Sự kiện miền bất đồng bộ\nOutbox & Consumer lũy đẳng',
      accent: theme.colors.teal, titleSize: 18, bodySize: 14.5, titleHeight: 0.38,
    });

    addArrow(slide, 8.94, 4.35, 9.42, 4.35, { color: theme.colors.navy, width: 2 });
    addLiveCard(slide, {
      x: 9.48, y: 3.64, w: 3.25, h: 1.48,
      title: 'SQL Server Container',
      body: '6 CSDL logic tách biệt\nTách schema & quyền sở hữu',
      accent: theme.colors.navy, titleSize: 18, bodySize: 14.5, titleHeight: 0.38,
    });

    addLiveCard(slide, {
      x: 9.48, y: 5.28, w: 3.25, h: 1.43,
      title: 'Prometheus & Jaeger',
      body: 'Thu thập Metrics, Cảnh báo\n& Truy vết phân tán (OTLP)',
      accent: theme.colors.amber, titleSize: 18, bodySize: 14.5, titleHeight: 0.38,
    });

    slide.addNotes(notesFor(7));
  }

  // =========================================================================
  // SLIDE 8 — Booking orchestration và SAGA
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addLiveBase(slide, {
      section: 'Luồng phối hợp đặt lịch',
      title: 'Booking orchestration và SAGA: Giữ chỗ và xác nhận nền',
      number: 8,
    });

    addLivePill(slide, 'GỌI API ĐỒNG BỘ', 1.65, 1.68, 2.02, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });
    addLivePill(slide, 'GIAO DỊCH CỤC BỘ', 5.62, 1.68, 2.05, theme.colors.sky, theme.colors.navy, { h: 0.36, fontSize: 12 });
    addLivePill(slide, 'OUTBOX → SỰ KIỆN RABBITMQ', 8.75, 1.68, 2.75, theme.colors.sky, theme.colors.teal, { h: 0.36, fontSize: 12 });

    const steps = [
      {
        x: 0.58,
        title: '1 · Gửi yêu cầu',
        body: 'Gateway nhận đơn\n+ Idempotency-Key',
        accent: theme.colors.navy,
      },
      {
        x: 3.06,
        title: '2 · Giữ chỗ',
        body: 'Gọi Scheduling;\ntrả 409 nếu hết slot',
        accent: theme.colors.teal,
      },
      {
        x: 5.54,
        title: '3 · Ghi cục bộ',
        body: 'Lưu lịch hẹn chờ\n& bảng Outbox',
        accent: theme.colors.navy,
      },
      {
        x: 8.02,
        title: '4 · Xác nhận',
        body: 'Worker phát event;\nchuyển Confirmed',
        accent: theme.colors.teal,
      },
      {
        x: 10.5,
        title: '5 · Thông báo',
        body: 'Notification cập nhật\nhộp thư người dùng',
        accent: theme.colors.amber,
      },
    ];

    for (const step of steps) {
      addLiveCard(slide, {
        x: step.x, y: 2.22, w: 2.22, h: 1.88,
        title: step.title, body: step.body, accent: step.accent,
        titleSize: 17.5, bodySize: 15, titleHeight: 0.4,
      });
    }

    addArrow(slide, 2.83, 3.16, 3.02, 3.16, { color: theme.colors.navy, width: 2 });
    addArrow(slide, 5.31, 3.16, 5.5, 3.16, { color: theme.colors.navy, width: 2 });
    addArrow(slide, 7.79, 3.16, 7.98, 3.16, { color: theme.colors.teal, dash: 'dash', width: 2 });
    addArrow(slide, 10.27, 3.16, 10.46, 3.16, { color: theme.colors.teal, dash: 'dash', width: 2 });

    const mechanisms = [
      {
        x: 0.58,
        title: 'Không dùng ACID đa CSDL',
        body: 'Mỗi service tự commit CSDL riêng;\nxử lý lỗi bằng retry hoặc bước bù',
        accent: theme.colors.navy,
      },
      {
        x: 4.72,
        title: 'Chặn đặt trùng & Lũy đẳng',
        body: 'Giữ chỗ đồng bộ chặn trùng slot;\nIdempotency-Key chặn request lặp',
        accent: theme.colors.teal,
      },
      {
        x: 8.86,
        title: 'Outbox chống mất sự kiện',
        body: 'Lưu sự kiện cùng giao dịch cục bộ;\nphát lại khi RabbitMQ phục hồi',
        accent: theme.colors.amber,
      },
    ];

    mechanisms.forEach((m) => {
      addLiveCard(slide, {
        x: m.x, y: 4.66, w: 3.87, h: 1.82,
        title: m.title, body: m.body, accent: m.accent,
        titleSize: 18, bodySize: 15, titleHeight: 0.42,
      });
    });

    slide.addNotes(notesFor(8));
  }
}

module.exports = { addLiveBase, addLiveCard, addLivePill, buildLiveSlides01To08 };

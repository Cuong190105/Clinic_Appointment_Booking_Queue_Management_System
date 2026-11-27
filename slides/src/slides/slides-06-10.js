'use strict';

const { addArrow, addCard, addPill, addSlideBase, notesFor, rect, text, theme } = require('../helpers');

function buildSlides06To10(pptx) {
  // =========================================================================
  // SLIDE 6 — Phân rã service và quyền sở hữu (Concise 2-line service cards)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Ranh giới nghiệp vụ',
      title: 'Phân rã sáu microservice và quyền sở hữu dữ liệu độc lập',
      source: '[P] §4  ·  [S] Slide 6',
      number: 6,
    });

    // Top Gateway banner
    rect(slide, 0.58, 1.52, 12.15, 0.72, theme.colors.navy, theme.colors.navy);
    text(slide, 'Clinic.API Gateway (Cổng 5000)', 0.82, 1.63, 3.6, 0.28, { fontSize: 15, bold: true, color: theme.colors.white });
    addPill(slide, 'ĐIỂM VÀO DUY NHẤT CỦA FRONTEND', 4.35, 1.63, 2.75, theme.colors.teal, theme.colors.white, { h: 0.26, fontSize: 8.8 });
    text(slide, 'Xác thực JWT  ·  Giới hạn tốc độ (Rate Limit)  ·  Định tuyến yêu cầu tới dịch vụ sở hữu endpoint', 0.82, 1.92, 11.5, 0.24, { fontSize: 12.2, color: theme.colors.sky });

    // 3x2 grid of 6 service ownership cards (concise 2-line descriptions)
    const services = [
      {
        x: 0.58, y: 2.42,
        title: '1 · Identity Service',
        db: 'ClinicIdentityDB',
        accent: theme.colors.navy,
        body: 'Tài khoản, thông tin xác thực (credential)\nCấp phát JWT/Refresh Token, vai trò và quyền',
      },
      {
        x: 4.72, y: 2.42,
        title: '2 · Staff & Scheduling',
        db: 'ClinicSchedulingDB',
        accent: theme.colors.teal,
        body: 'Hồ sơ nhân sự, bác sĩ, chuyên khoa\nLịch làm việc và giữ chỗ khung giờ (slot)',
      },
      {
        x: 8.86, y: 2.42,
        title: '3 · Appointment Service',
        db: 'ClinicAppointmentDB',
        accent: theme.colors.navy,
        body: 'Vòng đời đặt, đổi, hủy lịch và check-in\nĐiều phối SAGA đặt lịch, Snapshot và Outbox',
      },
      {
        x: 0.58, y: 4.32,
        title: '4 · Queue Service',
        db: 'ClinicQueueDB',
        accent: theme.colors.teal,
        body: 'Phiếu chờ (QueueTicket) và bộ đếm theo ngày\nTrạng thái gọi số, ưu tiên, bỏ qua và thử lại',
      },
      {
        x: 4.72, y: 4.32,
        title: '5 · Medical Records',
        db: 'ClinicMedicalRecordsDB',
        accent: theme.colors.navy,
        body: 'Hồ sơ bệnh nhân và ngữ cảnh khám (Encounter)\nBệnh án gắn theo AppointmentId và nhật ký',
      },
      {
        x: 8.86, y: 4.32,
        title: '6 · Notification Service',
        db: 'ClinicNotificationDB',
        accent: theme.colors.amber,
        body: 'Hộp thư thông báo (Inbox) và trạng thái gửi\nChống gửi trùng và đẩy thời gian thực (SignalR)',
      },
    ];

    services.forEach((s) => {
      addCard(slide, {
        x: s.x, y: s.y, w: 3.87, h: 1.72,
        title: s.title, body: s.body, accent: s.accent,
        titleSize: 15, bodySize: 12.4, titleHeight: 0.36,
      });
      addPill(slide, s.db, s.x + 0.2, s.y + 1.32, 1.98, theme.colors.sky, theme.colors.navy, { h: 0.26, fontSize: 9 });
    });

    // Bottom caveat strip
    rect(slide, 0.58, 6.24, 12.15, 0.62, theme.colors.amberLight, 'F1D5A1');
    text(slide, 'Mô hình dữ liệu đồ án:', 0.8, 6.4, 2.2, 0.28, { fontSize: 13, bold: true, color: theme.colors.amber });
    text(slide, '“Database per service” ở đây là tách biệt schema, quyền sở hữu logic và chuỗi kết nối; cả 6 CSDL vẫn chạy chung trên một container SQL Server.', 3.02, 6.4, 9.5, 0.28, { fontSize: 12.6, color: theme.colors.ink });

    slide.addNotes(notesFor(6));
  }

  // =========================================================================
  // SLIDE 7 — Kiến trúc tổng thể mới (Clear sync HTTP vs async RabbitMQ flows)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Kiến trúc tổng thể',
      title: 'Kiến trúc tổng thể mới: API đồng bộ, sự kiện và quan sát',
      source: '[P] §4.1  ·  [S] Slide 7',
      number: 7,
    });

    // Visual legend at top right
    addPill(slide, '───► Gọi HTTP đồng bộ', 8.05, 1.46, 2.15, theme.colors.sky, theme.colors.navy, { h: 0.26, fontSize: 9 });
    addPill(slide, '- - ► Sự kiện RabbitMQ', 10.35, 1.46, 2.38, theme.colors.sky, theme.colors.teal, { h: 0.26, fontSize: 9 });
    text(slide, 'Sơ đồ quan hệ giao tiếp logic trên môi trường Docker Compose', 0.58, 1.46, 7.2, 0.26, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    // Left column: Users -> Frontend -> Gateway
    addCard(slide, {
      x: 0.58, y: 1.86, w: 2.32, h: 0.88,
      title: 'Người dùng (4 vai trò)',
      body: 'Bệnh nhân · Lễ tân · Bác sĩ · Admin',
      accent: theme.colors.navy, titleSize: 12.6, bodySize: 10.8, titleHeight: 0.28,
    });
    addArrow(slide, 1.74, 2.77, 1.74, 3.04);

    addCard(slide, {
      x: 0.58, y: 3.08, w: 2.32, h: 0.88,
      title: 'Giao diện Web (Vue)',
      body: 'Chỉ gọi qua cổng Gateway',
      accent: theme.colors.teal, titleSize: 12.6, bodySize: 11, titleHeight: 0.28,
    });
    addArrow(slide, 1.74, 3.99, 1.74, 4.26);

    rect(slide, 0.58, 4.3, 2.32, 1.66, theme.colors.navy, theme.colors.navy);
    text(slide, 'Clinic.API Gateway', 0.74, 4.44, 2.0, 0.3, { fontSize: 14.5, bold: true, color: theme.colors.white });
    text(slide, '• Xác thực JWT\n• Giới hạn tốc độ (QoS)\n• Định tuyến HTTP tới 6 service', 0.74, 4.82, 2.0, 0.98, { fontSize: 11.4, color: theme.colors.sky, valign: 'top' });

    addArrow(slide, 2.94, 5.1, 3.3, 5.1, { color: theme.colors.navy, width: 1.8 });

    // Center zone: Six domain services
    rect(slide, 3.35, 1.86, 5.55, 4.1, theme.colors.sky, 'B9D4DC');
    text(slide, 'Sáu Dịch vụ Nghiệp vụ (Domain Services)', 3.55, 1.98, 5.15, 0.28, { fontSize: 13.5, bold: true, color: theme.colors.navy });

    const svcNodes = [
      { x: 3.55, y: 2.34, w: 2.45, h: 0.84, title: 'Identity Service', body: 'Xác thực & Phân quyền', accent: theme.colors.navy },
      { x: 6.25, y: 2.34, w: 2.45, h: 0.84, title: 'Staff & Scheduling', body: 'Nhân sự, ca trực, giữ slot', accent: theme.colors.teal },
      { x: 3.55, y: 3.68, w: 2.45, h: 0.84, title: 'Appointment Service', body: 'Đặt lịch, check-in, Outbox', accent: theme.colors.navy },
      { x: 6.25, y: 3.68, w: 2.45, h: 0.84, title: 'Queue Service', body: 'Phiếu chờ & gọi số khám', accent: theme.colors.teal },
      { x: 3.55, y: 4.92, w: 2.45, h: 0.84, title: 'Medical Records', body: 'Hồ sơ bệnh nhân & bệnh án', accent: theme.colors.navy },
      { x: 6.25, y: 4.92, w: 2.45, h: 0.84, title: 'Notification Service', body: 'Hộp thư & đẩy SignalR', accent: theme.colors.amber },
    ];
    svcNodes.forEach((n) => {
      addCard(slide, {
        x: n.x, y: n.y, w: n.w, h: n.h,
        title: n.title, body: n.body, accent: n.accent,
        titleSize: 13, bodySize: 11, titleHeight: 0.26,
      });
    });

    // Prominent sync HTTP badge between Appointment and Staff & Scheduling
    addPill(slide, 'Appointment ↔ Scheduling: Giữ & xác nhận slot (HTTP đồng bộ)', 3.92, 3.29, 4.42, theme.colors.white, theme.colors.navy, {
      h: 0.28, fontSize: 9, border: theme.colors.navy,
    });

    // Right column: RabbitMQ + SQL Server Container
    addArrow(slide, 8.94, 2.8, 9.42, 2.8, { color: theme.colors.teal, width: 1.8, dash: 'dash', startArrow: true });
    addCard(slide, {
      x: 9.48, y: 1.86, w: 3.25, h: 1.88,
      title: 'RabbitMQ Broker',
      body: '• Vận chuyển sự kiện miền bất đồng bộ\n• Phát từ Outbox, tiêu thụ lũy đẳng',
      accent: theme.colors.teal, titleSize: 15, bodySize: 12, titleHeight: 0.34,
    });

    addArrow(slide, 8.94, 4.95, 9.42, 4.95, { color: theme.colors.navy, width: 1.6 });
    addCard(slide, {
      x: 9.48, y: 3.98, w: 3.25, h: 1.98,
      title: 'SQL Server Container',
      body: '• Chứa 6 CSDL logic tách biệt\n• Tách schema & kết nối theo service\n  (Chạy chung 1 container SQL)',
      accent: theme.colors.navy, titleSize: 15, bodySize: 11.8, titleHeight: 0.34,
    });

    // Bottom Observability & Runtime bar
    rect(slide, 0.58, 6.18, 12.15, 0.68, theme.colors.white, theme.colors.line);
    addPill(slide, 'QUAN SÁT & VẬN HÀNH', 0.78, 6.38, 1.85, theme.colors.sky, theme.colors.teal, { h: 0.28, fontSize: 9 });
    text(slide, 'Prometheus (Metrics & Cảnh báo)  ·  Jaeger (Truy vết phân tán OTLP)  ·  Docker Compose (Khởi chạy trên 1 máy chủ đồ án, chưa phải cụm HA đa máy chủ).', 2.78, 6.38, 9.75, 0.28, {
      fontSize: 12.2, color: theme.colors.ink,
    });

    slide.addNotes(notesFor(7));
  }

  // =========================================================================
  // SLIDE 8 — Booking orchestration và SAGA (Clean 5-step flow + 3 compact pillars)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Luồng phối hợp đặt lịch',
      title: 'Booking orchestration và SAGA: Giữ chỗ đồng bộ, xác nhận nền',
      source: '[P] §4.2  ·  [S] Slide 8',
      number: 8,
    });

    addPill(slide, 'GỌI API ĐỒNG BỘ', 1.82, 1.72, 1.68, theme.colors.sky, theme.colors.navy);
    addPill(slide, 'GIAO DỊCH CỤC BỘ', 5.74, 1.72, 1.72, theme.colors.sky, theme.colors.navy);
    addPill(slide, 'WORKER HTTP + OUTBOX', 8.92, 1.72, 2.38, theme.colors.sky, theme.colors.teal);

    const steps = [
      {
        x: 0.58,
        title: '1 · Gửi yêu cầu',
        body: 'Gateway nhận đặt lịch\nkèm Idempotency-Key',
        accent: theme.colors.navy,
      },
      {
        x: 3.06,
        title: '2 · Giữ chỗ (Reserve)',
        body: 'Gọi Scheduling đồng bộ;\ntrả 409 ngay nếu hết slot',
        accent: theme.colors.teal,
      },
      {
        x: 5.54,
        title: '3 · Ghi cục bộ (Outbox)',
        body: 'Lưu lịch hẹn chờ + Outbox\ntrong một giao dịch cục bộ',
        accent: theme.colors.navy,
      },
      {
        x: 8.02,
        title: '4 · Xác nhận giữ chỗ',
        body: 'Worker gọi Scheduling qua HTTP;\nchuyển trạng thái sang Confirmed',
        accent: theme.colors.teal,
      },
      {
        x: 10.5,
        title: '5 · Phát thông báo',
        body: 'Notification tiêu thụ sự kiện\ncập nhật hộp thư người dùng',
        accent: theme.colors.amber,
      },
    ];

    for (const step of steps) {
      addCard(slide, {
        x: step.x, y: 2.18, w: 2.22, h: 1.62,
        title: step.title, body: step.body, accent: step.accent,
        titleSize: 12.2, bodySize: 12, titleHeight: 0.32,
      });
    }

    addArrow(slide, 2.83, 2.99, 3.02, 2.99, { color: theme.colors.navy });
    addArrow(slide, 5.31, 2.99, 5.5, 2.99, { color: theme.colors.navy });
    addArrow(slide, 7.79, 2.99, 7.98, 2.99, { color: theme.colors.teal, dash: 'dash' });
    addArrow(slide, 10.27, 2.99, 10.46, 2.99, { color: theme.colors.teal, dash: 'dash' });

    // 3 concise reliability mechanism cards instead of 2 dense paragraph blocks
    text(slide, 'Ba cơ chế bảo đảm độ tin cậy trong SAGA đặt lịch', 0.58, 4.14, 7.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    const mechanisms = [
      {
        x: 0.58,
        title: 'Không dùng ACID xuyên CSDL',
        body: 'Mỗi service tự commit CSDL riêng; tiến trình hoàn tất qua retry hoặc bước bù (compensation).',
        accent: theme.colors.navy,
      },
      {
        x: 4.72,
        title: 'Chặn đặt trùng & Lũy đẳng',
        body: 'Giữ chỗ đồng bộ ngăn 2 người lấy cùng slot; Idempotency-Key chặn request lặp tạo đơn thứ hai.',
        accent: theme.colors.teal,
      },
      {
        x: 8.86,
        title: 'Outbox chống mất sự kiện',
        body: 'Ghi Outbox chung giao dịch với Appointment để phát lại an toàn khi RabbitMQ gián đoạn.',
        accent: theme.colors.amber,
      },
    ];

    mechanisms.forEach((m) => {
      addCard(slide, {
        x: m.x, y: 4.5, w: 3.87, h: 1.38,
        title: m.title, body: m.body, accent: m.accent,
        titleSize: 14.2, bodySize: 12, titleHeight: 0.34,
      });
    });

    text(slide, 'Điều phối lai (Hybrid): Điều khiển đồng bộ bước giữ chỗ cốt lõi  ·  Phát sự kiện bất đồng bộ cho xác nhận và thông báo', 0.62, 6.22, 12.1, 0.34, {
      fontSize: 13.8, bold: true, color: theme.colors.teal,
    });

    slide.addNotes(notesFor(8));
  }

  // =========================================================================
  // SLIDE 9 — Check-in, Queue và choreography (Simplified flow + 3 safeguard cards)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Luồng hàng đợi & sự kiện',
      title: 'Check-in, Queue và choreography: Phối hợp hướng sự kiện',
      source: '[P] §4.2  ·  [S] Slide 9',
      number: 9,
    });

    addPill(slide, 'Sự kiện: AppointmentCheckedIn', 2.48, 1.66, 2.25, theme.colors.sky, theme.colors.teal, { h: 0.26, fontSize: 8.8 });
    addPill(slide, 'Sự kiện: PatientCalled / Exam*', 8.52, 1.66, 2.18, theme.colors.sky, theme.colors.teal, { h: 0.26, fontSize: 8.8 });

    // Step 1
    addCard(slide, {
      x: 0.58, y: 2.04, w: 2.65, h: 1.78,
      title: '1 · Appointment Service',
      body: '• Lễ tân walk-in / check-in\n• Nguồn sự thật về lịch hẹn\n• Ghi trạng thái & Outbox',
      accent: theme.colors.navy, titleSize: 13.8, bodySize: 11.8, titleHeight: 0.32,
    });

    addArrow(slide, 3.27, 2.93, 3.75, 2.93, { color: theme.colors.teal, dash: 'dash', width: 1.8 });

    // Step 2
    addCard(slide, {
      x: 3.8, y: 2.04, w: 2.75, h: 1.78,
      title: '2 · Queue (Tiêu thụ)',
      body: '• Nhận sự kiện lũy đẳng\n• Tạo đúng 1 phiếu chờ\n• Cấp số theo bác sĩ / ngày',
      accent: theme.colors.teal, titleSize: 13.8, bodySize: 11.8, titleHeight: 0.32,
    });

    addArrow(slide, 6.59, 2.93, 6.95, 2.93, { color: theme.colors.navy, width: 1.6 });

    // Step 3
    addCard(slide, {
      x: 7.0, y: 2.04, w: 2.65, h: 1.78,
      title: '3 · Điều khiển lượt khám',
      body: '• Lễ tân: gọi số, ưu tiên, retry\n• Bác sĩ: gọi kế tiếp, bỏ qua,\n  bắt đầu & hoàn tất khám',
      accent: theme.colors.navy, titleSize: 13.8, bodySize: 11.8, titleHeight: 0.32,
    });

    addArrow(slide, 9.69, 2.48, 10.15, 2.48, { color: theme.colors.teal, dash: 'dash', width: 1.6 });
    addArrow(slide, 9.69, 3.42, 10.15, 3.42, { color: theme.colors.teal, dash: 'dash', width: 1.6 });

    // Step 4a & 4b
    addCard(slide, {
      x: 10.2, y: 2.0, w: 2.53, h: 0.88,
      title: '4a · Medical Records',
      body: 'Mở ca khám theo AppointmentId',
      accent: theme.colors.teal, titleSize: 12.8, bodySize: 11, titleHeight: 0.26,
    });
    addCard(slide, {
      x: 10.2, y: 3.0, w: 2.53, h: 0.88,
      title: '4b · Notification',
      body: 'Thông báo gọi số & trạng thái',
      accent: theme.colors.amber, titleSize: 12.8, bodySize: 11, titleHeight: 0.26,
    });

    // 3 concise Choreography safeguards below
    text(slide, 'Ba nguyên tắc thiết kế Choreography và chốt chặn an toàn', 0.58, 4.14, 7.5, 0.28, {
      fontSize: 12.5, bold: true, color: theme.colors.slate,
    });

    const safeguards = [
      {
        x: 0.58,
        title: 'Đính kèm Snapshot tối thiểu',
        body: 'Sự kiện mang sẵn thông tin bệnh nhân/bác sĩ; Queue không phải gọi ngược sang Appointment.',
        accent: theme.colors.teal,
      },
      {
        x: 4.72,
        title: 'Chống trùng & Chốt trạng thái',
        body: 'Consumer xử lý lũy đẳng theo AppointmentId và kiểm tra state guard khi sự kiện đến sai thứ tự.',
        accent: theme.colors.navy,
      },
      {
        x: 8.86,
        title: 'Thử lại & Hàng đợi lỗi (DLQ)',
        body: 'Giới hạn số lần retry tự động và cô lập sự kiện lỗi vào Dead-Letter Queue để chẩn đoán/phát lại.',
        accent: theme.colors.amber,
      },
    ];

    safeguards.forEach((s) => {
      addCard(slide, {
        x: s.x, y: 4.5, w: 3.87, h: 1.42,
        title: s.title, body: s.body, accent: s.accent,
        titleSize: 14.2, bodySize: 12, titleHeight: 0.34,
      });
    });

    text(slide, 'Choreography giúp các miền phản ứng độc lập với sự kiện trạng thái mà không khóa chặt vào một bộ điều phối duy nhất.', 0.62, 6.24, 12.1, 0.32, {
      fontSize: 13.4, bold: true, color: theme.colors.teal,
    });

    slide.addNotes(notesFor(9));
  }

  // =========================================================================
  // SLIDE 10 — OpenAPI, kiểm thử và vận hành (Concise 4-block evidence grid)
  // =========================================================================
  {
    const slide = pptx.addSlide('BASE');
    addSlideBase(slide, {
      section: 'Hợp đồng API & vận hành',
      title: 'OpenAPI, kiểm thử tự động và công cụ quan sát vận hành',
      source: '[P] §4.3, §6.1  ·  [C]  ·  [S] Slide 10',
      number: 10,
    });

    const blocks = [
      {
        x: 0.58, y: 1.66,
        title: '1 · Hợp đồng OpenAPI & Kiểm tra lệch chuẩn',
        tag: 'tasks/contracts/ · ApiContractDriftTests',
        accent: theme.colors.navy,
        body: '• Lưu bản chụp đặc tả OpenAPI (snapshots) cho Gateway và 6 dịch vụ.\n• Tự động phát hiện khi mã nguồn thay đổi nhưng tài liệu API chưa cập nhật.',
      },
      {
        x: 6.81, y: 1.66,
        title: '2 · Kiểm thử đa tầng & Bất biến nghiệp vụ',
        tag: 'Unit · Integration · Failure · QoS',
        accent: theme.colors.teal,
        body: '• Kiểm thử luồng lỗi SAGA, gián đoạn broker, giới hạn tốc độ và Idempotency.\n• Đảm bảo bất biến: 0 đặt vượt chỗ (overbooking) và 0 phiếu hàng đợi trùng.',
      },
      {
        x: 0.58, y: 3.92,
        title: '3 · Giám sát sức khỏe & Docker Compose',
        tag: '/healthz · /readyz · Docker Compose',
        accent: theme.colors.teal,
        body: '• Công bố endpoint kiểm tra tiến trình (/healthz) và kết nối CSDL/broker (/readyz).\n• Tự động thực thi migration và khởi chạy đồng bộ toàn cụm dịch vụ.',
      },
      {
        x: 6.81, y: 3.92,
        title: '4 · Đo lường (Metrics) & Truy vết (Tracing)',
        tag: 'Prometheus · Jaeger · OTLP',
        accent: theme.colors.amber,
        body: '• Prometheus thu thập độ trễ, lưu lượng, tỷ lệ lỗi kèm bảng cảnh báo.\n• Jaeger nhận trace OTLP để theo dõi đường đi của request xuyên dịch vụ.',
      },
    ];

    blocks.forEach((b) => {
      addCard(slide, {
        x: b.x, y: b.y, w: 5.92, h: 2.02,
        title: b.title, body: b.body, accent: b.accent,
        titleSize: 15, bodySize: 12.6, titleHeight: 0.36,
      });
      addPill(slide, b.tag, b.x + 0.2, b.y + 1.62, 3.1, theme.colors.sky, theme.colors.navy, { h: 0.26, fontSize: 9 });
    });

    rect(slide, 0.58, 6.24, 12.15, 0.62, theme.colors.sky, 'B9D4DC');
    text(slide, 'Nguyên tắc nghiệm thu:', 0.82, 6.4, 2.25, 0.28, { fontSize: 13, bold: true, color: theme.colors.navy });
    text(slide, 'Kết hợp bộ kiểm thử hợp đồng/tích hợp tự động với kiểm chứng trực tiếp hành vi nghiệp vụ và quan sát runtime trên đúng commit.', 3.1, 6.4, 9.4, 0.28, { fontSize: 12.6, color: theme.colors.ink });

    slide.addNotes(notesFor(10));
  }
}

module.exports = { buildSlides06To10 };

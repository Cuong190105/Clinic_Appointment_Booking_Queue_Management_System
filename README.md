# Clinic_Appointment_Booking_Queue_Management_System

## Docs

- Identity Service setup, data import, and rollback: [identity-service-phase1.md](docs/identity-service-phase1.md)

- Đề bài, thiết kế: https://drive.google.com/drive/folders/1M1HR-tR2iFhIuq96xjhPAg8dM6SYoex-?usp=drive_link
- User story (trello): https://trello.com/invite/b/6acc3c87026b4f10a3060506/ATTI079a5560549b8093901f179a7939dc21E6625052/copy-of-clinic-booking-management
- Phân tích hệ thống hiện tại + phương hướng thiết kế: [microservices_analysis.md](microservices_analysis.md)

## Setup

Sử dụng docker

Tại thư mục gốc, chạy docker compose:

```
docker compose up -d --build
```

Before starting Compose, copy `.env.example` to `.env` and set `DB_SA_PASSWORD`,
`RABBITMQ_PASSWORD`, `JWT_KEY`, `IDENTITY_SERVICE_TOKEN`, `SMTP_USERNAME`, and
`SMTP_PASSWORD`. For Gmail, `SMTP_PASSWORD` must be a Gmail App Password, not
the normal account password. Email delivery uses the configured SMTP provider;
SMS is intentionally left as a mock provider and does not send real messages.

## Sử dụng app

Trong db đã có sẵn một số người dùng mẫu, tài khoản có dạng role + STT, ví dụ admin1, doctor2,...
Mật khẩu mặc định đều là Password123!

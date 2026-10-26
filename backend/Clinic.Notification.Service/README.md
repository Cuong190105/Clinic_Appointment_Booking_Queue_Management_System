# Clinic.Notification.Service scaffold

This project establishes the Phase 6 service boundary only. It exposes process health/readiness endpoints and is deliberately not wired to persistence, RabbitMQ consumers, providers, Gateway routes, or SignalR yet.

Planned folders: `Auth`, `Contracts`, `Endpoints`, `Entities`, `Messaging`, `Persistence`, `Realtime`, `Services`, and `Templates`. Add production behavior alongside the corresponding tests in `Clinic.Notification.Service.Tests` according to `tasks/PHASE_6_NOTIFICATION_SERVICE_EXECUTION_PLAN_MEMBER_A.md`.

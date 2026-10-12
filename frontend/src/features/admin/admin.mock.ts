// src/features/admin/admin.mock.ts

// ================================
// TYPES
// ================================

export type NavId =
  | 'overview'
  | 'accounts'
  | 'doctors'
  | 'specialties'
  | 'schedule'
  | 'reports'
  | 'settings'

export type ApptStatus =
  | 'completed'
  | 'waiting'
  | 'cancelled'
  | 'absent'

export type AccountRole =
  | 'patient'
  | 'doctor'
  | 'receptionist'
  | 'admin'

// ================================
// DASHBOARD
// ================================

export const WEEK_DATA = [
  { day: 'T2', value: 42 },
  { day: 'T3', value: 38 },
  { day: 'T4', value: 51 },
  { day: 'T5', value: 47 },
  { day: 'T6', value: 55 },
  { day: 'T7', value: 28 },
  { day: 'CN', value: 12 },
]

export const MONTH_DATA = [
  { day: 'T1', value: 420 },
  { day: 'T2', value: 380 },
  { day: 'T3', value: 450 },
  { day: 'T4', value: 480 },
  { day: 'T5', value: 510 },
  { day: 'T6', value: 360 },
  { day: 'T7', value: 285 },
  { day: 'T8', value: 490 },
  { day: 'T9', value: 465 },
  { day: 'T10', value: 425 },
  { day: 'T11', value: 385 },
  { day: 'T12', value: 540 },
]

export const DONUT_DATA = [
  {
    label: 'Hoàn thành',
    value: 215,
    color: '#00A878',
  },
  {
    label: 'Đang chờ',
    value: 87,
    color: '#0E4D92',
  },
  {
    label: 'Đã hủy',
    value: 34,
    color: '#EF4444',
  },
  {
    label: 'Không đến',
    value: 18,
    color: '#F59E0B',
  },
]

// ================================
// APPOINTMENTS
// ================================

export const APPOINTMENTS = [
  {
    id: 1,
    patient: 'Nguyễn Văn An',
    doctor: 'BS. Phạm Minh Tuấn',
    specialty: 'Tim mạch',
    datetime: '11/08/2026 08:00',
    status: 'completed' as ApptStatus,
  },
  {
    id: 2,
    patient: 'Trần Thị Bích',
    doctor: 'BS. Lê Thu Hằng',
    specialty: 'Nội khoa',
    datetime: '11/08/2026 08:30',
    status: 'waiting' as ApptStatus,
  },
  {
    id: 3,
    patient: 'Lê Văn Cường',
    doctor: 'BS. Nguyễn Sơn',
    specialty: 'Ngoại khoa',
    datetime: '11/08/2026 09:00',
    status: 'cancelled' as ApptStatus,
  },
  {
    id: 4,
    patient: 'Phạm Thị Dung',
    doctor: 'BS. Hoàng Mai',
    specialty: 'Da liễu',
    datetime: '11/08/2026 09:30',
    status: 'waiting' as ApptStatus,
  },
  {
    id: 5,
    patient: 'Hoàng Văn Em',
    doctor: 'BS. Phạm Minh Tuấn',
    specialty: 'Tim mạch',
    datetime: '11/08/2026 10:00',
    status: 'absent' as ApptStatus,
  },
  {
    id: 6,
    patient: 'Vũ Thị Phương',
    doctor: 'BS. Lê Thu Hằng',
    specialty: 'Nội khoa',
    datetime: '11/08/2026 10:30',
    status: 'completed' as ApptStatus,
  },
  {
    id: 7,
    patient: 'Đặng Văn Giang',
    doctor: 'BS. Trương Dũng',
    specialty: 'Thần kinh',
    datetime: '11/08/2026 11:00',
    status: 'waiting' as ApptStatus,
  },
  {
    id: 8,
    patient: 'Bùi Thị Hoa',
    doctor: 'BS. Nguyễn Sơn',
    specialty: 'Ngoại khoa',
    datetime: '11/08/2026 11:30',
    status: 'completed' as ApptStatus,
  },
  {
    id: 9,
    patient: 'Ngô Văn Ích',
    doctor: 'BS. Hoàng Mai',
    specialty: 'Da liễu',
    datetime: '11/08/2026 13:00',
    status: 'waiting' as ApptStatus,
  },
  {
    id: 10,
    patient: 'Tô Thị Kim',
    doctor: 'BS. Trương Dũng',
    specialty: 'Thần kinh',
    datetime: '11/08/2026 13:30',
    status: 'completed' as ApptStatus,
  },
  {
    id: 11,
    patient: 'Lý Văn Long',
    doctor: 'BS. Phạm Minh Tuấn',
    specialty: 'Tim mạch',
    datetime: '11/08/2026 14:00',
    status: 'cancelled' as ApptStatus,
  },
  {
    id: 12,
    patient: 'Mai Thị Minh',
    doctor: 'BS. Lê Thu Hằng',
    specialty: 'Nội khoa',
    datetime: '11/08/2026 14:30',
    status: 'waiting' as ApptStatus,
  },
  {
    id: 13,
    patient: 'Đinh Văn Nam',
    doctor: 'BS. Nguyễn Sơn',
    specialty: 'Ngoại khoa',
    datetime: '11/08/2026 15:00',
    status: 'completed' as ApptStatus,
  },
  {
    id: 14,
    patient: 'Cao Thị Oanh',
    doctor: 'BS. Hoàng Mai',
    specialty: 'Da liễu',
    datetime: '11/08/2026 15:30',
    status: 'absent' as ApptStatus,
  },
  {
    id: 15,
    patient: 'Phan Văn Phúc',
    doctor: 'BS. Trương Dũng',
    specialty: 'Thần kinh',
    datetime: '11/08/2026 16:00',
    status: 'waiting' as ApptStatus,
  },
]

// ================================
// DOCTORS
// ================================

export const DOCTORS = [
  {
    id: 1,
    name: 'BS. Phạm Minh Tuấn',
    specialty: 'Tim mạch',
    room: 'P.101',
    schedule: 'T2, T4, T6',
    patients: 38,
    status: 'active' as const,
  },
  {
    id: 2,
    name: 'BS. Lê Thu Hằng',
    specialty: 'Nội khoa',
    room: 'P.102',
    schedule: 'T2, T3, T5',
    patients: 42,
    status: 'active' as const,
  },
  {
    id: 3,
    name: 'BS. Nguyễn Sơn',
    specialty: 'Ngoại khoa',
    room: 'P.201',
    schedule: 'T3, T5, T7',
    patients: 29,
    status: 'active' as const,
  },
  {
    id: 4,
    name: 'BS. Hoàng Mai',
    specialty: 'Da liễu',
    room: 'P.103',
    schedule: 'T2, T4, T6',
    patients: 35,
    status: 'active' as const,
  },
  {
    id: 5,
    name: 'BS. Trương Dũng',
    specialty: 'Thần kinh',
    room: 'P.202',
    schedule: 'T3, T5',
    patients: 22,
    status: 'active' as const,
  },
  {
    id: 6,
    name: 'BS. Bùi Lan Anh',
    specialty: 'Nhi khoa',
    room: 'P.104',
    schedule: 'T2, T3, T4, T5',
    patients: 48,
    status: 'active' as const,
  },
  {
    id: 7,
    name: 'BS. Đinh Quốc Hùng',
    specialty: 'Chỉnh hình',
    room: 'P.301',
    schedule: 'T2, T5',
    patients: 18,
    status: 'inactive' as const,
  },
  {
    id: 8,
    name: 'BS. Ngô Thị Hương',
    specialty: 'Sản phụ khoa',
    room: 'P.105',
    schedule: 'T4, T6',
    patients: 31,
    status: 'active' as const,
  },
]

// ================================
// ACCOUNTS
// ================================

export const ACCOUNTS = [
  {
    id: 1,
    name: 'Nguyễn Văn An',
    email: 'nguyenvan.an@email.com',
    role: 'patient' as AccountRole,
    status: 'active' as const,
    created: '15/01/2024',
  },
  {
    id: 2,
    name: 'Trần Thị Bích',
    email: 'tran.bich@email.com',
    role: 'patient' as AccountRole,
    status: 'active' as const,
    created: '18/01/2024',
  },
  {
    id: 3,
    name: 'BS. Phạm Minh Tuấn',
    email: 'bs.tuan@clinic.vn',
    role: 'doctor' as AccountRole,
    status: 'active' as const,
    created: '01/01/2024',
  },
  {
    id: 4,
    name: 'BS. Lê Thu Hằng',
    email: 'bs.hang@clinic.vn',
    role: 'doctor' as AccountRole,
    status: 'active' as const,
    created: '01/01/2024',
  },
  {
    id: 5,
    name: 'Hoàng Văn Em',
    email: 'hoang.em@email.com',
    role: 'patient' as AccountRole,
    status: 'inactive' as const,
    created: '22/02/2024',
  },
  {
    id: 6,
    name: 'Lễ tân Minh Châu',
    email: 'le.tan.chau@clinic.vn',
    role: 'receptionist' as AccountRole,
    status: 'active' as const,
    created: '05/01/2024',
  },
  {
    id: 7,
    name: 'Quản trị Hệ thống',
    email: 'admin@clinic.vn',
    role: 'admin' as AccountRole,
    status: 'active' as const,
    created: '01/01/2024',
  },
  {
    id: 8,
    name: 'Vũ Thị Phương',
    email: 'vu.phuong@email.com',
    role: 'patient' as AccountRole,
    status: 'active' as const,
    created: '10/03/2024',
  },
  {
    id: 9,
    name: 'Đặng Văn Giang',
    email: 'dang.giang@email.com',
    role: 'patient' as AccountRole,
    status: 'active' as const,
    created: '15/03/2024',
  },
  {
    id: 10,
    name: 'Lễ tân Bảo Hân',
    email: 'le.tan.han@clinic.vn',
    role: 'receptionist' as AccountRole,
    status: 'active' as const,
    created: '05/01/2024',
  },
  {
    id: 11,
    name: 'BS. Nguyễn Sơn',
    email: 'bs.son@clinic.vn',
    role: 'doctor' as AccountRole,
    status: 'active' as const,
    created: '01/01/2024',
  },
  {
    id: 12,
    name: 'Bùi Thị Hoa',
    email: 'bui.hoa@email.com',
    role: 'patient' as AccountRole,
    status: 'inactive' as const,
    created: '20/04/2024',
  },
]

// ================================
// SPECIALTIES
// ================================

export const SPECIALTIES = [
  {
    id: 1,
    name: 'Tim mạch',
    code: 'TM',
    doctors: 4,
    room: 'P.101',
    status: 'active' as const,
  },
  {
    id: 2,
    name: 'Nội khoa',
    code: 'NK',
    doctors: 6,
    room: 'P.102',
    status: 'active' as const,
  },
  {
    id: 3,
    name: 'Ngoại khoa',
    code: 'NGK',
    doctors: 5,
    room: 'P.201',
    status: 'active' as const,
  },
  {
    id: 4,
    name: 'Da liễu',
    code: 'DL',
    doctors: 3,
    room: 'P.103',
    status: 'active' as const,
  },
  {
    id: 5,
    name: 'Thần kinh',
    code: 'TK',
    doctors: 4,
    room: 'P.202',
    status: 'active' as const,
  },
  {
    id: 6,
    name: 'Nhi khoa',
    code: 'NHI',
    doctors: 5,
    room: 'P.104',
    status: 'active' as const,
  },
  {
    id: 7,
    name: 'Chỉnh hình',
    code: 'CH',
    doctors: 2,
    room: 'P.301',
    status: 'inactive' as const,
  },
  {
    id: 8,
    name: 'Sản phụ khoa',
    code: 'SPK',
    doctors: 4,
    room: 'P.105',
    status: 'active' as const,
  },
]

// ================================
// SCHEDULE
// ================================

export const SCHEDULE_ROWS = [
  {
    name: 'BS. Phạm Minh Tuấn',
    spec: 'Tim mạch',
    slots: ['Sáng', null, 'Sáng', null, 'Cả ngày', null, null],
  },
  {
    name: 'BS. Lê Thu Hằng',
    spec: 'Nội khoa',
    slots: ['Chiều', 'Sáng', null, 'Chiều', null, 'Sáng', null],
  },
  {
    name: 'BS. Nguyễn Sơn',
    spec: 'Ngoại khoa',
    slots: [null, 'Cả ngày', null, 'Sáng', null, 'Chiều', null],
  },
  {
    name: 'BS. Hoàng Mai',
    spec: 'Da liễu',
    slots: ['Sáng', null, 'Chiều', null, 'Sáng', null, null],
  },
  {
    name: 'BS. Trương Dũng',
    spec: 'Thần kinh',
    slots: [null, 'Sáng', null, 'Sáng', null, null, null],
  },
  {
    name: 'BS. Bùi Lan Anh',
    spec: 'Nhi khoa',
    slots: ['Cả ngày', 'Cả ngày', 'Sáng', 'Chiều', null, null, null],
  },
]

export const DAYS = [
  'Thứ 2',
  'Thứ 3',
  'Thứ 4',
  'Thứ 5',
  'Thứ 6',
  'Thứ 7',
  'Chủ nhật',
]

// ================================
// REPORTS
// ================================

export const REPORT_SUMMARY = [
  {
    label: 'Tổng lượt khám',
    value: '1,735',
    change: '+8.4%',
  },
  {
    label: 'Lịch hẹn hoàn thành',
    value: '1,402',
    change: '+6.2%',
  },
  {
    label: 'Lịch hẹn đã hủy',
    value: '126',
    change: '-3.1%',
  },
  {
    label: 'Thời gian chờ TB',
    value: '18 phút',
    change: '-12.5%',
  },
]

export const SPEC_STATS = [
  {
    name: 'Tim mạch',
    visits: 312,
    avg: 18,
    pct: 94,
  },
  {
    name: 'Nội khoa',
    visits: 428,
    avg: 15,
    pct: 97,
  },
  {
    name: 'Ngoại khoa',
    visits: 187,
    avg: 22,
    pct: 91,
  },
  {
    name: 'Da liễu',
    visits: 256,
    avg: 12,
    pct: 96,
  },
  {
    name: 'Thần kinh',
    visits: 143,
    avg: 25,
    pct: 89,
  },
  {
    name: 'Nhi khoa',
    visits: 389,
    avg: 14,
    pct: 98,
  },
]

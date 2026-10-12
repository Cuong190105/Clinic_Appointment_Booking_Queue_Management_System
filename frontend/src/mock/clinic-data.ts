import type { AuthUser, UserRole } from '@/features/auth/auth.types'
import type {
  Appointment,
  AvailableSlot,
  Doctor,
  MedicalRecord,
  QueueStatus,
} from '@/features/patients/patient.types'

export const mockDoctors: Doctor[] = [
  {
    id: 1,
    name: 'BS. Trần Minh Hùng',
    specialty: 'Nội tổng quát',
    room: 'P.101',
  },
  {
    id: 2,
    name: 'BS. Lê Hồng Đăng',
    specialty: 'Tim mạch',
    room: 'P.205',
  },
  {
    id: 3,
    name: 'BS. Ngô Thu Hương',
    specialty: 'Nhi khoa',
    room: 'P.304',
  },
]

export const mockAvailableSlots: Record<number, AvailableSlot[]> = {
  1: [
    { id: 1, time: '08:00', available: true },
    { id: 2, time: '08:30', available: true },
    { id: 3, time: '09:00', available: false },
    { id: 4, time: '09:30', available: true },
  ],
  2: [
    { id: 1, time: '08:30', available: true },
    { id: 2, time: '09:00', available: true },
    { id: 3, time: '09:30', available: false },
  ],
  3: [
    { id: 1, time: '09:00', available: true },
    { id: 2, time: '10:00', available: true },
    { id: 3, time: '10:30', available: true },
  ],
}

export const mockAppointments: Appointment[] = [
  {
    id: 101,
    doctorId: 1,
    doctorName: 'BS. Trần Minh Hùng',
    specialty: 'Nội tổng quát',
    appointmentDate: '2026-08-20',
    appointmentTime: '08:00',
    status: 'Pending',
    queueNumber: 'A-12',
  },
  {
    id: 102,
    doctorId: 2,
    doctorName: 'BS. Lê Hồng Đăng',
    specialty: 'Tim mạch',
    appointmentDate: '2026-08-22',
    appointmentTime: '09:00',
    status: 'Confirmed',
    queueNumber: 'A-18',
  },
]

export const mockMedicalHistory: MedicalRecord[] = [
  {
    id: 1,
    examinationDate: '2026-08-11',
    doctorName: 'BS. Trần Minh Hùng',
    specialty: 'Nội tổng quát',
    diagnosis: 'Viêm họng',
    prescription: 'Paracetamol 500mg - uống sau ăn',
    note: 'Nghỉ ngơi và uống đủ nước trong 3 ngày',
  },
  {
    id: 2,
    examinationDate: '2026-07-02',
    doctorName: 'BS. Lê Hồng Đăng',
    specialty: 'Tim mạch',
    diagnosis: 'Huyết áp bình thường',
    prescription: 'Duy trì chế độ ăn uống lành mạnh',
    note: 'Tái khám sau 1 tháng',
  },
]

export const mockQueue: QueueStatus = {
  myTicket: 'A-12',
  position: 2,
  estimatedWaitMinutes: 5,
  doctorName: 'BS. Trần Minh Hùng',
  appointmentTime: '08:00',
  currentTicket: 'A-10',
  entries: [
    {
      ticket: 'A-10',
      patientName: 'Nguyễn Văn B',
      doctorId: 1,
      doctorName: 'BS. Trần Minh Hùng',
      appointmentTime: '08:00',
      status: 'Waiting',
      estimatedWaitMinutes: 5,
      position: 1,
      urgent: false,
    },
    {
      ticket: 'A-12',
      patientName: 'Nguyễn Văn A',
      doctorId: 1,
      doctorName: 'BS. Trần Minh Hùng',
      appointmentTime: '08:00',
      status: 'Waiting',
      estimatedWaitMinutes: 5,
      position: 2,
      urgent: false,
    },
    {
      ticket: 'A-13',
      patientName: 'Nguyễn Văn C',
      doctorId: 1,
      doctorName: 'BS. Trần Minh Hùng',
      appointmentTime: '08:00',
      status: 'Waiting',
      estimatedWaitMinutes: 15,
      position: 3,
      urgent: false,
    },
  ],
}

export const mockUsers: Record<string, AuthUser> = {
  'patient@clinic.com': {
    id: 1,
    name: 'Nguyễn Văn A',
    email: 'patient@clinic.com',
    role: 'Patient',
    roles: ['Patient'],
    activeRole: 'Patient',
  },
  'doctor@clinic.com': {
    id: 2,
    name: 'BS. Trần Minh Hùng',
    email: 'doctor@clinic.com',
    role: 'Doctor',
    roles: ['Doctor', 'Receptionist'],
    activeRole: 'Doctor',
  },
  'admin@clinic.com': {
    id: 3,
    name: 'Quản trị viên',
    email: 'admin@clinic.com',
    role: 'Admin',
    roles: ['Admin'],
    activeRole: 'Admin',
  },
}

// Track the current session email for mock getMe/register/login flows
let currentMockSessionEmail: string | null = null

export function mockLogin(email: string, password: string, role?: UserRole) {
  const normalizedEmail = email.trim().toLowerCase()
  const user = mockUsers[normalizedEmail]

  if (!user || password !== '123456') {
    const error: any = new Error('Email hoặc mật khẩu không đúng')
    error.response = { data: { message: 'Email hoặc mật khẩu không đúng' } }
    throw error
  }

  const userRoles: UserRole[] = user.roles ?? [user.role ?? 'Patient']
  const activeRole =
    role && userRoles.includes(role) ? role : user.activeRole ?? user.role ?? userRoles[0]

  const safeUser: AuthUser = {
    ...user,
    role: activeRole,
    activeRole,
    roles: userRoles,
  }

  // mark this user as the current mock session
  currentMockSessionEmail = normalizedEmail

  return {
    accessToken: `mock-access-${safeUser.id}`,
    refreshToken: `mock-refresh-${safeUser.id}`,
    user: safeUser,
  }
}

export function mockRegister(payload: { fullName: string; email: string; password: string; phoneNumber?: string; gender?: string; dateOfBirth?: string }) {
  const normalizedEmail = payload.email.trim().toLowerCase()
  if (mockUsers[normalizedEmail]) {
    const error: any = new Error('Email đã tồn tại')
    error.response = { data: { message: 'Email đã tồn tại' } }
    throw error
  }

  const maxId = Object.values(mockUsers).reduce((m, u) => {
    const uid = Number(String((u as any).id))
    return Number.isFinite(uid) ? Math.max(m, uid) : m
  }, 0)
  const newId = maxId + 1
  const newUser: AuthUser = {
    id: newId,
    name: payload.fullName,
    email: normalizedEmail,
    role: 'Patient',
    roles: ['Patient'],
    activeRole: 'Patient',
  }

  mockUsers[normalizedEmail] = newUser
  currentMockSessionEmail = normalizedEmail

  return {
    accessToken: `mock-access-${newUser.id}`,
    refreshToken: `mock-refresh-${newUser.id}`,
    user: newUser,
  }
}

export function mockGetMe(): AuthUser {
  const email = currentMockSessionEmail ?? 'patient@clinic.com'
  const current = mockUsers[email] ?? mockUsers['patient@clinic.com'] ?? {
    id: 1,
    name: 'Nguyễn Văn A',
    email: 'patient@clinic.com',
    role: 'Patient',
    roles: ['Patient'],
    activeRole: 'Patient',
  }

  return {
    ...current,
    roles: current.roles ?? [current.role ?? 'Patient'],
    activeRole: current.activeRole ?? current.role ?? 'Patient',
  }
}

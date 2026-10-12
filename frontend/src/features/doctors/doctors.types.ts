export type DoctorStatus = 'Active' | 'Inactive'
export type Gender = 'Male' | 'Female' | 'Other'
export type DoctorGender = Gender

export interface Doctor {
  id: string
  fullName: string
  name?: string
  phoneNumber: string
  email: string
  gender: number | Gender
  currentSpecialty?: string
  specialty?: string
  specialtyId?: string
  licenseNumber: string
  qualification: string
  experienceYears: number
  biography?: string | null
  status: string | number | DoctorStatus
  room?: string
  dateOfBirth?: string
  address?: string
  avatarUrl?: string
  hireDate?: string
  rating?: number
}

/** Full doctor profile returned by /doctors/me */
export interface DoctorDetail {
  id: string
  fullName: string
  phoneNumber: string
  email: string
  gender: Gender
  licenseNumber: string
  qualification: string
  currentSpecialty: string
  experienceYears: number
  status: DoctorStatus
  biography: string
  dateOfBirth?: string
  address?: string
}

export interface Specialty {
  id: string
  name: string
}

// ──────────────────────────────────────────────
// Payloads for Admin doctor management
// ──────────────────────────────────────────────
export interface CreateDoctorPayload {
  username: string
  password?: string
  fullName: string
  phoneNumber: string
  email: string
  dateOfBirth: string
  gender: number
  address: string
  hireDate?: string
  licenseNumber: string
  qualification: string
  experienceYears: number
  specialtyId: string
  biography?: string | null
  status?: number
}

export interface CreateDoctorRequest {
  hireDate: string
  licenseNumber: string
  qualification: string
  experienceYears: number
  status: DoctorStatus
  specialtyId: string
  email: string
  address: string
  username: string
  password: string
  fullName: string
  phoneNumber: string
  dateOfBirth: string
  gender: Gender
  biography: string
}

export interface UpdateDoctorPayload {
  fullName: string
  phoneNumber: string
  email: string
  dateOfBirth: string
  gender: number
  address: string
  licenseNumber: string
  qualification: string
  experienceYears: number
  specialtyId?: string
  biography?: string | null
  status?: number
}

export interface UpdateDoctorRequest {
  fullName: string
  phoneNumber: string
  email: string
  dateOfBirth: string
  gender: Gender
  address: string
  licenseNumber: string
  qualification: string
  experienceYears: number
  biography: string
}

export interface GetDoctorsParams {
  search?: string
  Search?: string
  specialtyId?: string
  SpecialtyId?: string
  status?: string | number
  Status?: DoctorStatus | string
  sortBy?: string
  SortBy?: string
  orderBy?: string
  OrderBy?: string
  gender?: number
  Qualification?: string
  pageNumber?: number
  PageNumber?: number
  pageSize?: number
  PageSize?: number
}

export interface PagedDoctorsResponse {
  items: Doctor[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

// ──────────────────────────────────────────────
// Work schedule types (Doctor Portal)
// ──────────────────────────────────────────────
export interface WorkSchedule {
  id: string
  doctorId: string
  date: string
  startTime: string
  endTime: string
  patientLimit: number
  status: string
}

export interface DoctorSchedule<T = WorkSchedule> {
  doctorId: string
  doctorName: string
  schedules: T[]
  startDate: string
  endDate: string
}

export interface RequestedShift {
  id: string
  doctorId: string
  date: string
  startTime: string
  endTime: string
  patientLimit: number
  reason?: string | null
  status: string
}

// ──────────────────────────────────────────────
// Queue types (Doctor Portal)
// ──────────────────────────────────────────────
export interface QueueTicket {
  id: string
  appointmentId: string
  queueNumber: number
  priority: boolean
  status: string
  checkInTime: string
  calledAt?: string | null
  patientName?: string | null
}

export interface SkipQueueResult {
  skippedTicket: QueueTicket
  nextCalledTicket?: QueueTicket | null
}

export interface AppointmentDetail {
  id: string
  patientId: string
  doctorId: string
  patientName: string
  doctorName: string
  timeSlot: string
  date: string
  reason: string
  status: string
  queueNumber?: string | null
  queueTime?: string | null
  medicalReport?: MedicalReport | null
  createdAt: string
}

export interface MedicalReport {
  id: string
  doctorName: string
  examDate: string
  symptoms?: string | null
  diagnosis?: string | null
  prescription?: string | null
  notes?: string | null
}

// ──────────────────────────────────────────────
// Generic API response wrapper
// ──────────────────────────────────────────────
export interface ApiResponse<T> {
  statusCode: string
  isSuccess: boolean
  errorMessages: string[]
  result: T
}

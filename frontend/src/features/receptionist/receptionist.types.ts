export type QueueStatus = 'waiting' | 'examining' | 'completed' | 'skipped' | 'cancelled'

export interface QueuePatient {
  id?: string
  appointmentId?: string
  no: string
  name: string
  doctor: string
  time: string
  status: QueueStatus
  priority?: boolean
}

export interface QueueTicketDto {
  id: string
  appointmentId: string
  queueNumber: number | string
  patientName?: string
  doctorName?: string
  priority?: boolean
  status: 'Waiting' | 'Called' | 'InProgress' | 'Completed' | 'Skipped' | 'Cancelled' | string
  checkInTime?: string | null
  calledAt?: string | null
}

export interface ReceptionistDoctor {
  id: string
  fullName: string
  roomNumber?: string
  specialtyName?: string
}

export interface PatientSearchItem {
  id: string
  fullName: string
  phoneNumber?: string | null
  email?: string | null
  insuranceNumber?: string | null
  dateOfBirth?: string | null
  gender?: string | number | null
  address?: string | null
}

export interface AppointmentItem {
  id: string
  patientId: string
  doctorId: string
  patientName?: string | null
  doctorName?: string | null
  specialtyName?: string | null
  date?: string | null
  timeSlot?: string | { hours?: number; minutes?: number; seconds?: number } | null
  reason?: string | null
  status?: string | number | null
  queueTicket?: QueueTicketDto | null
}

export interface PaginationEnvelope<T> {
  items?: T[]
  totalCount?: number
  pageNumber?: number
  pageSize?: number
  totalPages?: number
}

export interface CheckInResponse {
  id?: string
  queueNumber: number | string
  appointmentId?: string
  checkInTime?: string
}

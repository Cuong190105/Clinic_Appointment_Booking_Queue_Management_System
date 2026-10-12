export interface Doctor {
  id: string | number
  name: string
  specialty: string
  room?: string
}

export interface AvailableSlot {
  id: string | number
  workScheduleId?: string | number
  time: string
  available: boolean
}

export interface Appointment {
  id: string | number
  doctorId: string | number
  doctorName: string
  specialty: string
  appointmentDate: string
  appointmentTime: string
  status: 'Pending' | 'Confirmed' | 'CheckedIn' | 'Completed' | 'Cancelled' | string
  queueNumber?: string
}

export interface CreateAppointmentRequest {
  doctorId?: string | number
  workScheduleId?: string | number
  appointmentDate?: string
  appointmentTime?: string
  timeSlot?: string
  reason?: string
  symptoms?: string
}

export interface QueueEntry {
  ticket: string
  patientName: string
  doctorId: string | number
  doctorName: string
  appointmentTime: string
  status: 'Waiting' | 'InProgress' | 'Completed' | 'Skipped' | string
  estimatedWaitMinutes: number
  position: number
  urgent: boolean
}

export interface QueueStatus {
  myTicket: string
  position: number
  estimatedWaitMinutes: number
  doctorName: string
  appointmentTime: string
  currentTicket: string
  entries: QueueEntry[]
}

export interface MedicalRecord {
  id: string | number
  examinationDate: string
  doctorName: string
  specialty: string
  diagnosis: string
  prescription: string
  note?: string
}

export interface PatientProfile {
  id?: string | number
  fullName?: string
  email?: string
  phoneNumber?: string
  address?: string
  dateOfBirth?: string
  gender?: number | string
  insuranceNumber?: string | null
  emergencyContact?: string | null
}
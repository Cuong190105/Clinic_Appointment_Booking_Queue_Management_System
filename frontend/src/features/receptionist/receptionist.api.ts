import { http } from '@/lib/api/http'
import type {
  AppointmentItem,
  CheckInResponse,
  CreatePatientPayload,
  PaginationEnvelope,
  PatientSearchItem,
  QueueTicketDto,
  WalkInPayload,
  WalkInResult,
  WorkingDoctor,
} from './receptionist.types'

function unwrap<T>(payload: any): T {
  if (!payload || typeof payload !== 'object') {
    return payload as T
  }
  if (payload.result !== undefined) {
    return payload.result as T
  }
  if (payload.data !== undefined) {
    return payload.data as T
  }
  if (payload.items !== undefined) {
    return payload as T
  }
  return payload as T
}

export const receptionistApi = {
  // ──────────────────────────────────────────────
  // PATIENT SEARCH & MANAGEMENT
  // ──────────────────────────────────────────────

  async searchPatients(keyword: string, pageNumber = 1, pageSize = 20): Promise<PaginationEnvelope<PatientSearchItem>> {
    const res = await http.get('/patients', {
      params: {
        Search: keyword,
        pageNumber,
        pageSize,
      },
    })
    const unwrapped = unwrap<any>(res.data)
    const items: PatientSearchItem[] = Array.isArray(unwrapped)
      ? unwrapped
      : unwrapped?.items || []

    return {
      items,
      totalCount: unwrapped?.totalCount ?? items.length,
      pageNumber: unwrapped?.pageNumber ?? pageNumber,
      pageSize: unwrapped?.pageSize ?? pageSize,
    }
  },

  async createPatient(payload: CreatePatientPayload): Promise<PatientSearchItem> {
    const res = await http.post('/patients/add', payload)
    return unwrap<PatientSearchItem>(res.data)
  },

  // ──────────────────────────────────────────────
  // DOCTORS WORKING TODAY & WALK-IN BOOKING
  // ──────────────────────────────────────────────

  async getWorkingDoctors(specialtyId?: string): Promise<WorkingDoctor[]> {
    const res = await http.get('/receptionist/working-doctors', {
      params: { specialtyId: specialtyId || undefined },
    })
    const unwrapped = unwrap<any>(res.data)
    return Array.isArray(unwrapped) ? unwrapped : []
  },

  async createWalkInAppointment(payload: WalkInPayload, idempotencyKey: string): Promise<WalkInResult> {
    const res = await http.post('/receptionist/appointments/walk-in', payload, {
      headers: { 'Idempotency-Key': idempotencyKey },
    })
    return unwrap<WalkInResult>(res.data)
  },

  async createScheduledAppointment(payload: {
    patientId: string
    workScheduleId: string
    timeSlot: string
    reason: string
    isWalkIn?: boolean
  }): Promise<any> {
    const res = await http.post('/receptionist/appointments', {
      ...payload,
      isWalkIn: payload.isWalkIn ?? false,
    })
    return unwrap<any>(res.data)
  },

  // ──────────────────────────────────────────────
  // APPOINTMENTS & CHECK-IN
  // ──────────────────────────────────────────────

  async getUpcomingAppointments(patientId: string): Promise<AppointmentItem[]> {
    try {
      const res = await http.get(`/patients/${patientId}/appointments`)
      const unwrapped = unwrap<any>(res.data)
      if (Array.isArray(unwrapped)) return unwrapped
      if (Array.isArray(unwrapped?.items)) return unwrapped.items
      if (Array.isArray(unwrapped?.result)) return unwrapped.result
      return []
    } catch {
      try {
        const res = await http.get(`/patients/${patientId}/appointments/upcoming`)
        const unwrapped = unwrap<any>(res.data)
        if (Array.isArray(unwrapped)) return unwrapped
        if (Array.isArray(unwrapped?.items)) return unwrapped.items
        if (Array.isArray(unwrapped?.result)) return unwrapped.result
        return []
      } catch {
        return []
      }
    }
  },

  async checkInAppointment(appointmentId: string, isUrgent = false): Promise<CheckInResponse> {
    const res = await http.post(`/appointments/${appointmentId}/check-in`, undefined, {
      params: { isUrgent },
      headers: {
        'Idempotency-Key': crypto.randomUUID(),
      },
    })
    return unwrap<CheckInResponse>(res.data)
  },

  // ──────────────────────────────────────────────
  // APPOINTMENTS CONFIRMATION
  // ──────────────────────────────────────────────

  async getPendingAppointments(params?: {
    pageNumber?: number
    pageSize?: number
    sortBy?: string
    orderBy?: string
  }): Promise<PaginationEnvelope<AppointmentItem>> {
    const res = await http.get('/receptionist/appointments/pending', {
      params: {
        pageNumber: params?.pageNumber ?? 1,
        pageSize: params?.pageSize ?? 10,
        sortBy: params?.sortBy ?? 'date',
        orderBy: params?.orderBy ?? 'asc',
      },
    })
    const unwrapped = unwrap<any>(res.data)
    const items: AppointmentItem[] = Array.isArray(unwrapped)
      ? unwrapped
      : unwrapped?.items || []

    return {
      items,
      totalCount: unwrapped?.totalCount ?? items.length,
      pageNumber: unwrapped?.pageNumber ?? (params?.pageNumber ?? 1),
      pageSize: unwrapped?.pageSize ?? (params?.pageSize ?? 10),
      totalPages: unwrapped?.totalPages,
    }
  },

  async confirmAppointment(appointmentId: string): Promise<void> {
    await http.post(`/appointments/${appointmentId}/confirm`)
  },

  // ──────────────────────────────────────────────
  // QUEUE MANAGEMENT
  // ──────────────────────────────────────────────

  async getDoctorQueue(doctorId: string): Promise<QueueTicketDto[]> {
    const res = await http.get(`/doctors/${doctorId}/queue`)
    const unwrapped = unwrap<any>(res.data)
    const items = Array.isArray(unwrapped)
      ? unwrapped
      : unwrapped?.items || unwrapped?.data || []
    return items
  },

  async startExam(queueTicketId: string): Promise<void> {
    await http.post(`/queue/${queueTicketId}/start-exam`)
  },

  async completeExam(queueTicketId: string): Promise<void> {
    await http.post(`/queue/${queueTicketId}/complete-exam`)
  },

  async skipTicket(queueTicketId: string): Promise<void> {
    await http.post(`/queue/${queueTicketId}/skip`)
  },

  async setPriority(queueTicketId: string, priority: boolean): Promise<void> {
    await http.patch(`/queue/${queueTicketId}/priority`, { priority })
  },

  async getQueueTicketByAppointmentId(appointmentId: string): Promise<QueueTicketDto | null> {
    try {
      const res = await http.get(`/queue/appointment/${appointmentId}`)
      return unwrap<QueueTicketDto>(res.data)
    } catch {
      return null
    }
  },

  async pollQueueTicketByAppointmentId(
    appointmentId: string,
    maxRetries: number = 60,
    delayMs: number = 500
  ): Promise<QueueTicketDto | null> {
    for (let i = 0; i < maxRetries; i++) {
      try {
        const res = await http.get(`/queue/appointment/${appointmentId}`)
        const ticket = unwrap<QueueTicketDto>(res.data)
        if (ticket && ticket.queueNumber) {
          return ticket
        }
      } catch (error: any) {
        const status = error?.response?.status
        if (status !== 404 && status !== 502 && status !== 503 && status !== 504 && error?.response) {
          throw error
        }
      }
      if (i < maxRetries - 1) {
        await new Promise((resolve) => setTimeout(resolve, delayMs))
      }
    }
    return null
  },
}

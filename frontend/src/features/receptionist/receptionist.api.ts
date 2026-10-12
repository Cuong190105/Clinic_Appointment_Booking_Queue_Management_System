import { http } from '@/lib/api/http'
import type {
  AppointmentItem,
  CheckInResponse,
  PaginationEnvelope,
  PatientSearchItem,
  QueueTicketDto,
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
  // PATIENT SEARCH & CHECK-IN
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

  async checkInAppointment(appointmentId: string): Promise<CheckInResponse> {
    const res = await http.post(`/appointments/${appointmentId}/check-in`)
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
    await http.post(`/queue/${queueTicketId}/priority`, { priority })
  },
}

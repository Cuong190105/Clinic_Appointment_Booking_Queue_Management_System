import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { http } from '@/lib/api/http'
import { useAuthStore } from '@/stores/auth'
import { receptionistApi } from '@/features/receptionist/receptionist.api'
import type { QueuePatient, QueueStatus, QueueTicketDto } from '@/features/receptionist/receptionist.types'

function normalizeStatusText(value?: string | null): string {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .replace(/[_\s-]+/g, '')
}

function mapQueueStatus(status: string): QueueStatus {
  const normalized = normalizeStatusText(status)

  switch (normalized) {
    case 'waiting':
    case 'pending':
    case 'queued':
      return 'waiting'
    case 'called':
    case 'inprogress':
    case 'examining':
    case 'beingexamined':
      return 'examining'
    case 'completed':
    case 'done':
    case 'finished':
      return 'completed'
    case 'skipped':
      return 'skipped'
    case 'cancelled':
    case 'canceled':
      return 'cancelled'
    default:
      return 'waiting'
  }
}

function formatQueueNumber(number: number | string): string {
  if (typeof number === 'string' && number.includes('-')) return number
  return `A-${String(number).padStart(3, '0')}`
}

function formatApiTime(value?: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleTimeString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

function toQueuePatient(item: QueueTicketDto): QueuePatient {
  return {
    id: item.id,
    appointmentId: item.appointmentId,
    no: formatQueueNumber(item.queueNumber),
    name: item.patientName ?? 'Bệnh nhân',
    doctor: item.doctorName ?? 'Bác sĩ',
    time: formatApiTime(item.checkInTime),
    status: mapQueueStatus(item.status),
    priority: item.priority,
  }
}

export const useReceptionistStore = defineStore('receptionist', () => {
  const authStore = useAuthStore()

  // ================================
  // STATE
  // ================================

  const queue = ref<QueuePatient[]>([])
  const queueError = ref<string | null>(null)
  const queueLoading = ref(false)

  // ================================
  // GETTERS
  // ================================

  const waitingCount = computed(() => {
    return queue.value.filter((patient) => patient.status === 'waiting').length
  })

  const examiningCount = computed(() => {
    return queue.value.filter((patient) => patient.status === 'examining').length
  })

  const completedCount = computed(() => {
    return queue.value.filter((patient) => patient.status === 'completed').length
  })

  // ================================
  // ACTIONS
  // ================================

  async function resolveQueueDoctorId(doctorId?: string): Promise<string | undefined> {
    if (doctorId) return doctorId

    const activeRole = normalizeStatusText(authStore.user?.activeRole ?? authStore.user?.role)
    if (activeRole === 'doctor') {
      try {
        const { data } = await http.get<any>('/doctors/me')
        const id = data?.result?.id || data?.id
        if (id != null) return String(id)
      } catch {
        // Fallback below
      }
    }

    try {
      const { data } = await http.get<any>('/doctors', {
        params: { pageNumber: 1, pageSize: 20 },
      })
      const items = data?.result?.items || data?.items || []
      const firstDoctor = items.find((item: any) => item.id != null)
      return firstDoctor ? String(firstDoctor.id) : undefined
    } catch {
      return undefined
    }
  }

  async function fetchQueue(doctorId?: string) {
    queueLoading.value = true
    queueError.value = null

    try {
      const targetDoctorId = await resolveQueueDoctorId(doctorId)
      if (!targetDoctorId) {
        queue.value = []
        return
      }

      const tickets = await receptionistApi.getDoctorQueue(targetDoctorId)
      const nextQueue = tickets
        .map((item) => toQueuePatient(item))
        .sort((a, b) => {
          const numberA = Number(a.no.replace(/\D/g, '')) || 0
          const numberB = Number(b.no.replace(/\D/g, '')) || 0
          return numberA - numberB
        })

      queue.value = nextQueue
    } catch (error: any) {
      const status = error.response?.status
      if (status === 401 || status === 403) {
        queueError.value = 'Bạn không có quyền xem hàng đợi.'
      } else {
        queueError.value = error.response?.data?.message || 'Không thể tải hàng đợi từ máy chủ.'
      }
      queue.value = []
    } finally {
      queueLoading.value = false
    }
  }

  async function callPatient(no: string) {
    const patient = queue.value.find((item) => item.no === no)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.startExam(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'examining'
  }

  async function completePatient(no: string) {
    const patient = queue.value.find((item) => item.no === no)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.completeExam(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'completed'
  }

  async function skipPatient(no: string) {
    const patient = queue.value.find((item) => item.no === no)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.skipTicket(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'skipped'
  }

  return {
    queue,
    queueLoading,
    queueError,

    waitingCount,
    examiningCount,
    completedCount,

    fetchQueue,
    callPatient,
    completePatient,
    skipPatient,
  }
})

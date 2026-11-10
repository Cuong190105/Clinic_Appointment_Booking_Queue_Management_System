import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
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
      return 'called'
    case 'inprogress':
    case 'examining':
    case 'beingexamined':
      return 'inprogress'
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
  const isIsoWithoutTz = value.includes('T') && !value.endsWith('Z') && !/[+-]\d{2}(:\d{2})?$/.test(value)
  const date = new Date(isIsoWithoutTz ? `${value}Z` : value)
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
    doctorId: item.doctorId,
    no: formatQueueNumber(item.queueNumber),
    name: item.patientName ?? 'Bệnh nhân',
    doctor: item.doctorName ?? 'Bác sĩ',
    time: formatApiTime(item.checkInTime),
    status: mapQueueStatus(item.status),
    priority: item.priority,
  }
}

export const useReceptionistStore = defineStore('receptionist', () => {
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
    return queue.value.filter((patient) => patient.status === 'called' || patient.status === 'inprogress').length
  })

  const completedCount = computed(() => {
    return queue.value.filter((patient) => patient.status === 'completed').length
  })

  // ================================
  // ACTIONS
  // ================================

  async function fetchQueue(doctorId?: string) {
    queueLoading.value = true
    queueError.value = null

    try {
      const doctors = doctorId
        ? [{ doctorId }]
        : await receptionistApi.getQueueDoctors()
      const doctorQueues = await Promise.all(
        doctors.map(async (doctor) => receptionistApi.getDoctorQueue(doctor.doctorId)),
      )
      const tickets = doctorQueues.flat()
      const nextQueue = tickets
        .map((item) => toQueuePatient(item))
        .sort((a, b) => {
          const numberA = Number(a.no.replace(/\D/g, '')) || 0
          const numberB = Number(b.no.replace(/\D/g, '')) || 0
          return numberA - numberB
        })

      queue.value = nextQueue
    } catch (error: any) {
      const status = error.status ?? error.response?.status
      if (status === 401 || status === 403) {
        queueError.value = 'Bạn không có quyền xem hàng đợi.'
      } else {
        queueError.value = error.message || 'Không thể tải hàng đợi từ máy chủ.'
      }
      queue.value = []
    } finally {
      queueLoading.value = false
    }
  }

  async function callPatient(ticketId: string) {
    const patient = queue.value.find((item) => item.id === ticketId)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.callTicket(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'called'
  }

  async function skipPatient(ticketId: string) {
    const patient = queue.value.find((item) => item.id === ticketId)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.skipTicket(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'skipped'
  }

  async function retryPatient(ticketId: string) {
    const patient = queue.value.find((item) => item.id === ticketId)
    if (!patient) return

    if (patient.id) {
      await receptionistApi.retryTicket(patient.id)
      await fetchQueue()
      return
    }

    patient.status = 'waiting'
  }

  async function setPatientPriority(ticketId: string, priority: boolean) {
    const patient = queue.value.find((item) => item.id === ticketId)
    if (!patient?.id) return
    await receptionistApi.setPriority(patient.id, priority)
    await fetchQueue()
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
    skipPatient,
    retryPatient,
    setPatientPriority,
  }
})

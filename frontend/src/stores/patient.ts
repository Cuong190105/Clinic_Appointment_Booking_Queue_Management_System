import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { useAuthStore } from '@/stores/auth'
import { patientApi } from '@/features/patients/patient.api'

import type {
  Doctor,
  AvailableSlot,
  Appointment,
  CreateAppointmentRequest,
  QueueStatus,
  MedicalRecord,
} from '@/features/patients/patient.types'

export const usePatientStore = defineStore('patient', () => {
  const doctors = ref<Doctor[]>([])
  const slots = ref<AvailableSlot[]>([])
  const appointments = ref<Appointment[]>([])
  const queue = ref<QueueStatus | null>(null)
  const history = ref<MedicalRecord[]>([])

  function sortMedicalHistory(records: MedicalRecord[]) {
    return [...records].sort((a, b) => {
      const rawA = (a as any)?.examinationDate ?? (a as any)?.examDate ?? (a as any)?.date
      const rawB = (b as any)?.examinationDate ?? (b as any)?.examDate ?? (b as any)?.date

      const parseDisplayDate = (value: unknown): Date | null => {
        if (value === null || value === undefined) return null
        const s = String(value).trim()
        if (!s) return null

        const dm = /^([0-3]?\d)\/(0?[1-9]|1[0-2])\/(\d{4})$/.exec(s)
        if (dm) {
          const day = Number(dm[1])
          const month = Number(dm[2])
          const year = Number(dm[3])
          const date = new Date(year, month - 1, day)
          if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
            return null
          }
          return date
        }

        const parsed = new Date(s)
        if (!Number.isNaN(parsed.getTime())) return parsed
        return null
      }

      const dateA = parseDisplayDate(rawA)
      const dateB = parseDisplayDate(rawB)

      if (dateA && dateB) return dateB.getTime() - dateA.getTime()
      if (dateA && !dateB) return -1
      if (!dateA && dateB) return 1
      return 0
    })
  }

  const sortedHistory = computed(() => sortMedicalHistory(history.value))
  const profile = ref<import('@/features/patients/patient.types').PatientProfile | null>(null)

  // Loading riêng
  const doctorsLoading = ref(false)
  const slotsLoading = ref(false)
  const appointmentsLoading = ref(false)
  const queueLoading = ref(false)
  const historyLoading = ref(false)
  
  // Error riêng
  const doctorsError = ref<string | null>(null)
  const slotsError = ref<string | null>(null)
  const appointmentsError = ref<string | null>(null)
  const queueError = ref<string | null>(null)
  const historyError = ref<string | null>(null)
  const profileLoading = ref(false)
  const profileError = ref<string | null>(null)

  function parseDisplayDate(value: unknown): Date | null {
    if (!value && value !== 0) return null
    const s = String(value).trim()
    if (!s) return null

    const dm = /^([0-3]?\d)\/(0?[1-9]|1[0-2])\/(\d{4})$/.exec(s)
    if (dm) {
      const d = new Date(Number(dm[3]), Number(dm[2]) - 1, Number(dm[1]))
      return Number.isNaN(d.getTime()) ? null : d
    }

    const isoDateOnly = /(\d{4})-(\d{1,2})-(\d{1,2})/.exec(s)
    if (isoDateOnly) {
      const d = new Date(Number(isoDateOnly[1]), Number(isoDateOnly[2]) - 1, Number(isoDateOnly[3]))
      return Number.isNaN(d.getTime()) ? null : d
    }

    const parsed = new Date(s)
    return Number.isNaN(parsed.getTime()) ? null : parsed
  }

  function normalizeStatus(value: unknown): string {
    if (value == null) return ''
    return String(value)
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
  }

  function isConfirmedStatus(value: unknown): boolean {
    const status = normalizeStatus(value)
    return status.includes('confirm') || status.includes('xac') || status === 'confirmed'
  }

  function isPendingStatus(value: unknown): boolean {
    const status = normalizeStatus(value)
    return status.includes('pend') || status.includes('wait') || status.includes('cho') || status === 'pending'
  }

  const upcomingAppointments = computed(() =>
    appointments.value.filter(
      (a) =>
        a.status === 'Pending' ||
        a.status === 'Confirmed' ||
        a.status === 'CheckedIn',
    ),
  )

  const sortedAppointments = computed(() => {
    return [...appointments.value].sort((a, b) => {
      const da = parseDisplayDate(a.appointmentDate)
      const db = parseDisplayDate(b.appointmentDate)

      if (da && db) return da.getTime() - db.getTime()
      if (da && !db) return -1
      if (!da && db) return 1
      return 0
    })
  })

  const upcomingWeekIds = computed(() => {
    const start = new Date()
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(end.getDate() + 6)

    return appointments.value.reduce<Array<number | string>>((acc, item) => {
      const status = item.status
      if (!isConfirmedStatus(status) && !isPendingStatus(status)) return acc
      const d = parseDisplayDate(item.appointmentDate)
      if (!d) return acc
      d.setHours(0, 0, 0, 0)
      if (d >= start && d <= end) acc.push(item.id)
      return acc
    }, [])
  })

  const upcomingWeekCount = computed(() => upcomingWeekIds.value.length)

  async function loadDoctors(specialty?: string) {
    doctorsLoading.value = true
    doctorsError.value = null

    try {
      doctors.value = await patientApi.getDoctors(specialty)
    } catch (e: any) {
      doctorsError.value =
        e.response?.data?.message ||
        'Không thể tải danh sách bác sĩ'
    } finally {
      doctorsLoading.value = false
    }
  }

  async function loadSlots(doctorId: string | number, date: string) {
    slotsLoading.value = true
    slotsError.value = null

    try {
      slots.value = await patientApi.getAvailableSlots(
        doctorId,
        date,
      )
    } catch (e: any) {
      slotsError.value =
        e.response?.data?.message ||
        'Không thể tải khung giờ'
    } finally {
      slotsLoading.value = false
    }
  }

  function clearSlots() {
    slots.value = []
  }

  async function createAppointment(
    payload: CreateAppointmentRequest,
  ) {
    appointmentsLoading.value = true
    appointmentsError.value = null

    try {
      const appointment =
        await patientApi.createAppointment(payload)

      appointments.value.unshift(appointment)

      return appointment
    } catch (e: any) {
      appointmentsError.value =
        e.response?.data?.message ||
        (Array.isArray(e.response?.data?.errorMessages) && e.response.data.errorMessages.join(', ')) ||
        e.response?.data?.title ||
        'Đặt lịch thất bại'

      throw e
    } finally {
      appointmentsLoading.value = false
    }
  }

  async function loadAppointments() {
    appointmentsLoading.value = true
    appointmentsError.value = null

    try {
      const result = await patientApi.getMyAppointments()
      appointments.value = result
    } catch (e: any) {
      appointments.value = []
      appointmentsError.value =
        e.response?.data?.message ||
        (Array.isArray(e.response?.data?.errorMessages) && e.response.data.errorMessages.join(', ')) ||
        e.response?.data?.title ||
        'Không thể tải lịch hẹn'
    } finally {
      appointmentsLoading.value = false
    }
  }

  async function cancelAppointment(id: string | number) {
    appointmentsLoading.value = true
    appointmentsError.value = null

    try {
      await patientApi.cancelAppointment(id)

      const appointment = appointments.value.find(
        (a) => String(a.id) === String(id),
      )

      if (appointment) {
        appointment.status = 'Cancelled'
      }
    } catch (e: any) {
      appointmentsError.value =
        e.response?.data?.message ||
        'Không thể hủy lịch'

      throw e
    } finally {
      appointmentsLoading.value = false
    }
  }

  async function loadQueue() {
    queueLoading.value = true
    queueError.value = null

    try {
      queue.value = await patientApi.getMyQueue()
    } catch (e: any) {
      queue.value = null
      queueError.value =
        e.response?.data?.message ||
        'Không thể tải hàng đợi'
    } finally {
      queueLoading.value = false
    }
  }

  async function loadHistory() {
    historyLoading.value = true
    historyError.value = null

    try {
      const result = await patientApi.getMedicalHistory()
      history.value = sortMedicalHistory(result)
    } catch (e: any) {
      history.value = []
      historyError.value =
        e.response?.data?.message ||
        'Không thể tải lịch sử khám'
    } finally {
      historyLoading.value = false
    }
  }

  async function loadProfile() {
    profileLoading.value = true
    profileError.value = null

    try {
      const result = await patientApi.getMyProfile()
      profile.value = result

      const authStore = useAuthStore()
      if (result?.fullName && authStore.user) {
        authStore.setUser({
          ...authStore.user,
          name: result.fullName,
          email: result.email ?? authStore.user.email,
        })
      }
    } catch (e: any) {
      profile.value = null
      profileError.value = e.response?.data?.message || 'Không thể tải hồ sơ bệnh nhân'
    } finally {
      profileLoading.value = false
    }
  }

  return {
    // Data
    doctors,
    slots,
    appointments,
    upcomingAppointments,
    sortedAppointments,
    upcomingWeekIds,
    upcomingWeekCount,
    queue,
    history,
    sortedHistory,

    // Loading
    doctorsLoading,
    slotsLoading,
    appointmentsLoading,
    queueLoading,
    historyLoading,

    // Error
    doctorsError,
    slotsError,
    appointmentsError,
    queueError,
    historyError,

    // Actions
    loadDoctors,
    loadSlots,
    clearSlots,
    createAppointment,
    loadAppointments,
    cancelAppointment,
    loadQueue,
    loadHistory,
    loadProfile,

    // Profile
    profile,
    profileLoading,
    profileError,
  }
})
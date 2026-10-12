import { env } from '@/config/env'
import { http } from '@/lib/api/http'
import { logger } from '@/lib/logger'
import type {
  Doctor,
  AvailableSlot,
  Appointment,
  CreateAppointmentRequest,
  QueueStatus,
  MedicalRecord,
  PatientProfile,
} from './patient.types'

function toTimeString(value: unknown): string {
  if (!value) return ''
  if (typeof value === 'string') return value.slice(0, 5)
  if (typeof value === 'object' && value && 'hours' in (value as object)) {
    const v = value as { hours?: number; minutes?: number; seconds?: number }
    const hh = String(v.hours ?? 0).padStart(2, '0')
    const mm = String(v.minutes ?? 0).padStart(2, '0')
    return `${hh}:${mm}`
  }
  return String(value)
}

const PATIENT_PROTECTED_ENDPOINTS_DISABLED_KEY = 'clinic.patient.protected-disabled'

function isProtectedPatientEndpointsDisabled(): boolean {
  try {
    return localStorage.getItem(PATIENT_PROTECTED_ENDPOINTS_DISABLED_KEY) === '1'
  } catch {
    return false
  }
}

function disableProtectedPatientEndpoints(): void {
  try {
    localStorage.setItem(PATIENT_PROTECTED_ENDPOINTS_DISABLED_KEY, '1')
  } catch {
    // ignore
  }
}

export function enableProtectedPatientEndpoints(): void {
  try {
    localStorage.removeItem(PATIENT_PROTECTED_ENDPOINTS_DISABLED_KEY)
  } catch {
    // ignore
  }
}

export function areProtectedPatientEndpointsDisabled(): boolean {
  return isProtectedPatientEndpointsDisabled()
}

function shouldSkipProtectedPatientRequest(): boolean {
  return isProtectedPatientEndpointsDisabled()
}

function mapDoctor(raw: any): Doctor {
  const id = raw?.id ?? raw?.doctorId ?? raw?.DoctorId ?? ''
  const name = raw?.fullName ?? raw?.name ?? raw?.doctorName ?? raw?.FullName ?? 'BS. Chưa xác định'
  const specialty = raw?.currentSpecialty ?? raw?.specialty ?? raw?.CurrentSpecialty ?? raw?.Specialty ?? 'Khác'

  return {
    id: id || '0',
    name,
    specialty,
    room: raw?.room ?? raw?.roomNumber,
  }
}

function normalizeDoctorList(data: any): Doctor[] {
  if (Array.isArray(data)) return data.map((item: any) => mapDoctor(item))
  if (Array.isArray(data?.items)) return data.items.map((item: any) => mapDoctor(item))
  if (Array.isArray(data?.result?.items)) return data.result.items.map((item: any) => mapDoctor(item))
  if (Array.isArray(data?.result)) return data.result.map((item: any) => mapDoctor(item))
  if (Array.isArray(data?.data)) return data.data.map((item: any) => mapDoctor(item))
  return []
}

function formatTimeSlotForBackend(time: string): string {
  if (!time) return '08:00:00'
  const parts = time.trim().split(':')
  if (parts.length === 2) return `${parts[0]}:${parts[1]}:00`
  return time
}

function normalizeDoctorAvailableSlots(data: any): AvailableSlot[] {
  const items = Array.isArray(data) ? data : data?.items ?? data?.result ?? data?.data ?? []
  const slots: AvailableSlot[] = []

  for (const item of items) {
    const wsId = item?.id ?? item?.Id ?? item?.workScheduleId ?? item?.WorkScheduleId ?? ''
    const timeSlots = item?.timeSlot ?? item?.TimeSlot ?? item?.timeSlots ?? []

    if (Array.isArray(timeSlots) && timeSlots.length > 0) {
      for (const t of timeSlots) {
        const timeStr = toTimeString(t)
        if (timeStr) {
          slots.push({
            id: `${wsId}-${timeStr}`,
            workScheduleId: wsId,
            time: timeStr,
            available: true,
          })
        }
      }
    } else {
      const rawTime = item?.shiftStart ?? item?.ShiftStart ?? item?.time ?? item?.startTime ?? item?.slotTime
      if (rawTime) {
        slots.push({
          id: wsId,
          workScheduleId: wsId,
          time: toTimeString(rawTime),
          available: true,
        })
      }
    }
  }

  return slots
}

function normalizeSlotList(data: any): AvailableSlot[] {
  const items = Array.isArray(data) ? data : data?.items ?? data?.result ?? data?.data ?? []
  return (Array.isArray(items) ? items : []).map((item: any) => {
    const wsId = item?.workScheduleId ?? item?.WorkScheduleId ?? item?.id ?? item?.Id ?? ''
    const rawTime = item?.shiftStart ?? item?.ShiftStart ?? item?.time ?? item?.startTime ?? item?.slotTime
    const capacity = item?.remainingCapacity ?? item?.RemainingCapacity ?? item?.available ?? 1
    return {
      id: wsId,
      workScheduleId: wsId,
      time: toTimeString(rawTime),
      available: Number(capacity) > 0,
    }
  })
}

async function callWithFallback<T>(
  call: () => Promise<T>,
  fallback: T,
  allowFallbackStatus: number[] = [401, 403, 404, 405],
): Promise<T> {
  try {
    return await call()
  } catch (error: any) {
    const status = Number(error?.response?.status ?? error?.status ?? 0)
    const details = error?.response?.data ?? error ?? {}

    if (allowFallbackStatus.includes(status)) {
      logger.debug('[patientApi] Backend request returned allowed status, using fallback data.', {
        status,
        error: details,
      })
      return fallback
    }

    if (status >= 500) {
      logger.warn('[patientApi] Backend request failed (server error), using fallback data.', {
        status,
        error: details,
      })
      return fallback
    }

    throw error
  }
}

export const patientApi = {
  getDoctors(specialty?: string): Promise<Doctor[]> {
    return callWithFallback<Doctor[]>(
      async () => {
        const res = await http.get<any>('/doctors', {
          params: { pageNumber: 1, pageSize: 50 },
        })
        const list = normalizeDoctorList(res.data)
        return list
      },
      [],
    )
  },

  getAvailableSlots(doctorId?: string | number, date?: string): Promise<AvailableSlot[]> {
    return callWithFallback<AvailableSlot[]>(
      async () => {
        if (doctorId && doctorId !== '0' && doctorId !== 0) {
          try {
            const res = await http.get<any>(`/doctors/${doctorId}/available`, {
              params: { date: date || undefined },
            })
            const list = normalizeDoctorAvailableSlots(res.data)
            if (list.length > 0) return list
          } catch {
            // fallback to /slots if /doctors/{id}/available fails
          }
        }

        const params: Record<string, any> = {}
        if (doctorId && doctorId !== '0' && doctorId !== 0) {
          params.doctorId = String(doctorId)
        }
        if (date) {
          params.fromDate = new Date(`${date}T00:00:00`).toISOString()
          params.toDate = new Date(`${date}T23:59:59`).toISOString()
        }

        const res = await http.get<any>('/slots', { params })
        const list = normalizeSlotList(res.data)
        return list
      },
      [],
    )
  },

  createAppointment(payload: CreateAppointmentRequest): Promise<Appointment> {
    const workScheduleId = payload.workScheduleId ?? payload.doctorId
    const reason = payload.reason ?? payload.symptoms ?? 'Đặt lịch khám'
    const rawTime = payload.timeSlot ?? payload.appointmentTime ?? '08:00:00'
    const timeSlot = formatTimeSlotForBackend(rawTime)

    return callWithFallback<Appointment>(
      async () => {
        const body = {
          workScheduleId,
          timeSlot,
          reason,
        }

        const res = await http.post<any>('/appointments', body)
        const created = res.data?.result ?? res.data
        const doctorName = created?.doctorName ?? 'BS. Chưa xác định'
        const specialty = created?.specialty ?? 'Khác'

        return {
          id: created?.id ?? String(Date.now()),
          doctorId: created?.doctorId ?? payload.doctorId ?? '',
          doctorName,
          specialty,
          appointmentDate: created?.date ?? payload.appointmentDate ?? '',
          appointmentTime: created?.timeSlot ?? payload.appointmentTime ?? payload.timeSlot ?? timeSlot,
          status: created?.status ?? 'Pending',
          queueNumber: created?.queueNumber ?? `A-${Math.floor(10 + Math.random() * 90)}`,
        }
      },
      {
        id: String(Date.now()),
        doctorId: payload.doctorId ?? '',
        doctorName: 'BS. Chưa xác định',
        specialty: 'Khác',
        appointmentDate: payload.appointmentDate ?? '',
        appointmentTime: payload.appointmentTime ?? payload.timeSlot ?? '08:00:00',
        status: 'Pending',
        queueNumber: `A-${Math.floor(10 + Math.random() * 90)}`,
      },
    )
  },

  getMyAppointments(): Promise<Appointment[]> {
    if (shouldSkipProtectedPatientRequest()) {
      return Promise.resolve([])
    }

    return callWithFallback<Appointment[]>(
      async () => {
        try {
          const res = await http.get<any>('/me/appointments')
          const list = Array.isArray(res.data) ? res.data : res.data?.items ?? res.data?.result ?? []
          const items = Array.isArray(list) ? list : []
          return items.map((item: any) => {
            const id = item.id ?? item.Id ?? ''
            const doctorId = item.doctorId ?? item.DoctorId ?? ''
            const doctorName = item.doctorName ?? item.DoctorName ?? 'BS. Chưa xác định'
            const specialty = item.specialty ?? item.Specialty ?? 'Khác'
            const appointmentDate = item.appointmentDate ?? item.date ?? item.Date ?? ''
            const appointmentTime = toTimeString(item.appointmentTime ?? item.timeSlot ?? item.TimeSlot ?? '08:00')
            const status = ((item.status ?? item.Status ?? 'Pending') as string).trim() as Appointment['status']
            const queueNumber = item.queueNumber ?? item.QueueNumber ?? ''

            return {
              id,
              doctorId,
              doctorName,
              specialty,
              appointmentDate,
              appointmentTime,
              status,
              queueNumber,
            }
          })
        } catch (error: any) {
          const status = Number(error?.response?.status ?? error?.status ?? 0)
          if (status === 403 || status === 404) {
            disableProtectedPatientEndpoints()
          }
          throw error
        }
      },
      [],
    )
  },

  cancelAppointment(id: string | number): Promise<void> {
    return callWithFallback(
      async () => {
        await http.post(`/appointments/${id}/cancel`)
        return undefined
      },
      undefined,
    )
  },

  getMyQueue(): Promise<QueueStatus> {
    if (shouldSkipProtectedPatientRequest()) {
      return Promise.resolve({
        myTicket: '',
        position: 0,
        estimatedWaitMinutes: 0,
        doctorName: 'BS. Chưa xác định',
        appointmentTime: '',
        currentTicket: '',
        entries: [],
      })
    }

    return callWithFallback<QueueStatus>(
      async () => {
        try {
          const res = await http.get<any>('/me/queue-status')
          const list = Array.isArray(res.data) ? res.data : res.data?.items ?? res.data?.result ?? []
          const first = Array.isArray(list) && list.length > 0 ? list[0] : null

          if (!first) {
            return {
              myTicket: '',
              position: 0,
              estimatedWaitMinutes: 0,
              doctorName: 'BS. Chưa xác định',
              appointmentTime: '',
              currentTicket: '',
              entries: [],
            }
          }

          const myTicket = String(first.queueNumber ?? first.QueueNumber ?? first.queueTicketId ?? first.QueueTicketId ?? '')
          const position = Number(first.positionInQueue ?? first.PositionInQueue ?? 0)
          const estimatedWaitMinutes = Number(first.estimatedWaitMinutes ?? first.EstimatedWaitMinutes ?? 0)
          const doctorName = first.doctorName ?? first.DoctorName ?? 'BS. Chưa xác định'
          const currentTicket = myTicket

          return {
            myTicket,
            position,
            estimatedWaitMinutes,
            doctorName,
            appointmentTime: '',
            currentTicket,
            entries: list.map((item: any) => ({
              ticket: String(item.queueNumber ?? item.QueueNumber ?? ''),
              patientName: 'Bệnh nhân',
              doctorId: item.doctorId ?? item.DoctorId ?? '',
              doctorName: item.doctorName ?? item.DoctorName ?? 'BS. Chưa xác định',
              appointmentTime: '',
              status: (item.status ?? item.Status ?? 'Waiting') === 'Waiting' ? 'Waiting' : 'InProgress',
              estimatedWaitMinutes: Number(item.estimatedWaitMinutes ?? item.EstimatedWaitMinutes ?? 0),
              position: Number(item.positionInQueue ?? item.PositionInQueue ?? 0),
              urgent: false,
            })),
          }
        } catch (error: any) {
          const status = Number(error?.response?.status ?? error?.status ?? 0)
          if (status === 403 || status === 404) {
            disableProtectedPatientEndpoints()
          }
          throw error
        }
      },
      {
        myTicket: '',
        position: 0,
        estimatedWaitMinutes: 0,
        doctorName: 'BS. Chưa xác định',
        appointmentTime: '',
        currentTicket: '',
        entries: [],
      },
    )
  },

  getMedicalHistory(): Promise<MedicalRecord[]> {
    if (shouldSkipProtectedPatientRequest()) {
      return Promise.resolve([])
    }

    return callWithFallback<MedicalRecord[]>(
      async () => {
        try {
          const res = await http.get<any>('/me/medical-history')
          const list = Array.isArray(res.data) ? res.data : res.data?.items ?? res.data?.result ?? []

          return (Array.isArray(list) ? list : []).map((item: any) => {
            const id = item.id ?? item.Id ?? ''
            const examDate = item.examDate ?? item.ExamDate ?? item.examinationDate ?? item.date ?? ''
            const doctorName = item.doctorName ?? item.DoctorName ?? 'BS. Chưa xác định'
            const specialty = item.specialty ?? item.Specialty ?? 'Khác'
            const diagnosis = item.diagnosis ?? item.Diagnosis ?? ''
            const prescription = item.prescription ?? item.Prescription ?? ''
            const note = item.note ?? item.notes ?? item.Notes ?? ''

            return {
              id,
              examinationDate: examDate,
              doctorName,
              specialty,
              diagnosis,
              prescription,
              note,
            }
          })
        } catch (error: any) {
          const status = Number(error?.response?.status ?? error?.status ?? 0)
          if (status === 403 || status === 404) {
            disableProtectedPatientEndpoints()
          }
          throw error
        }
      },
      [],
    )
  },

  getMyProfile(): Promise<PatientProfile> {
    const fallback: PatientProfile = { fullName: '', email: '', phoneNumber: '' }
    if (shouldSkipProtectedPatientRequest()) {
      return Promise.resolve(fallback)
    }

    return callWithFallback<PatientProfile>(
      async () => {
        const tries = ['/patients/me']
        for (const p of tries) {
          try {
            const res = await http.get<any>(p)
            const data = res.data?.result ?? res.data ?? res.data?.data ?? res.data?.profile ?? {}
            return {
              id: data.id ?? data.Id ?? data.patientId ?? data.PatientId ?? data.userId ?? data.UserId,
              fullName: data.fullName ?? data.FullName ?? data.name ?? data.Name ?? data.full_name ?? data.username,
              email: data.email ?? data.Email ?? data.emailAddress ?? data.EmailAddress ?? data.email_address,
              phoneNumber: data.phoneNumber ?? data.PhoneNumber ?? data.phone ?? data.Phone ?? data.phone_number,
              address: data.address ?? data.Address ?? data.location ?? data.Location,
              dateOfBirth: data.dateOfBirth ?? data.DateOfBirth ?? data.dob ?? data.Dob ?? data.birthDate ?? data.BirthDate,
              gender: data.gender ?? data.Gender ?? data.sex ?? data.Sex,
              insuranceNumber: data.insuranceNumber ?? data.InsuranceNumber ?? data.insurance_number,
              emergencyContact: data.emergencyContact ?? data.EmergencyContact ?? data.emergency_contact,
            }
          } catch (e: any) {
            const status = e?.response?.status
            if (status === 403 || status === 404) {
              disableProtectedPatientEndpoints()
              return fallback
            }
            continue
          }
        }

        return fallback
      },
      fallback,
    )
  },

  async updateMyProfile(payload: {
    email?: string
    gender?: number | string
    address?: string
    insuranceNumber?: string | null
    emergencyContact?: string | null
    dateOfBirth?: string | null
    phoneNumber?: string | null
    fullName?: string | null
  }): Promise<void> {
    try {
      await http.put('/patients/me', payload)
    } catch {
      await http.post('/patients', payload)
    }
  },
}
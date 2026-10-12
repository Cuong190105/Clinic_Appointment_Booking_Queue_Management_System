import { http } from '@/lib/api/http'
import type {
  CreateShiftPayload,
  DoctorScheduleResponse,
  ShiftSuggestion,
  UpdateShiftPayload,
  WorkSchedule,
} from './shifts.types'

function formatTime(t: any): string {
  if (!t) return ''
  if (typeof t === 'string') return t.slice(0, 5)
  if (typeof t === 'object' && 'hours' in t) {
    const hh = String(t.hours ?? 0).padStart(2, '0')
    const mm = String(t.minutes ?? 0).padStart(2, '0')
    return `${hh}:${mm}`
  }
  return String(t)
}

function ensureTimeWithSeconds(timeStr: string): string {
  if (!timeStr) return '08:00:00'
  const parts = timeStr.split(':')
  if (parts.length === 2) return `${timeStr}:00`
  return timeStr
}

export const shiftsApi = {
  // GET /doctors/{doctorId}/shifts?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
  async getDoctorShifts(doctorId: string, startDate: string, endDate: string): Promise<DoctorScheduleResponse> {
    const res = await http
      .get<any>(`/doctors/${doctorId}/shifts`, {
        params: {
          StartDate: startDate,
          EndDate: endDate,
        },
      })
      .then((r) => r.data)

    const data = res?.result !== undefined ? res.result : res
    const rawSchedules = data?.schedules || data?.Schedules || []

    const schedules: WorkSchedule[] = rawSchedules.map((s: any) => ({
      id: String(s.id || s.Id || ''),
      doctorId: String(s.doctorId || s.DoctorId || doctorId),
      doctorName: data?.doctorName || data?.DoctorName || '',
      date: s.date || s.Date || '',
      startTime: formatTime(s.startTime || s.StartTime || s.shiftStart),
      endTime: formatTime(s.endTime || s.EndTime || s.shiftEnd),
      patientLimit: s.patientLimit ?? s.PatientLimit ?? 20,
      status: s.status ?? s.Status ?? 'Active',
    }))

    return {
      doctorId: String(data?.doctorId || data?.DoctorId || doctorId),
      doctorName: data?.doctorName || data?.DoctorName || '',
      schedules,
      startDate: data?.startDate || data?.StartDate || startDate,
      endDate: data?.endDate || data?.EndDate || endDate,
    }
  },

  // POST /doctors/{doctorId}/shifts (Admin only)
  async createDoctorShift(payload: CreateShiftPayload): Promise<WorkSchedule> {
    const res = await http
      .post<any>(`/doctors/${payload.doctorId}/shifts`, {
        date: payload.date,
        startTime: ensureTimeWithSeconds(payload.startTime),
        endTime: ensureTimeWithSeconds(payload.endTime),
        patientLimit: Number(payload.patientLimit) || 20,
      })
      .then((r) => r.data)

    const s = res?.result !== undefined ? res.result : res
    return {
      id: String(s?.id || s?.Id || ''),
      doctorId: payload.doctorId,
      date: s?.date || s?.Date || payload.date,
      startTime: formatTime(s?.startTime || s?.StartTime || payload.startTime),
      endTime: formatTime(s?.endTime || s?.EndTime || payload.endTime),
      patientLimit: s?.patientLimit ?? s?.PatientLimit ?? payload.patientLimit,
      status: s?.status ?? s?.Status ?? 'Active',
    }
  },

  // PUT /shifts/{shiftId}
  async updateShift(shiftId: string, payload: UpdateShiftPayload): Promise<void> {
    await http.put(`/shifts/${shiftId}`, {
      date: payload.date,
      startTime: ensureTimeWithSeconds(payload.startTime),
      endTime: ensureTimeWithSeconds(payload.endTime),
      patientLimitPerSlot: Number(payload.patientLimitPerSlot) || 20,
    })
  },

  // POST /shifts/{shiftId}/cancel
  async cancelShift(shiftId: string, reason: string): Promise<void> {
    await http.post(`/shifts/${shiftId}/cancel`, {
      reason: reason || 'Hủy ca trực',
    })
  },

  // GET /slots?fromDate=...&toDate=... (Lấy toàn bộ slots/schedules thực tế trong tuần)
  async getAllSlots(fromDate: string, toDate: string, doctorId?: string, specialtyId?: string): Promise<WorkSchedule[]> {
    const params: Record<string, any> = {
      fromDate: `${fromDate}T00:00:00.000Z`,
      toDate: `${toDate}T23:59:59.000Z`,
    }
    if (doctorId) params.doctorId = doctorId
    if (specialtyId && specialtyId !== 'all') params.specialtyId = specialtyId

    const res = await http.get<any>('/slots', { params }).then((r) => r.data)
    const rawItems = res?.result !== undefined ? res.result : res
    const items = Array.isArray(rawItems) ? rawItems : rawItems?.items || []

    return items.map((item: any) => ({
      id: String(item.workScheduleId || item.WorkScheduleId || item.id || item.Id || ''),
      doctorId: String(item.doctorId || item.DoctorId || ''),
      doctorName: item.doctorName || item.DoctorName || '',
      date: item.date || item.Date || '',
      startTime: formatTime(item.shiftStart || item.ShiftStart || item.startTime),
      endTime: formatTime(item.shiftEnd || item.ShiftEnd || item.endTime),
      patientLimit: item.patientLimit ?? item.PatientLimit ?? 20,
      remainingCapacity: item.remainingCapacity ?? item.RemainingCapacity ?? 0,
      status: 'Active',
    }))
  },

  // ──────────────────────────────────────────────
  // Shift Suggestions Approval (Admin)
  // ──────────────────────────────────────────────

  // GET /doctors/{doctorId}/suggestions?StartDate=...&EndDate=...
  async getDoctorSuggestions(doctorId: string, startDate: string, endDate: string): Promise<ShiftSuggestion[]> {
    const res = await http
      .get<any>(`/doctors/${doctorId}/suggestions`, {
        params: {
          StartDate: startDate,
          EndDate: endDate,
        },
      })
      .then((r) => r.data)

    const data = res?.result !== undefined ? res.result : res
    const rawSchedules = data?.schedules || data?.Schedules || []
    const doctorName = data?.doctorName || data?.DoctorName || ''

    return rawSchedules.map((s: any) => ({
      id: String(s.id || s.Id || ''),
      doctorId: String(s.doctorId || s.DoctorId || doctorId),
      doctorName: doctorName || s.doctorName || s.DoctorName || '',
      date: s.date || s.Date || '',
      startTime: formatTime(s.startTime || s.StartTime || s.shiftStart),
      endTime: formatTime(s.endTime || s.EndTime || s.shiftEnd),
      patientLimit: s.patientLimit ?? s.PatientLimit ?? 20,
      reason: s.reason || s.Reason || '',
      status: s.status || s.Status || 'Pending',
    }))
  },

  // POST /shifts/suggestions/{suggestionId}/approve
  async approveShiftSuggestion(suggestionId: string): Promise<void> {
    await http.post(`/shifts/suggestions/${suggestionId}/approve`)
  },

  // POST /shifts/suggestions/{suggestionId}/reject
  async rejectShiftSuggestion(suggestionId: string): Promise<void> {
    await http.post(`/shifts/suggestions/${suggestionId}/reject`)
  },
}

import { http } from '@/lib/api/http'
import type {
  CreateShiftPayload,
  DoctorSchedulePageItem,
  DoctorScheduleResponse,
  PagedDoctorSchedulesResponse,
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
  async getDoctorShifts(
    doctorId: string,
    startDate: string,
    endDate: string,
  ): Promise<DoctorScheduleResponse> {
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
      RecommendedCapacity: s.RecommendedCapacity ?? s.RecommendedCapacity ?? 20,
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
        RecommendedCapacity: Number(payload.RecommendedCapacity) || 20,
      })
      .then((r) => r.data)

    const s = res?.result !== undefined ? res.result : res
    return {
      id: String(s?.id || s?.Id || ''),
      doctorId: payload.doctorId,
      date: s?.date || s?.Date || payload.date,
      startTime: formatTime(s?.startTime || s?.StartTime || payload.startTime),
      endTime: formatTime(s?.endTime || s?.EndTime || payload.endTime),
      RecommendedCapacity:
        s?.RecommendedCapacity ?? s?.RecommendedCapacity ?? payload.RecommendedCapacity,
      status: s?.status ?? s?.Status ?? 'Active',
    }
  },

  // PUT /shifts/{shiftId}
  async updateShift(shiftId: string, payload: UpdateShiftPayload): Promise<void> {
    await http.put(`/shifts/${shiftId}`, {
      date: payload.date,
      startTime: ensureTimeWithSeconds(payload.startTime),
      endTime: ensureTimeWithSeconds(payload.endTime),
      RecommendedCapacityPerSlot: Number(payload.RecommendedCapacityPerSlot) || 20,
    })
  },

  // POST /shifts/{shiftId}/cancel
  async cancelShift(shiftId: string, reason: string): Promise<void> {
    await http.post(`/shifts/${shiftId}/cancel`, {
      reason: reason || 'Hủy ca trực',
    })
  },

  // GET /doctors/shifts?StartDate=...&EndDate=... (Paginated doctor schedules)
  async getAllSlots(
    fromDate: string,
    toDate: string,
    pageNumber = 1,
    pageSize = 10,
    specialtyId?: string,
    searchTerm?: string,
  ): Promise<PagedDoctorSchedulesResponse> {
    const res = await http
      .get<any>('/doctors/shifts', {
        params: {
          StartDate: fromDate,
          EndDate: toDate,
          SpecialtyId: specialtyId && specialtyId !== 'all' ? specialtyId : undefined,
          SearchTerm: searchTerm?.trim() || undefined,
          PageNumber: pageNumber,
          PageSize: pageSize,
        },
      })
      .then((r) => r.data)

    const data = res?.result !== undefined ? res.result : res
    const doctorRows = data?.items || data?.Items || []
    const items: DoctorSchedulePageItem[] = doctorRows.map((doctor: any) => {
      const doctorIdValue = String(
        doctor.doctorId || doctor.DoctorId || doctor.id || doctor.Id || '',
      )
      const doctorName = doctor.doctorName || doctor.DoctorName || ''
      const schedules = doctor.schedules || doctor.Schedules || []

      return {
        doctorId: doctorIdValue,
        doctorName,
        specialtyId: String(doctor.specialtyId || doctor.SpecialtyId || ''),
        specialtyName: doctor.specialtyName || doctor.SpecialtyName || '',
        schedules: schedules.map((schedule: any) => ({
          id: String(schedule.id || schedule.Id || ''),
          doctorId: String(schedule.doctorId || schedule.DoctorId || doctorIdValue),
          doctorName,
          specialtyName: doctor.specialtyName || doctor.SpecialtyName || '',
          date: schedule.date || schedule.Date || '',
          startTime: formatTime(
            schedule.startTime || schedule.StartTime || schedule.shiftStart || schedule.ShiftStart,
          ),
          endTime: formatTime(
            schedule.endTime || schedule.EndTime || schedule.shiftEnd || schedule.ShiftEnd,
          ),
          RecommendedCapacity: schedule.RecommendedCapacity ?? schedule.RecommendedCapacity ?? 20,
          status: schedule.status ?? schedule.Status ?? 'Active',
        })),
        startDate: doctor.startDate || doctor.StartDate || fromDate,
        endDate: doctor.endDate || doctor.EndDate || toDate,
      }
    })

    const responsePageNumber = data?.pageNumber ?? data?.PageNumber ?? pageNumber
    const responsePageSize = data?.pageSize ?? data?.PageSize ?? pageSize
    const totalCount = data?.totalCount ?? data?.TotalCount ?? doctorRows.length
    const totalPages =
      data?.totalPages ?? data?.TotalPages ?? (Math.ceil(totalCount / responsePageSize) || 1)

    return {
      items,
      pageNumber: responsePageNumber,
      pageSize: responsePageSize,
      totalCount,
      totalPages,
      hasPrevious: data?.hasPrevious ?? data?.HasPrevious ?? responsePageNumber > 1,
      hasNext: data?.hasNext ?? data?.HasNext ?? responsePageNumber < totalPages,
    }
  },

  // ──────────────────────────────────────────────
  // Shift Suggestions Approval (Admin)
  // ──────────────────────────────────────────────

  // GET /doctors/{doctorId}/suggestions?StartDate=...&EndDate=...
  async getDoctorSuggestions(
    doctorId: string,
    startDate: string,
    endDate: string,
  ): Promise<ShiftSuggestion[]> {
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
      RecommendedCapacity: s.RecommendedCapacity ?? s.RecommendedCapacity ?? 20,
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

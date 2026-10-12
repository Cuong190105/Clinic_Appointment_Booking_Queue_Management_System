import { http } from '@/lib/api/http'
import type {
  ApiResponse,
  AppointmentDetail,
  Doctor,
  DoctorDetail,
  DoctorSchedule,
  GetDoctorsParams,
  PagedDoctorsResponse,
  CreateDoctorPayload,
  UpdateDoctorPayload,
  UpdateDoctorRequest,
  QueueTicket,
  RequestedShift,
  SkipQueueResult,
  WorkSchedule,
} from './doctors.types'

const unwrap = <T>(data: ApiResponse<T> | T): T => {
  if (data && typeof data === 'object' && 'result' in data) {
    return (data as ApiResponse<T>).result
  }
  return data as T
}

export const doctorsApi = {
  // GET /doctors
  async list(params?: GetDoctorsParams): Promise<PagedDoctorsResponse> {
    const res = await http
      .get<any>('/doctors', {
        params: {
          Search: params?.search || undefined,
          SpecialtyId: params?.specialtyId || undefined,
          SortBy: params?.sortBy || 'fullName',
          OrderBy: params?.orderBy || 'asc',
          Gender: params?.gender,
          Status: params?.status !== undefined && params?.status !== '' && params?.status !== 'all' ? params.status : undefined,
          PageNumber: params?.pageNumber ?? 1,
          PageSize: params?.pageSize ?? 10,
        },
      })
      .then((r) => r.data)

    const data = res?.result !== undefined ? res.result : res
    const rawItems = data?.items || data?.Items || (Array.isArray(data) ? data : [])
    const items: Doctor[] = rawItems.map((d: any) => ({
      id: String(d.id || d.Id || ''),
      name: d.fullName || d.FullName || d.name || d.Name || '',
      fullName: d.fullName || d.FullName || d.name || d.Name || '',
      specialty: d.currentSpecialty || d.CurrentSpecialty || d.specialty || '',
      specialtyId: d.specialtyId || d.SpecialtyId || '',
      experienceYears: d.experienceYears ?? d.ExperienceYears ?? 0,
      rating: d.rating ?? d.Rating ?? 5.0,
      avatarUrl: d.avatarUrl || d.AvatarUrl || '',
      email: d.email || d.Email || '',
      phoneNumber: d.phoneNumber || d.PhoneNumber || '',
      licenseNumber: d.licenseNumber || d.LicenseNumber || '',
      qualification: d.qualification || d.Qualification || '',
      biography: d.biography || d.Biography || '',
      status: d.status ?? d.Status ?? 0,
      dateOfBirth: d.dateOfBirth || d.DateOfBirth || '',
      gender: typeof d.gender === 'string' ? (d.gender.toLowerCase() === 'female' ? 1 : 0) : (d.gender ?? 0),
    }))

    const pageNumber = data?.pageNumber || data?.PageNumber || params?.pageNumber || 1
    const pageSize = data?.pageSize || data?.PageSize || params?.pageSize || 10
    const totalCount = data?.totalCount ?? data?.TotalCount ?? items.length
    const totalPages = data?.totalPages || data?.TotalPages || Math.ceil(totalCount / pageSize) || 1

    return {
      items,
      pageNumber,
      pageSize,
      totalCount,
      totalPages,
      hasPrevious: pageNumber > 1,
      hasNext: pageNumber < totalPages,
    }
  },

  // GET /doctors/{doctorId}
  async getById(id: string): Promise<DoctorDetail> {
    const res = await http.get<any>(`/doctors/${id}`).then((r) => r.data)
    const d = res?.result !== undefined ? res.result : res
    return {
      id: String(d.id || d.Id || ''),
      name: d.fullName || d.FullName || d.name || d.Name || '',
      fullName: d.fullName || d.FullName || d.name || d.Name || '',
      specialty: d.currentSpecialty || d.CurrentSpecialty || d.specialty || '',
      specialtyId: d.specialtyId || d.SpecialtyId || '',
      experienceYears: d.experienceYears ?? d.ExperienceYears ?? 0,
      rating: d.rating ?? d.Rating ?? 5.0,
      avatarUrl: d.avatarUrl || d.AvatarUrl || '',
      email: d.email || d.Email || '',
      phoneNumber: d.phoneNumber || d.PhoneNumber || '',
      licenseNumber: d.licenseNumber || d.LicenseNumber || '',
      qualification: d.qualification || d.Qualification || '',
      biography: d.biography || d.Biography || '',
      status: d.status ?? d.Status ?? 0,
      dateOfBirth: d.dateOfBirth || d.DateOfBirth || '',
      gender: typeof d.gender === 'string' ? (d.gender.toLowerCase() === 'female' ? 1 : 0) : (d.gender ?? 0),
      address: d.address || d.Address || '',
      hireDate: d.hireDate || d.HireDate || '',
    } as any
  },

  // GET /doctors/me (Admin fallback)
  async getMyProfile(): Promise<DoctorDetail> {
    const res = await http.get<any>('/doctors/me').then((r) => r.data)
    const d = res?.result !== undefined ? res.result : res
    return {
      id: String(d.id || d.Id || ''),
      name: d.fullName || d.FullName || d.name || d.Name || '',
      fullName: d.fullName || d.FullName || d.name || d.Name || '',
      specialty: d.currentSpecialty || d.CurrentSpecialty || d.specialty || '',
      specialtyId: d.specialtyId || d.SpecialtyId || '',
      experienceYears: d.experienceYears ?? d.ExperienceYears ?? 0,
      rating: d.rating ?? d.Rating ?? 5.0,
      avatarUrl: d.avatarUrl || d.AvatarUrl || '',
      email: d.email || d.Email || '',
      phoneNumber: d.phoneNumber || d.PhoneNumber || '',
      licenseNumber: d.licenseNumber || d.LicenseNumber || '',
      qualification: d.qualification || d.Qualification || '',
      biography: d.biography || d.Biography || '',
      status: d.status ?? d.Status ?? 0,
      dateOfBirth: d.dateOfBirth || d.DateOfBirth || '',
      gender: typeof d.gender === 'string' ? (d.gender.toLowerCase() === 'female' ? 1 : 0) : (d.gender ?? 0),
      address: d.address || d.Address || '',
      hireDate: d.hireDate || d.HireDate || '',
    } as any
  },

  // POST /doctors
  async create(payload: CreateDoctorPayload): Promise<any> {
    const res = await http
      .post<any>('/doctors', {
        username: payload.username,
        password: payload.password,
        fullName: payload.fullName,
        phoneNumber: payload.phoneNumber,
        email: payload.email,
        dateOfBirth: payload.dateOfBirth,
        gender: payload.gender,
        address: payload.address,
        hireDate: new Date().toISOString().split('T')[0],
        licenseNumber: payload.licenseNumber,
        qualification: payload.qualification,
        biography: payload.biography || null,
        experienceYears: Number(payload.experienceYears) || 0,
        status: 0,
        specialtyId: payload.specialtyId,
      })
      .then((r) => r.data)
    return res?.result !== undefined ? res.result : res
  },

  // PUT /doctors/{doctorId}
  async update(id: string, payload: UpdateDoctorPayload): Promise<void> {
    await http.put(`/doctors/${id}`, {
      fullName: payload.fullName,
      phoneNumber: payload.phoneNumber,
      email: payload.email,
      dateOfBirth: payload.dateOfBirth || '1990-01-01',
      gender: payload.gender ?? 0,
      address: payload.address || 'Cơ sở khám chữa bệnh',
      licenseNumber: payload.licenseNumber,
      qualification: payload.qualification || 'Bác sĩ chuyên khoa',
      experienceYears: Number(payload.experienceYears) || 1,
      biography: payload.biography || null,
    })
  },

  // DELETE /doctors/{doctorId} -> Inactive
  async delete(id: string): Promise<void> {
    await http.patch(`/doctors/${id}/status`, 1)
  },

  // PATCH /doctors/{doctorId}/status
  async updateStatus(id: string, status: number): Promise<void> {
    await http.patch(`/doctors/${id}/status`, status)
  },

  // ──────────────────────────────────────────────
  // Doctor Portal APIs (from doctor-full branch)
  // ──────────────────────────────────────────────

  // GET /doctors/me (Doctor Portal – typed DoctorDetail)
  getOwnProfile: (): Promise<DoctorDetail> =>
    http.get<ApiResponse<DoctorDetail>>('/doctors/me').then((r) => unwrap(r.data)),

  // PUT /doctors/me
  updateOwnProfile: (data: UpdateDoctorRequest): Promise<unknown> =>
    http.put<ApiResponse<unknown>>('/doctors/me', data).then((r) => unwrap(r.data)),

  // GET /shifts?StartDate=...&EndDate=...
  getOwnSchedule: (startDate: string, endDate: string): Promise<DoctorSchedule<WorkSchedule>> =>
    http
      .get<ApiResponse<DoctorSchedule<WorkSchedule>>>('/shifts', {
        params: { StartDate: startDate, EndDate: endDate },
      })
      .then((r) => unwrap(r.data)),

  // GET /shifts/suggestions
  getOwnShiftRequests: (startDate: string, endDate: string): Promise<DoctorSchedule<RequestedShift>> =>
    http
      .get<ApiResponse<DoctorSchedule<RequestedShift>> | DoctorSchedule<RequestedShift>>('/shifts/suggestions', {
        params: {
          StartDate: startDate,
          EndDate: endDate,
        },
      })
      .then((r) => unwrap(r.data)),

  // POST /shifts/suggestions
  createShiftRequest: (data: {
    date: string
    startTime: string
    endTime: string
    patientLimit: number
    reason: string
  }): Promise<RequestedShift> =>
    http
      .post<ApiResponse<RequestedShift> | RequestedShift>('/shifts/suggestions', data)
      .then((r) => unwrap(r.data)),

  // PATCH /shifts/suggestions/{id}
  updateShiftRequest: (
    id: string,
    data: {
      date: string
      startTime: string
      endTime: string
      patientLimitPerSlot: number
      reason: string
    },
  ): Promise<unknown> =>
    http.patch<ApiResponse<unknown>>(`/shifts/suggestions/${id}`, data).then((r) => unwrap(r.data)),

  // POST /shifts/suggestions/{id}/cancel
  cancelShiftRequest: (id: string): Promise<unknown> =>
    http.post<ApiResponse<unknown>>(`/shifts/suggestions/${id}/cancel`).then((r) => unwrap(r.data)),

  // GET /doctors/{doctorId}/queue
  getQueue: (doctorId: string): Promise<QueueTicket[]> =>
    http.get<ApiResponse<QueueTicket[]>>(`/doctors/${doctorId}/queue`).then((r) => unwrap(r.data)),

  // POST /doctors/{doctorId}/queue/next
  callNext: (doctorId: string): Promise<QueueTicket> =>
    http.post<ApiResponse<QueueTicket>>(`/doctors/${doctorId}/queue/next`).then((r) => unwrap(r.data)),

  // POST /queue/{queueTicketId}/start-exam
  startExam: (queueTicketId: string): Promise<unknown> =>
    http.post<ApiResponse<unknown>>(`/queue/${queueTicketId}/start-exam`).then((r) => unwrap(r.data)),

  // POST /queue/{queueTicketId}/complete-exam
  completeExam: (queueTicketId: string): Promise<unknown> =>
    http.post<ApiResponse<unknown>>(`/queue/${queueTicketId}/complete-exam`).then((r) => unwrap(r.data)),

  // POST /queue/{queueTicketId}/skip
  skip: (queueTicketId: string): Promise<SkipQueueResult> =>
    http.post<ApiResponse<SkipQueueResult>>(`/queue/${queueTicketId}/skip`).then((r) => unwrap(r.data)),

  // PATCH /queue/{queueTicketId}/priority
  setPriority: (queueTicketId: string, priority: boolean): Promise<unknown> =>
    http
      .patch<ApiResponse<unknown>>(`/queue/${queueTicketId}/priority`, { priority })
      .then((r) => unwrap(r.data)),

  // GET /appointments/{appointmentId}
  getAppointment: (appointmentId: string): Promise<AppointmentDetail> =>
    http
      .get<ApiResponse<AppointmentDetail>>(`/appointments/${appointmentId}`)
      .then((r) => unwrap(r.data)),

  // Alias for compatibility
  get: (id: string) =>
    http.get<ApiResponse<Doctor>>(`/doctors/${id}`).then((r) => unwrap(r.data)),
}

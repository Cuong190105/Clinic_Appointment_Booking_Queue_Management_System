import { http } from '@/lib/api/http'
import type {
  GetMedicalReportsParams,
  MedicalReportDto,
  PagedMedicalReportsResponse,
  UpdateMedicalReportPayload,
} from './medical-records.types'

const unwrap = <T>(data: any): T => {
  if (data && typeof data === 'object' && 'result' in data) {
    return data.result
  }
  return data as T
}

export const medicalRecordsApi = {
  // GET /medical-reports
  async list(params?: GetMedicalReportsParams): Promise<PagedMedicalReportsResponse> {
    const res = await http.get('/medical-reports', {
      params: {
        Search: params?.search || undefined,
        Status: params?.status !== undefined && params?.status !== 'all' && params?.status !== '' ? params.status : undefined,
        FromDate: params?.fromDate || undefined,
        ToDate: params?.toDate || undefined,
        PageNumber: params?.pageNumber ?? 1,
        PageSize: params?.pageSize ?? 10,
      },
    })

    const raw = unwrap<any>(res.data)
    const items: MedicalReportDto[] = Array.isArray(raw)
      ? raw
      : raw?.items || []

    const pageNumber = raw?.pageNumber ?? params?.pageNumber ?? 1
    const pageSize = raw?.pageSize ?? params?.pageSize ?? 10
    const totalCount = raw?.totalCount ?? items.length
    const totalPages = raw?.totalPages ?? Math.ceil(totalCount / pageSize) ?? 1

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

  // GET /medical-reports/{id}
  async getById(id: string): Promise<MedicalReportDto> {
    const res = await http.get(`/medical-reports/${id}`)
    return unwrap<MedicalReportDto>(res.data)
  },

  async getByAppointment(appointmentId: string): Promise<MedicalReportDto> {
    const res = await http.get(`/medical-reports/appointment/${appointmentId}`)
    return unwrap<MedicalReportDto>(res.data)
  },

  async createDraft(appointmentId: string): Promise<MedicalReportDto> {
    const res = await http.post('/medical-reports', {
      appointmentId,
      isFinalize: false,
    })
    return unwrap<MedicalReportDto>(res.data)
  },

  // PUT /medical-reports/{id}
  async update(id: string, payload: UpdateMedicalReportPayload): Promise<MedicalReportDto> {
    const res = await http.put(`/medical-reports/${id}`, payload)
    return unwrap<MedicalReportDto>(res.data)
  },

  // POST /medical-reports/{id}/finalize
  async finalize(id: string): Promise<MedicalReportDto> {
    const res = await http.post(`/medical-reports/${id}/finalize`)
    return unwrap<MedicalReportDto>(res.data)
  },
}


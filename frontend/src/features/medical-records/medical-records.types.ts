export enum MedicalReportStatus {
  Draft = 0,
  Finalized = 1,
}

export interface MedicalReportDto {
  id: string
  appointmentId: string
  patientId: string
  patientName: string
  doctorId: string
  doctorName: string
  specialtyName: string
  examDate: string
  symptoms?: string | null
  diagnosis?: string | null
  prescription?: string | null
  notes?: string | null
  examStartTime?: string | null
  examEndTime?: string | null
  status: MedicalReportStatus | number | 'Draft' | 'Finalized'
  createdAt: string
}

export interface GetMedicalReportsParams {
  search?: string
  status?: MedicalReportStatus | number | string
  fromDate?: string
  toDate?: string
  pageNumber?: number
  pageSize?: number
}

export interface UpdateMedicalReportPayload {
  symptoms?: string
  diagnosis?: string
  prescription?: string
  notes?: string
  isFinalize?: boolean
}

export interface PagedMedicalReportsResponse {
  items: MedicalReportDto[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}


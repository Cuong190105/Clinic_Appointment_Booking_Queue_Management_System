export interface Specialty {
  id: string
  name: string
  description?: string | null
  establishedDate: string
  status?: string | boolean
}

export interface CreateSpecialtyPayload {
  name: string
  description?: string | null
  establishedDate: string
}

export interface UpdateSpecialtyPayload {
  name: string
  description?: string | null
  establishedDate: string
}

export interface GetSpecialtiesParams {
  search?: string
  pageNumber?: number
  pageSize?: number
  sortBy?: string
  orderBy?: string
}

export interface PagedSpecialtiesResponse {
  items: Specialty[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

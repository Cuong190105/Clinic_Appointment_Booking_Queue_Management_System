import { http } from '@/lib/api/http'
import { env } from '@/config/env'
import { SPECIALTIES } from '@/features/admin/admin.mock'
import type {
  CreateSpecialtyPayload,
  GetSpecialtiesParams,
  PagedSpecialtiesResponse,
  Specialty,
  UpdateSpecialtyPayload,
} from './specialties.types'

export const specialtiesApi = {
  // GET /specialties
  async list(params?: GetSpecialtiesParams): Promise<PagedSpecialtiesResponse> {
    if (env.enableMock) {
      const filtered = SPECIALTIES.filter((s) =>
        !params?.search || s.name.toLowerCase().includes(params.search.toLowerCase()),
      )
      const page = params?.pageNumber ?? 1
      const size = params?.pageSize ?? 10
      const start = (page - 1) * size
      const items = filtered.slice(start, start + size).map((s) => ({
        id: String(s.id),
        name: s.name,
        description: `Chuyên khoa ${s.name}`,
        establishedDate: '2020-01-01',
        status: s.status === 'active',
      }))

      return {
        items,
        pageNumber: page,
        pageSize: size,
        totalCount: filtered.length,
        totalPages: Math.ceil(filtered.length / size) || 1,
        hasPrevious: page > 1,
        hasNext: start + size < filtered.length,
      }
    }

    const res = await http
      .get<any>('/specialties', { params })
      .then((r) => r.data)

    const data = res?.result !== undefined ? res.result : res
    const rawItems = data?.items || data?.Items || (Array.isArray(data) ? data : [])
    const items: Specialty[] = rawItems.map((s: any) => ({
      id: String(s.id || s.Id || ''),
      name: s.name || s.Name || '',
      description: s.description || s.Description || '',
      establishedDate: s.establishedDate || s.EstablishedDate || '',
      status: s.status ?? s.Status ?? true,
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

  // GET /specialties/{id}
  async get(id: string): Promise<Specialty> {
    const res = await http.get<any>(`/specialties/${id}`).then((r) => r.data)
    const s = res?.result !== undefined ? res.result : res
    return {
      id: String(s.id || s.Id || ''),
      name: s.name || s.Name || '',
      description: s.description || s.Description || '',
      establishedDate: s.establishedDate || s.EstablishedDate || '',
      status: s.status ?? s.Status ?? true,
    }
  },

  // POST /specialties
  async create(payload: CreateSpecialtyPayload): Promise<Specialty> {
    const res = await http.post<any>('/specialties', payload).then((r) => r.data)
    return res?.result !== undefined ? res.result : res
  },

  // PUT /specialties/{id}
  async update(id: string, payload: UpdateSpecialtyPayload): Promise<void> {
    await http.put(`/specialties/${id}`, payload)
  },

  // PATCH /specialties/{id}/status
  async toggleStatus(id: string): Promise<void> {
    await http.patch(`/specialties/${id}/status`)
  },
}

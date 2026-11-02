import { describe, it, expect, vi, beforeEach } from 'vitest'
import { medicalRecordsApi } from '../medical-records.api'
import { http } from '@/lib/api/http'

vi.mock('@/lib/api/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}))

describe('medicalRecordsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list handles pagination and filters correctly', async () => {
    vi.mocked(http.get).mockResolvedValueOnce({
      data: {
        result: {
          items: [
            { id: 'mr-1', patientName: 'Nguyen Van A', diagnosis: 'Cam cum' },
          ],
          totalCount: 1,
          pageNumber: 1,
          pageSize: 10,
          totalPages: 1,
        },
      },
    } as any)

    const res = await medicalRecordsApi.list({
      search: 'Nguyen',
      status: 'Finalized',
      pageNumber: 1,
      pageSize: 10,
    })

    expect(http.get).toHaveBeenCalledWith('/medical-reports', {
      params: {
        Search: 'Nguyen',
        Status: 'Finalized',
        FromDate: undefined,
        ToDate: undefined,
        PageNumber: 1,
        PageSize: 10,
      },
    })
    expect(res.items).toHaveLength(1)
    expect(res.items[0]?.diagnosis).toBe('Cam cum')
  })

  it('getByAppointment calls /medical-reports/appointment/:id', async () => {
    vi.mocked(http.get).mockResolvedValueOnce({
      data: { result: { id: 'mr-1', appointmentId: 'apt-10' } },
    } as any)

    const res = await medicalRecordsApi.getByAppointment('apt-10')
    expect(http.get).toHaveBeenCalledWith('/medical-reports/appointment/apt-10')
    expect(res.id).toBe('mr-1')
  })

  it('createDraft posts appointmentId with isFinalize: false', async () => {
    vi.mocked(http.post).mockResolvedValueOnce({
      data: { result: { id: 'mr-new', isFinalized: false } },
    } as any)

    const res = await medicalRecordsApi.createDraft('apt-20')
    expect(http.post).toHaveBeenCalledWith('/medical-reports', {
      appointmentId: 'apt-20',
      isFinalize: false,
    })
    expect(res.id).toBe('mr-new')
  })

  it('update puts payload to /medical-reports/:id', async () => {
    const payload = {
      diagnosis: 'Viem hong',
      treatmentPlan: 'Nghi ngoi',
      notes: 'Tai kham sau 3 ngay',
    }
    vi.mocked(http.put).mockResolvedValueOnce({
      data: { result: { id: 'mr-1', ...payload } },
    } as any)

    const res = await medicalRecordsApi.update('mr-1', payload)
    expect(http.put).toHaveBeenCalledWith('/medical-reports/mr-1', payload)
    expect(res.diagnosis).toBe('Viem hong')
  })

  it('finalize posts to /medical-reports/:id/finalize', async () => {
    vi.mocked(http.post).mockResolvedValueOnce({
      data: { result: { id: 'mr-1', isFinalized: true } },
    } as any)

    const res = await medicalRecordsApi.finalize('mr-1')
    expect(http.post).toHaveBeenCalledWith('/medical-reports/mr-1/finalize')
    expect(res.id).toBe('mr-1')
  })
})

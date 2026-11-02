import { describe, it, expect, vi, beforeEach } from 'vitest'
import { shiftsApi } from '../shifts.api'
import { http } from '@/lib/api/http'

vi.mock('@/lib/api/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}))

describe('shiftsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('getAllDoctors', () => {
    it('filters out doctors without doctorId or fullName', async () => {
      vi.mocked(http.get).mockResolvedValueOnce({
        data: {
          result: [
            { doctorId: 'doc-1', fullName: 'BS Nam', email: 'nam@clinic.com', isActive: true },
            { doctorId: '', fullName: 'Khong co id' },
            { doctorId: 'doc-3', fullName: '' },
          ],
        },
      } as any)

      const doctors = await shiftsApi.getAllDoctors()
      expect(http.get).toHaveBeenCalledWith('/admin/doctors/brief')
      expect(doctors).toHaveLength(1)
      expect(doctors[0]?.doctorId).toBe('doc-1')
    })
  })

  describe('getDoctorShifts', () => {
    it('correctly maps TimeSpan object and string times', async () => {
      vi.mocked(http.get).mockResolvedValueOnce({
        data: {
          result: {
            doctorId: 'doc-1',
            doctorName: 'BS Nam',
            startDate: '2026-11-01',
            endDate: '2026-11-07',
            schedules: [
              {
                id: 'sch-1',
                date: '2026-11-02',
                startTime: '08:00:00',
                endTime: '12:00:00',
                recommendedCapacity: 15,
                status: 'Active',
              },
              {
                id: 'sch-2',
                date: '2026-11-03',
                startTime: { hours: 13, minutes: 30 },
                endTime: { hours: 17, minutes: 0 },
                recommendedCapacity: 20,
                status: 'Active',
              },
            ],
          },
        },
      } as any)

      const res = await shiftsApi.getDoctorShifts('doc-1', '2026-11-01', '2026-11-07')

      expect(http.get).toHaveBeenCalledWith('/doctors/doc-1/shifts', {
        params: {
          StartDate: '2026-11-01',
          EndDate: '2026-11-07',
        },
      })
      expect(res.schedules).toHaveLength(2)
      // Check HH:mm string formatting
      expect(res.schedules[0]?.startTime).toBe('08:00')
      expect(res.schedules[0]?.endTime).toBe('12:00')
      // Check TimeSpan object formatting
      expect(res.schedules[1]?.startTime).toBe('13:30')
      expect(res.schedules[1]?.endTime).toBe('17:00')
    })
  })

  describe('createDoctorShift', () => {
    it('ensures seconds are appended to times and sends payload', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({
        data: {
          result: {
            id: 'sch-new',
            date: '2026-11-05',
            startTime: '08:00:00',
            endTime: '12:00:00',
            recommendedCapacity: 25,
          },
        },
      } as any)

      const payload = {
        doctorId: 'doc-1',
        date: '2026-11-05',
        startTime: '08:00',
        endTime: '12:00',
        RecommendedCapacity: 25,
      }

      const res = await shiftsApi.createDoctorShift(payload)

      expect(http.post).toHaveBeenCalledWith('/doctors/doc-1/shifts', {
        date: '2026-11-05',
        startTime: '08:00:00',
        endTime: '12:00:00',
        RecommendedCapacity: 25,
      })
      expect(res.id).toBe('sch-new')
    })
  })

  describe('approveShiftSuggestion & rejectShiftSuggestion', () => {
    it('approveShiftSuggestion posts to /shifts/suggestions/:id/approve', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({ data: {} } as any)

      await shiftsApi.approveShiftSuggestion('sug-1')
      expect(http.post).toHaveBeenCalledWith('/shifts/suggestions/sug-1/approve')
    })

    it('rejectShiftSuggestion posts to /shifts/suggestions/:id/reject', async () => {
      vi.mocked(http.post).mockResolvedValueOnce({ data: {} } as any)

      await shiftsApi.rejectShiftSuggestion('sug-1')
      expect(http.post).toHaveBeenCalledWith('/shifts/suggestions/sug-1/reject')
    })
  })
})
